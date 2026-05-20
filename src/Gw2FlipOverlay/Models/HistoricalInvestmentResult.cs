using System;

namespace Gw2FlipOverlay.Models;

public sealed class HistoricalInvestmentResult {

    public int ItemId { get; set; }

    public string ItemName { get; set; } = string.Empty;

    public int RealizedProfitCopper { get; set; }

    public int UnrealizedProfitCopper { get; set; }

    public int TotalProfitCopper { get; set; }

    public decimal ReturnOnCapitalPercent { get; set; }

    public int BoughtQuantity { get; set; }

    public int SoldQuantity { get; set; }

    public int HeldQuantity { get; set; }

    public int AverageBuyPriceCopper { get; set; }

    public int CurrentSellFloorCopper { get; set; }

    public DateTimeOffset? LastActivityUtc { get; set; }

    public string Verdict { get; set; } = string.Empty;
}
