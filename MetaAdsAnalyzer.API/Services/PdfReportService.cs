using MetaAdsAnalyzer.API.Extensions;
using MetaAdsAnalyzer.Core;
using MetaAdsAnalyzer.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace MetaAdsAnalyzer.API.Services;

public sealed class PdfReportService : IPdfReportService
{
    private readonly AppDbContext _db;
    private readonly IDirectiveEngineService _directives;
    private readonly IVideoReportInsightService _videoInsights;

    public PdfReportService(
        AppDbContext db,
        IDirectiveEngineService directives,
        IVideoReportInsightService videoInsights)
    {
        _db = db;
        _directives = directives;
        _videoInsights = videoInsights;
    }

    public async Task<byte[]> BuildAnalysisReportAsync(int userId, CancellationToken cancellationToken = default)
    {
        var user = await _db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new { u.Email, u.Currency, u.MetaAdAccountId })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (user is null)
        {
            throw new InvalidOperationException($"Kullanıcı bulunamadı: {userId}");
        }

        var directives = await _directives.GetActiveDirectivesAsync(userId, cancellationToken).ConfigureAwait(false);
        var rawCount = await _db.RawInsights.AsNoTracking()
            .ForUserActiveAdAccount(userId, user.MetaAdAccountId)
            .CountAsync(cancellationToken)
            .ConfigureAwait(false);

        var act = MetaAdAccountIdNormalizer.Normalize(user.MetaAdAccountId);
        var computedCount = await (
                from c in _db.ComputedMetrics
                join r in _db.RawInsights on c.RawInsightId equals r.Id
                where r.UserId == userId
                      && (string.IsNullOrEmpty(act) ? r.MetaAdAccountId == null : r.MetaAdAccountId == act)
                select c.Id)
            .CountAsync(cancellationToken)
            .ConfigureAwait(false);

        QuestPDF.Settings.License = LicenseType.Community;

