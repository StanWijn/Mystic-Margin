using System.Collections.Generic;

namespace Gw2FlipOverlay.Models;

public sealed class ScanPreset {

    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public int TopCount { get; set; }

    public int MinimumProfitCopper { get; set; }

    public int MinimumRoiPercent { get; set; }

    public FlipSortMode SortMode { get; set; }

    public bool PracticalOnly { get; set; }

    public OpportunityMode OpportunityMode { get; set; }

    public int MaxAcquireCostCopper { get; set; }

    public int MinimumMarketDepth { get; set; }

    public int MinimumDiscountPercent { get; set; }

    public bool WatchlistOnly { get; set; }

    public int MaxOwnedQuantity { get; set; }

    public int MaxOpenSellQuantity { get; set; }

    public int MaxVolatilityPercent { get; set; }

    public List<AlertRule> AlertRules { get; set; } = AlertRule.CreateDefaultRules();

    public ScanPreset Clone() {
        return new ScanPreset() {
            Id = Id,
            Name = Name,
            TopCount = TopCount,
            MinimumProfitCopper = MinimumProfitCopper,
            MinimumRoiPercent = MinimumRoiPercent,
            SortMode = SortMode,
            PracticalOnly = PracticalOnly,
            OpportunityMode = OpportunityMode,
            MaxAcquireCostCopper = MaxAcquireCostCopper,
            MinimumMarketDepth = MinimumMarketDepth,
            MinimumDiscountPercent = MinimumDiscountPercent,
            WatchlistOnly = WatchlistOnly,
            MaxOwnedQuantity = MaxOwnedQuantity,
            MaxOpenSellQuantity = MaxOpenSellQuantity,
            MaxVolatilityPercent = MaxVolatilityPercent,
            AlertRules = new List<AlertRule>(AlertRules ?? AlertRule.CreateDefaultRules())
        };
    }

    public static List<ScanPreset> CreateDefaults() {
        return new List<ScanPreset>() {
            new ScanPreset() {
                Id = "starter-volume",
                Name = "Starter Volume",
                TopCount = 20,
                MinimumProfitCopper = 250,
                MinimumRoiPercent = 4,
                SortMode = FlipSortMode.Score,
                PracticalOnly = true,
                OpportunityMode = OpportunityMode.Flip,
                MaxAcquireCostCopper = 50000,
                MinimumMarketDepth = 2000,
                MinimumDiscountPercent = 0,
                WatchlistOnly = false,
                MaxOwnedQuantity = 150,
                MaxOpenSellQuantity = 50,
                MaxVolatilityPercent = 25
            },
            new ScanPreset() {
                Id = "daily-volume",
                Name = "Daily Volume",
                TopCount = 30,
                MinimumProfitCopper = 500,
                MinimumRoiPercent = 5,
                SortMode = FlipSortMode.Score,
                PracticalOnly = true,
                OpportunityMode = OpportunityMode.Flip,
                MaxAcquireCostCopper = 200000,
                MinimumMarketDepth = 2000,
                MinimumDiscountPercent = 0,
                WatchlistOnly = false,
                MaxOwnedQuantity = 100,
                MaxOpenSellQuantity = 50,
                MaxVolatilityPercent = 25
            },
            new ScanPreset() {
                Id = "craft-margin",
                Name = "Craft Margin",
                TopCount = 20,
                MinimumProfitCopper = 1000,
                MinimumRoiPercent = 7,
                SortMode = FlipSortMode.Score,
                PracticalOnly = true,
                OpportunityMode = OpportunityMode.Craft,
                MaxAcquireCostCopper = 1000000,
                MinimumMarketDepth = 500,
                MinimumDiscountPercent = 0,
                WatchlistOnly = false,
                MaxOwnedQuantity = 50,
                MaxOpenSellQuantity = 25,
                MaxVolatilityPercent = 25
            },
            new ScanPreset() {
                Id = "deep-value",
                Name = "Deep Value",
                TopCount = 20,
                MinimumProfitCopper = 500,
                MinimumRoiPercent = 8,
                SortMode = FlipSortMode.Score,
                PracticalOnly = true,
                OpportunityMode = OpportunityMode.Value,
                MaxAcquireCostCopper = 500000,
                MinimumMarketDepth = 2000,
                MinimumDiscountPercent = 10,
                WatchlistOnly = false,
                MaxOwnedQuantity = 50,
                MaxOpenSellQuantity = 20,
                MaxVolatilityPercent = 25
            },
            new ScanPreset() {
                Id = "daily-cooldowns",
                Name = "Daily Cooldowns",
                TopCount = 15,
                MinimumProfitCopper = 5000,
                MinimumRoiPercent = 25,
                SortMode = FlipSortMode.Score,
                PracticalOnly = false,
                OpportunityMode = OpportunityMode.Cooldown,
                MaxAcquireCostCopper = 100000,
                MinimumMarketDepth = 250,
                MinimumDiscountPercent = 0,
                WatchlistOnly = false,
                MaxOwnedQuantity = 25,
                MaxOpenSellQuantity = 10,
                MaxVolatilityPercent = 25
            },
            new ScanPreset() {
                Id = "seasonal-watch",
                Name = "Seasonal Watch",
                TopCount = 20,
                MinimumProfitCopper = 1000,
                MinimumRoiPercent = 6,
                SortMode = FlipSortMode.Score,
                PracticalOnly = false,
                OpportunityMode = OpportunityMode.Investment,
                MaxAcquireCostCopper = 1500000,
                MinimumMarketDepth = 1000,
                MinimumDiscountPercent = 6,
                WatchlistOnly = false,
                MaxOwnedQuantity = 75,
                MaxOpenSellQuantity = 25,
                MaxVolatilityPercent = 25
            }
        };
    }
}
