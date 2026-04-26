namespace MetaAdsAnalyzer.API.Services;

/// <summary>Kural tabanlı karlılık ve kreatif oranları (Faza 3.2–3.3).</summary>
public static class ProfitMath
{
    /// <param name="returnRatePct">İade oranı %; net ciro = satış × (1 − iade%). Ödeme komisyonu brüt satış üzerinden.</param>
    public static decimal ContributionMargin(
        decimal sellingPrice,
        decimal cogs,
        decimal shippingCost,
        decimal paymentFeePct,
        decimal returnRatePct = 0)
    {
        var rr = returnRatePct < 0 ? 0 : (returnRatePct > 100 ? 100 : returnRatePct);
        var netRevenue = sellingPrice * (1m - rr / 100m);
        var fee = sellingPrice * (paymentFeePct / 100m);
        return netRevenue - cogs - shippingCost - fee;
    }

    public static decimal? Roas(decimal purchaseValue, decimal ltvMultiplier, decimal spend)
    {
        if (spend <= 0)
        {
            return null;
        }

        return purchaseValue * ltvMultiplier / spend;
    }

    public static decimal? Cpa(decimal spend, long purchases)
    {
        if (purchases <= 0)
        {
            return null;
        }

        return spend / purchases;
    }

    public static decimal? BreakEvenRoas(decimal sellingPrice, decimal contributionMargin)
    {
        if (contributionMargin <= 0)
        {
            return null;
        }

        return sellingPrice / contributionMargin;
    }

    public static decimal TargetProfitAmount(decimal sellingPrice, decimal targetMarginPct) =>
        sellingPrice * (targetMarginPct / 100m);

    public static decimal? TargetRoas(decimal sellingPrice, decimal contributionMargin, decimal targetMarginPct)
    {
        var targetProfit = TargetProfitAmount(sellingPrice, targetMarginPct);
        var denom = contributionMargin - targetProfit;
        if (denom <= 0)
        {
            return null;
        }

        return sellingPrice / denom;
    }

    public static decimal? MaxCpa(decimal contributionMargin) =>
        contributionMargin <= 0 ? null : contributionMargin;

    public static decimal? TargetCpa(decimal contributionMargin, decimal sellingPrice, decimal targetMarginPct)
    {
        var targetProfit = TargetProfitAmount(sellingPrice, targetMarginPct);
        var v = contributionMargin - targetProfit;
        return v <= 0 ? null : v;
    }

    public static decimal? NetProfitPerOrder(decimal contributionMargin, decimal? cpa) =>
        cpa is null ? null : contributionMargin - cpa.Value;

    public static decimal? NetMarginPct(decimal? netProfitPerOrder, decimal sellingPrice)
    {
        if (netProfitPerOrder is null || sellingPrice <= 0)
        {
            return null;
        }

        return netProfitPerOrder.Value / sellingPrice * 100m;
    }

    public static decimal? HookRatePct(long impressions, long videoPlay3s)
    {
        if (impressions <= 0)
        {
            return null;
        }

        return (decimal)videoPlay3s / impressions * 100m;
    }

    /// <summary>3 sn izlenme / erişim ×100 (thumbstop).</summary>
    public static decimal? ThumbstopRatePct(long reach, long videoPlay3s)
    {
        if (reach <= 0)
        {
            return null;
        }

        return (decimal)videoPlay3s / reach * 100m;
    }

    /// <summary>Thruplay / 3 sn izlenme ×100 (hold).</summary>
    public static decimal? HoldRatePct(long videoPlay3s, long videoThruplay)
    {
        if (videoPlay3s <= 0)
        {
            return null;
        }

        return (decimal)videoThruplay / videoPlay3s * 100m;
    }

    public static decimal? CompletionRatePct(long impressions, long videoP100)
    {
        if (impressions <= 0)
        {
            return null;
        }

        return (decimal)videoP100 / impressions * 100m;
    }

    public static decimal? VideoViewsPerSpend(long views, decimal spend)
    {
        if (spend <= 0 || views <= 0)
        {
            return null;
        }

        return views / spend;
    }

    public static decimal? MismatchRatio(decimal ctrAll, decimal ctrLink)
    {
        if (ctrLink <= 0)
        {
            return null;
        }

        return ctrAll / ctrLink;
    }
}
