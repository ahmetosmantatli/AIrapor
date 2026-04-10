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

    public PdfReportService(AppDbContext db, IDirectiveEngineService directives)
    {
        _db = db;
        _directives = directives;
    }

    public async Task<byte[]> BuildAnalysisReportAsync(int userId, CancellationToken cancellationToken = default)
    {
        var user = await _db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new { u.Email, u.Currency })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (user is null)
        {
            throw new InvalidOperationException($"Kullanıcı bulunamadı: {userId}");
        }

        var directives = await _directives.GetActiveDirectivesAsync(userId, cancellationToken).ConfigureAwait(false);
        var rawCount = await _db.RawInsights.CountAsync(r => r.UserId == userId, cancellationToken)
            .ConfigureAwait(false);
        var computedCount = await (
                from c in _db.ComputedMetrics
                join r in _db.RawInsights on c.RawInsightId equals r.Id
                where r.UserId == userId
                select c)
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
                                    row.RelativeItem().Text("Meta Reklam Analiz Raporu").SemiBold().FontSize(16);
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
}
