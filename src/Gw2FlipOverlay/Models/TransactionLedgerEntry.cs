using System;

namespace Gw2FlipOverlay.Models;

public sealed class TransactionLedgerEntry {

    public int ItemId { get; set; }

    public string ItemName { get; set; } = string.Empty;

    public int HeldQuantity { get; set; }

    public int BoughtQuantity { get; set; }

    public int SoldQuantity { get; set; }

    public int AverageBuyPriceCopper { get; set; }

    public int AverageSellPriceCopper { get; set; }

    public int FeesPaidCopper { get; set; }

    public int RealizedProfitCopper { get; set; }

    public int UnrealizedProfitCopper { get; set; }

    public int CurrentSellFloorCopper { get; set; }

    public int CurrentOpenBuyQuantity { get; set; }

    public int CurrentOpenSellQuantity { get; set; }

    public decimal MarketConfidenceScore { get; set; }

    public RecommendationState RecommendationState { get; set; }

    public string Notes { get; set; } = string.Empty;

    public DateTimeOffset? LastBoughtUtc { get; set; }

    public DateTimeOffset? LastSoldUtc { get; set; }
}
