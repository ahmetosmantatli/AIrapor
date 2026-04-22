using MetaAdsAnalyzer.API.Security;
using MetaAdsAnalyzer.API.Services;
using MetaAdsAnalyzer.Core;
using MetaAdsAnalyzer.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MetaAdsAnalyzer.API.Controllers;

[ApiController]
[Authorize]
[Route("api/video-assets")]
public class VideoAssetsController : ControllerBase
{
    private readonly AppDbContext _db;

    public VideoAssetsController(AppDbContext db)
    {
        _db = db;
    }

    public sealed class VideoAssetRowDto
    {
        public string VideoId { get; set; } = null!;

        public string? ThumbnailUrl { get; set; }

        public string? RepresentativeAdName { get; set; }

        public decimal TotalSpend { get; set; }

        public decimal? HookRateAvg { get; set; }

        public decimal? HoldRateAvg { get; set; }

        public decimal? CompletionRateAvg { get; set; }

        public decimal? TotalRoas { get; set; }

        public IReadOnlyList<string> ProblemTags { get; set; } = Array.Empty<string>();
    }

    [HttpGet("by-user/{userId:int}")]
    public async Task<ActionResult<IReadOnlyList<VideoAssetRowDto>>> ListByUser(
        int userId,
        [FromQuery] string? metaAdAccountId,
        CancellationToken cancellationToken)
    {
        if (userId <= 0)
        {
            return BadRequest();
        }

        var auth = this.EnsureOwnUser(userId);
        if (auth is not null)
        {
            return auth;
        }

        var act = MetaAdAccountIdNormalizer.Normalize(metaAdAccountId);
        if (string.IsNullOrEmpty(act))
        {
            act = MetaAdAccountIdNormalizer.Normalize(
                await _db.Users.AsNoTracking()
                    .Where(u => u.Id == userId)
                    .Select(u => u.MetaAdAccountId)
                    .FirstOrDefaultAsync(cancellationToken)
                    .ConfigureAwait(false));
        }

        if (string.IsNullOrEmpty(act))
        {
            return Ok(Array.Empty<VideoAssetRowDto>());
        }

        var rows = await _db.VideoAssets.AsNoTracking()
            .Where(v => v.UserId == userId && v.MetaAdAccountId == act)
            .OrderByDescending(v => v.TotalSpend)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var list = rows.Select(
                v =>
                {
                    var tags = VideoNarrativeBuilder.BuildProblemTags(
                        v.HookRateAvg,
                        v.HoldRateAvg,
                        v.CompletionRateAvg,
                        v.TotalRoas,
                        null,
                        null,
                        0,
                        null,
                        0);
                    return new VideoAssetRowDto
                    {
                        VideoId = v.VideoId,
                        ThumbnailUrl = v.ThumbnailUrl,
                        RepresentativeAdName = v.RepresentativeAdName,
                        TotalSpend = v.TotalSpend,
                        HookRateAvg = v.HookRateAvg,
                        HoldRateAvg = v.HoldRateAvg,
                        CompletionRateAvg = v.CompletionRateAvg,
                        TotalRoas = v.TotalRoas,
                        ProblemTags = tags,
                    };
                })
            .ToList();

        return Ok(list);
    }
}
