using MetaAdsAnalyzer.API.Models;
using MetaAdsAnalyzer.API.Security;
using MetaAdsAnalyzer.Core.Entities;
using MetaAdsAnalyzer.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MetaAdsAnalyzer.API.Controllers;

[ApiController]
[Authorize]
[Route("api/campaign-product-maps")]
public class CampaignMapsController : ControllerBase
{
    private readonly AppDbContext _db;

    public CampaignMapsController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet("by-user/{userId:int}")]
    public async Task<ActionResult<IReadOnlyList<CampaignProductMapResponseDto>>> ListByUser(
        int userId,
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

        var list = await _db.CampaignProductMaps.AsNoTracking()
            .Where(m => m.UserId == userId)
            .Select(m => new CampaignProductMapResponseDto
            {
                Id = m.Id,
                CampaignId = m.CampaignId,
                ProductId = m.ProductId,
                UserId = m.UserId,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return Ok(list);
    }

    [HttpPost]
    public async Task<ActionResult<CampaignProductMapResponseDto>> Create(
        [FromBody] CreateCampaignProductMapRequestDto body,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var auth = this.EnsureOwnUser(body.UserId);
        if (auth is not null)
        {
            return auth;
        }

        var product = await _db.Products.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == body.ProductId && p.UserId == body.UserId, cancellationToken)
            .ConfigureAwait(false);
        if (product is null)
        {
            return BadRequest(new { message = "Ürün bu kullanıcıya ait değil." });
        }

        var userOk = await _db.Users.AnyAsync(u => u.Id == body.UserId, cancellationToken).ConfigureAwait(false);
        if (!userOk)
        {
            return BadRequest(new { message = "Kullanıcı bulunamadı." });
        }

        var campaignId = body.CampaignId.Trim();
        var existing = await _db.CampaignProductMaps
            .FirstOrDefaultAsync(m => m.UserId == body.UserId && m.CampaignId == campaignId, cancellationToken)
            .ConfigureAwait(false);

        if (existing is not null)
        {
            existing.ProductId = body.ProductId;
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return Ok(
                new CampaignProductMapResponseDto
                {
                    Id = existing.Id,
                    CampaignId = existing.CampaignId,
                    ProductId = existing.ProductId,
                    UserId = existing.UserId,
                });
        }

        var map = new CampaignProductMap
        {
            UserId = body.UserId,
            CampaignId = campaignId,
            ProductId = body.ProductId,
        };

        _db.CampaignProductMaps.Add(map);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return CreatedAtAction(
            nameof(ListByUser),
            new { userId = map.UserId },
            new CampaignProductMapResponseDto
            {
                Id = map.Id,
                CampaignId = map.CampaignId,
                ProductId = map.ProductId,
                UserId = map.UserId,
            });
    }
}
