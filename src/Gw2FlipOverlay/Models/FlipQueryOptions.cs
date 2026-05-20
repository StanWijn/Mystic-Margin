namespace Gw2FlipOverlay.Models;

public sealed class FlipQueryOptions {

    public int TopCount { get; set; }

    public int MinimumProfitCopper { get; set; }

    public FlipSortMode SortMode { get; set; }

    public bool PracticalOnly { get; set; }

    public int HistoryRetentionDays { get; set; }

    public OpportunityMode OpportunityMode { get; set; }

    public int MaxAcquireCostCopper { get; set; }

    public int MinimumMarketDepth { get; set; }

    public int MinimumDiscountPercent { get; set; }

    public int MinimumRoiPercent { get; set; }

    public bool WatchlistOnly { get; set; }

    public int MaxOwnedQuantity { get; set; }

    public int MaxOpenSellQuantity { get; set; }

    public int MaxVolatilityPercent { get; set; }

    public int AutoFlipQuantity { get; set; }

    public string ActivePresetName { get; set; } = string.Empty;

    public System.Collections.Generic.IReadOnlyList<AlertRule> AlertRules { get; set; } = System.Array.Empty<AlertRule>();
}
