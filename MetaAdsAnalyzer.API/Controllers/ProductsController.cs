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
[Route("api/products")]
public class ProductsController : ControllerBase
{
    private readonly AppDbContext _db;

    public ProductsController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet("by-user/{userId:int}")]
    public async Task<ActionResult<IReadOnlyList<ProductResponseDto>>> ListByUser(
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

        var list = await _db.Products.AsNoTracking()
            .Where(p => p.UserId == userId)
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new ProductResponseDto
            {
                Id = p.Id,
                UserId = p.UserId,
                Name = p.Name,
                Cogs = p.Cogs,
                SellingPrice = p.SellingPrice,
                ShippingCost = p.ShippingCost,
                PaymentFeePct = p.PaymentFeePct,
                ReturnRatePct = p.ReturnRatePct,
                LtvMultiplier = p.LtvMultiplier,
                TargetMarginPct = p.TargetMarginPct,
                CreatedAt = p.CreatedAt,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return Ok(list);
    }

    [HttpPost]
    public async Task<ActionResult<ProductResponseDto>> Create(
        [FromBody] CreateProductRequestDto body,
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

        if (body.SellingPrice <= 0 || body.LtvMultiplier <= 0)
        {
            return BadRequest(new { message = "Satış fiyatı ve LTV çarpanı pozitif olmalıdır." });
        }

        if (body.Cogs < 0 || body.ShippingCost < 0 || body.PaymentFeePct < 0 || body.ReturnRatePct < 0
            || body.TargetMarginPct < 0)
        {
            return BadRequest(new { message = "Maliyet ve yüzde alanları negatif olamaz." });
        }

        var userOk = await _db.Users.AnyAsync(u => u.Id == body.UserId, cancellationToken).ConfigureAwait(false);
        if (!userOk)
        {
            return BadRequest(new { message = "Kullanıcı bulunamadı." });
        }

        var entity = new Product
        {
            UserId = body.UserId,
            Name = body.Name.Trim(),
            Cogs = body.Cogs,
            SellingPrice = body.SellingPrice,
            ShippingCost = body.ShippingCost,
            PaymentFeePct = body.PaymentFeePct,
            ReturnRatePct = body.ReturnRatePct,
            LtvMultiplier = body.LtvMultiplier,
            TargetMarginPct = body.TargetMarginPct,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        _db.Products.Add(entity);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return CreatedAtAction(
            nameof(ListByUser),
            new { userId = entity.UserId },
            new ProductResponseDto
            {
                Id = entity.Id,
                UserId = entity.UserId,
                Name = entity.Name,
                Cogs = entity.Cogs,
                SellingPrice = entity.SellingPrice,
                ShippingCost = entity.ShippingCost,
                PaymentFeePct = entity.PaymentFeePct,
                ReturnRatePct = entity.ReturnRatePct,
                LtvMultiplier = entity.LtvMultiplier,
                TargetMarginPct = entity.TargetMarginPct,
                CreatedAt = entity.CreatedAt,
            });
    }
}