        var doc = Document.Create(
            container =>
            {
                container.Page(
                    page =>
                    {
                        page.Margin(40);
                        page.Size(PageSizes.A4);
                        page.DefaultTextStyle(x => x.FontSize(10));

                        page.Header()
                            .Row(
                                row =>
                                {
                                    row.RelativeItem().Text("Adlyz Meta Reklam Raporu").SemiBold().FontSize(16);
                                    row.ConstantItem(120).AlignRight().Text(DateTimeOffset.UtcNow.ToString("yyyy-MM-dd HH:mm") + " UTC").FontSize(9);
                                });

                        page.Content().Column(
                            column =>
                            {
                                column.Spacing(12);
                                column.Item().Text($"Kullanıcı: {user.Email}");
                                column.Item().Text($"Para birimi: {user.Currency}");
                                column.Item().Text($"Ham insight satırı: {rawCount} · Hesaplanmış metrik: {computedCount}");

                                column.Item().PaddingTop(8).Text("Aktif direktifler").SemiBold().FontSize(12);

                                if (directives.Count == 0)
                                {
                                    column.Item().Text("Kayıtlı aktif direktif yok.").Italic().FontColor(Colors.Grey.Medium);
                                }
                                else
                                {
                                    column.Item().Table(
                                        table =>
                                        {
                                            table.ColumnsDefinition(
                                                columns =>
                                                {
                                                    columns.RelativeColumn(1.2f);
                                                    columns.RelativeColumn(2f);
                                                    columns.RelativeColumn(1f);
                                                    columns.RelativeColumn(0.8f);
                                                    columns.RelativeColumn(0.8f);
                                                    columns.RelativeColumn(3f);
                                                });

                                            table.Header(
                                                header =>
                                                {
                                                    static IContainer CellStyle(IContainer c) =>
                                                        c.DefaultTextStyle(x => x.SemiBold()).PaddingVertical(4).BorderBottom(1).BorderColor(Colors.Grey.Lighten2);

                                                    header.Cell().Element(CellStyle).Text("Tip");
                                                    header.Cell().Element(CellStyle).Text("Varlık");
                                                    header.Cell().Element(CellStyle).Text("Direktif");
                                                    header.Cell().Element(CellStyle).Text("Skor");
                                                    header.Cell().Element(CellStyle).Text("Sağlık");
                                                    header.Cell().Element(CellStyle).Text("Mesaj");
                                                });

                                            foreach (var d in directives.Take(200))
                                            {
                                                static IContainer Cell(IContainer c) =>
                                                    c.PaddingVertical(3).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten3);

                                                table.Cell().Element(Cell).Text(d.EntityType);
                                                table.Cell().Element(Cell).Text(d.EntityId);
                                                table.Cell().Element(Cell).Text(d.DirectiveType);
                                                table.Cell().Element(Cell).Text(d.Score?.ToString() ?? "—");
                                                table.Cell().Element(Cell).Text(d.HealthStatus ?? "—");
                                                table.Cell().Element(Cell).Text(d.Message);
                                            }
                                        });
                                }
                            });

                        page.Footer()
                            .AlignCenter()
                            .DefaultTextStyle(x => x.FontSize(8).FontColor(Colors.Grey.Medium))
                            .Text(
                                t =>
                                {
                                    t.CurrentPageNumber();
                                    t.Span(" / ");
                                    t.TotalPages();
                                });
                    });
            });

        return doc.GeneratePdf();
    }

    public async Task<byte[]> BuildVideoReportPdfAsync(
        int userId,
        string? metaAdAccountId,
        IReadOnlyList<string> adIds,
        string? videoId,
        string? displayName,
        CancellationToken cancellationToken = default)
    {
        var agg = await _videoInsights.BuildAggregateAsync(userId, metaAdAccountId, adIds, cancellationToken)
            .ConfigureAwait(false);
        if (!agg.HasInsightRows)
        {
            throw new InvalidOperationException(agg.DiagnosticMessage ?? "Bu reklamlar için ham insight bulunamadı.");
        }

        var set = adIds.ToHashSet(StringComparer.Ordinal);
        var directives = await _db.Directives.AsNoTracking()
            .Where(d => d.UserId == userId && d.IsActive && d.EntityType == "ad" && set.Contains(d.EntityId))
            .OrderBy(d => d.Severity == "critical" ? 0 : d.Severity == "warning" ? 1 : 2)
            .ThenByDescending(d => d.TriggeredAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        QuestPDF.Settings.License = LicenseType.Community;

        var title = string.IsNullOrWhiteSpace(displayName) ? "Video raporu" : displayName.Trim();
        var vidLine = string.IsNullOrWhiteSpace(videoId) ? "—" : videoId.Trim();

        var doc = Document.Create(
            container =>
            {
                container.Page(
                    page =>
                    {
                        page.Margin(40);
                        page.Size(PageSizes.A4);
                        page.DefaultTextStyle(x => x.FontSize(10));

                        page.Header()
                            .Row(
                                row =>
                                {
                                    row.RelativeItem().Text("AI Video Raporu").SemiBold().FontSize(16);
                                    row.ConstantItem(120).AlignRight().Text(DateTimeOffset.UtcNow.ToString("yyyy-MM-dd HH:mm") + " UTC").FontSize(9);
                                });

                        page.Content().Column(
                            column =>
                            {
                                column.Spacing(10);
                                column.Item().Text(title).SemiBold().FontSize(13);
                                column.Item().Text($"video_id: {vidLine}");
                                column.Item().Text($"Reklam sayısı: {adIds.Count}");

                                column.Item().PaddingTop(6).Text("Birleşik metrikler").SemiBold().FontSize(12);
                                column.Item().Text($"Harcama: {agg.Spend:F2}");
                                column.Item().Text($"Gösterim: {agg.Impressions} · Erişim: {agg.Reach} · Link tıklaması: {agg.LinkClicks}");
                                column.Item().Text($"Satın alma: {agg.Purchases} · Satış cirosu: {agg.PurchaseValue:F2}");
                                column.Item().Text(
                                    $"Thumbstop %: {agg.ThumbstopPct?.ToString("F2") ?? "—"} · Hold %: {agg.HoldPct?.ToString("F2") ?? "—"} · Completion %: {agg.CompletionPct?.ToString("F2") ?? "—"}");
                                column.Item().Text(
                                    $"ROAS: {agg.Roas?.ToString("F2") ?? "—"} · Kâr eşiği ROAS: {agg.BreakEvenRoas?.ToString("F2") ?? "—"} · Hedef ROAS: {agg.TargetRoas?.ToString("F2") ?? "—"}");
                                column.Item().Text($"Kreatif skor: {agg.CreativeScore?.ToString() ?? "—"}/100");

                                column.Item().PaddingTop(8).Text("Özet").SemiBold().FontSize(12);
                                if (agg.NarrativeLines.Count == 0)
                                {
                                    column.Item().Text("—").Italic().FontColor(Colors.Grey.Medium);
                                }
                                else
                                {
                                    foreach (var line in agg.NarrativeLines)
                                    {
                                        column.Item().Text("• " + line);
                                    }
                                }

                                column.Item().PaddingTop(8).Text("Problem etiketleri").SemiBold().FontSize(12);
                                column.Item().Text(agg.ProblemTags.Count == 0 ? "—" : string.Join(" · ", agg.ProblemTags));

                                column.Item().PaddingTop(8).Text("Direktifler").SemiBold().FontSize(12);
                                if (directives.Count == 0)
                                {
                                    column.Item().Text("Aktif direktif yok.").Italic().FontColor(Colors.Grey.Medium);
                                }
                                else
                                {
                                    foreach (var d in directives.Take(40))
                                    {
                                        column.Item().Text($"[{d.Severity}] {d.Message}");
                                    }
                                }
                            });

                        page.Footer()
                            .AlignCenter()
                            .DefaultTextStyle(x => x.FontSize(8).FontColor(Colors.Grey.Medium))
                            .Text(
                                t =>
                                {
                                    t.CurrentPageNumber();
                                    t.Span(" / ");
                                    t.TotalPages();
                                });
                    });
            });

        return doc.GeneratePdf();
    }
}
