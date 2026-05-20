using Gw2FlipOverlay.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Gw2FlipOverlay.Services;

public sealed class MockMarketDataProvider : IMarketDataProvider {

    private readonly FlipScoringService _scoringService;

    public MockMarketDataProvider(FlipScoringService scoringService) {
        _scoringService = scoringService;
    }

    public string SourceName => "mock data";

    public Task<MarketScanResult> GetScanAsync(FlipQueryOptions queryOptions, ScanExecutionMode scanMode, CancellationToken cancellationToken, IProgress<ScanProgressUpdate> progress = null) {
        var generatedAtUtc = DateTimeOffset.UtcNow;
        var flipRows = new[] {
            Seed(19721, "Glob of Ectoplasm", "CraftingMaterial", "Rare", 1874, 2311, 9210, 8742, 18, -7),
            Seed(24358, "Orichalcum Ingot", "CraftingMaterial", "Fine", 391, 487, 23610, 19442, 5, 3),
            Seed(19697, "Vicious Claw", "CraftingMaterial", "Fine", 1160, 1468, 4820, 4330, 12, 9),
            Seed(19976, "Armored Scale", "CraftingMaterial", "Fine", 1864, 2279, 6120, 5785, 24, 11),
            Seed(19700, "Ancient Bone", "CraftingMaterial", "Fine", 901, 1112, 9420, 8844, -8, -2),
            Seed(13006, "Powerful Venom Sac", "CraftingMaterial", "Fine", 310, 399, 22110, 24860, 4, 2),
            Seed(36041, "Elaborate Totem", "CraftingMaterial", "Fine", 1654, 2030, 3920, 3550, 14, 10),
            Seed(46747, "Ancient Wood Plank", "CraftingMaterial", "Fine", 688, 828, 10340, 9940, 6, 4),
            Seed(24351, "Mithril Ingot", "CraftingMaterial", "Basic", 217, 274, 31220, 30140, 3, 2),
            Seed(24284, "Bolt of Linen", "CraftingMaterial", "Basic", 198, 246, 29040, 28110, -1, 1),
            Seed(24295, "Bolt of Silk", "CraftingMaterial", "Fine", 334, 409, 16770, 17410, 9, 5),
            Seed(46740, "Seasoned Wood Plank", "CraftingMaterial", "Basic", 121, 161, 24320, 25280, 2, 2),
            Seed(24502, "Cured Hardened Leather Square", "CraftingMaterial", "Fine", 792, 990, 14210, 11980, 7, 4),
            Seed(19685, "Vial of Potent Blood", "CraftingMaterial", "Fine", 1730, 2140, 5130, 4020, 12, 5)
        };

        var craftRows = new[] {
            SeedCraft(46731, "Pile of Bloodstone Dust", "CraftingMaterial", "Basic", 980, 1460, 22110, 19040, 0, 14),
            SeedCraft(24309, "Onyx Core", "CraftingMaterial", "Rare", 4210, 5390, 6320, 5910, 0, 22),
            SeedCraft(44941, "Watchwork Sprocket", "CraftingMaterial", "Basic", 2110, 2790, 18420, 17330, 0, 8),
            SeedCraft(19684, "Vial of Powerful Blood", "CraftingMaterial", "Fine", 3340, 4210, 14820, 13910, 0, 17),
            SeedCraft(19701, "Ancient Fang", "CraftingMaterial", "Fine", 1120, 1570, 23640, 22180, 0, 9)
        };

        var valueRows = new[] {
            SeedValue(24295, "Bolt of Silk", "CraftingMaterial", "Fine", 358, 334, 409, 16770, 17410, 6),
            SeedValue(24502, "Cured Hardened Leather Square", "CraftingMaterial", "Fine", 865, 792, 990, 14210, 11980, 7),
            SeedValue(19697, "Vicious Claw", "CraftingMaterial", "Fine", 1305, 1160, 1468, 4820, 4330, 5),
            SeedValue(19700, "Ancient Bone", "CraftingMaterial", "Fine", 1030, 901, 1112, 9420, 8844, 8),
            SeedValue(36041, "Elaborate Totem", "CraftingMaterial", "Fine", 1890, 1654, 2030, 3920, 3550, 9)
        };
        var cooldownRows = new[] {
            SeedCooldown(46731, "Jeweled Damask Patch", "CraftingMaterial", "Rare", 6120, 8400, 610, 470),
            SeedCooldown(76063, "Deldrimor Steel Ingot", "CraftingMaterial", "Ascended", 22450, 28900, 180, 140),
            SeedCooldown(66913, "Spiritwood Plank", "CraftingMaterial", "Ascended", 19440, 25100, 210, 160)
        };
        var investmentRows = new[] {
            SeedInvestment(77532, "Continue Coin", "Consumable", "Fine", 1290, 1810, 402, 390, 780, 6.8m, "Active seasonal window: Super Adventure Festival"),
            SeedInvestment(36041, "Candy Corn Cob", "Consumable", "Fine", 9800, 12400, 520, 480, 9100, 7.2m, "Off-season watch: Halloween"),
            SeedInvestment(86069, "Red Lantern", "Consumable", "Fine", 1320, 1660, 280, 255, 1410, 4.5m, "Off-season watch: Lunar New Year")
        };

        var rows = queryOptions.OpportunityMode switch {
            OpportunityMode.Craft => craftRows,
            OpportunityMode.Cooldown => cooldownRows,
            OpportunityMode.Investment => investmentRows,
            OpportunityMode.Value => valueRows,
            _ => flipRows
        };

        var resultRows = rows
            .Where(row =>
                row.EstimatedProfit >= queryOptions.MinimumProfitCopper &&
                row.SpreadPercent >= queryOptions.MinimumRoiPercent &&
                row.MarketDepth >= queryOptions.MinimumMarketDepth &&
                (queryOptions.MaxAcquireCostCopper <= 0 || row.AcquisitionCostCopper <= queryOptions.MaxAcquireCostCopper) &&
                (!queryOptions.PracticalOnly || row.ItemType == "CraftingMaterial"))
            .OrderByDescending(row => row.FastFlipScore)
            .ThenByDescending(row => row.EstimatedProfit)
            .ToList();

        var result = new MarketScanResult() {
            Candidates = resultRows,
            TotalPriceRows = rows.Length,
            SavedSnapshotCount = 0,
            SnapshotRootPath = "mock history disabled",
            SourceName = SourceName,
            GeneratedAtUtc = generatedAtUtc,
            OpportunityMode = queryOptions.OpportunityMode,
            UniverseCandidateCount = resultRows.Count,
            FilteredCandidateCount = resultRows.Count,
            ActivePresetName = queryOptions.ActivePresetName ?? string.Empty
        };

        progress?.Report(new ScanProgressUpdate() {
            StatusMessage = $"Loaded {resultRows.Count} mock candidates via {scanMode} scan.",
            PartialResult = result
        });

        return Task.FromResult(result);
    }

    private FlipCandidate Seed(int itemId, string name, string itemType, string rarity, int highestBuy, int lowestSell, int buyStackSize, int sellStackSize, int buyDeltaCopper, int sellDeltaCopper) {
        var candidate = _scoringService.CreatePriceCandidate(itemId, highestBuy, lowestSell);
        candidate = _scoringService.FinalizeCandidate(candidate, name, buyStackSize, sellStackSize, SourceName);
        candidate.ItemType = itemType;
        candidate.Rarity = rarity;
        candidate.BuyDeltaCopper = buyDeltaCopper;
        candidate.SellDeltaCopper = sellDeltaCopper;
        candidate.PreviousSeenUtc = DateTimeOffset.UtcNow.AddMinutes(-5);
        candidate.AcquisitionCostCopper = candidate.HighestBuy;
        candidate.MarketDepth = Math.Min(buyStackSize, sellStackSize);
        candidate.VolumeScore = CalculateVolumeScore(candidate.MarketDepth);
        candidate.DemandPressure = CalculateDemandPressure(buyStackSize, sellStackSize);
        candidate.TurnoverScore = CalculateTurnoverScore(candidate.DemandPressure, candidate.AcquisitionCostCopper);
        candidate.FastFlipScore = CalculateFastFlipScore(candidate);
        candidate.ConfidenceScore = CalculateConfidenceScore(candidate.LiquidityScore, candidate.StabilityScore, candidate.VolumeScore, candidate.TurnoverScore, 1.0m);
        candidate.Score = candidate.FastFlipScore;
        candidate.OpportunityMode = OpportunityMode.Flip;
        candidate.FairValueCopper = lowestSell + 42;
        candidate.FairValueRecentMedianCopper = lowestSell + 35;
        candidate.FairValueWeightedCopper = lowestSell + 42;
        candidate.VolatilityPercent = 9.5m;
        candidate.SoldThroughConfidence = 74m;
        candidate.ExpectedFillsPerDay = 3.4m;
        candidate.ExpectedGoldPerDayCopper = (int)Math.Round(candidate.EstimatedProfit * candidate.ExpectedFillsPerDay);
        candidate.ExitQualityScore = 78m;
        candidate.CapitalEfficiencyScore = 61m;
        candidate.AdvisorScore = Math.Round((candidate.ExpectedGoldPerDayCopper / 100m) * 0.78m * 0.61m, 2);
        candidate.StrategyTag = AdvisorStrategyTag.FastFlip;
        ApplyMarketValue(candidate);

        return candidate;
    }

    private FlipCandidate SeedCraft(int itemId, string name, string itemType, string rarity, int craftCost, int outputSell, int buyDepth, int sellDepth, int buyDeltaCopper, int sellDeltaCopper) {
        var candidate = new FlipCandidate() {
            ItemId = itemId,
            ItemName = name,
            ItemType = itemType,
            Rarity = rarity,
            HighestBuy = craftCost,
            LowestSell = outputSell,
            NetResaleValue = _scoringService.CalculateNetResaleValue(outputSell),
            EstimatedProfit = _scoringService.CalculateNetResaleValue(outputSell) - craftCost,
            SpreadPercent = craftCost <= 0 ? 0m : Math.Round(((_scoringService.CalculateNetResaleValue(outputSell) - craftCost) / (decimal)craftCost) * 100m, 1),
            BuyStackSize = buyDepth,
            SellStackSize = sellDepth,
            AcquisitionCostCopper = craftCost,
            MarketDepth = Math.Min(buyDepth, sellDepth),
            BuyDeltaCopper = buyDeltaCopper,
            SellDeltaCopper = sellDeltaCopper,
            PreviousSeenUtc = DateTimeOffset.UtcNow.AddMinutes(-5),
            Source = "mock craft data",
            OpportunityMode = OpportunityMode.Craft,
            CraftIngredients = new List<CraftIngredientNeed>() {
                new CraftIngredientNeed() { ItemId = 24358, ItemName = "Orichalcum Ingot", RequiredCount = 2, UnitBuyPriceCopper = 391 },
                new CraftIngredientNeed() { ItemId = 19721, ItemName = "Glob of Ectoplasm", RequiredCount = 1, UnitBuyPriceCopper = 1874 }
            }
        };

        candidate.LiquidityScore = _scoringService.CalculateLiquidityScoreForDisplay(candidate.BuyStackSize, candidate.SellStackSize);
        candidate.StabilityScore = _scoringService.CalculateStabilityScoreForDisplay(candidate.BuyStackSize, candidate.SellStackSize);
        candidate.VolumeScore = CalculateVolumeScore(candidate.MarketDepth);
        candidate.DemandPressure = CalculateDemandPressure(candidate.BuyStackSize, candidate.SellStackSize);
        candidate.TurnoverScore = CalculateTurnoverScore(candidate.DemandPressure, candidate.AcquisitionCostCopper);
        candidate.FastFlipScore = CalculateFastFlipScore(candidate);
        candidate.ConfidenceScore = CalculateConfidenceScore(candidate.LiquidityScore, candidate.StabilityScore, candidate.VolumeScore, candidate.TurnoverScore, 1.0m);
        candidate.Score = candidate.FastFlipScore;
        candidate.FairValueCopper = outputSell + 85;
        candidate.FairValueRecentMedianCopper = outputSell + 62;
        candidate.FairValueWeightedCopper = outputSell + 85;
        candidate.VolatilityPercent = 11.2m;
        candidate.SoldThroughConfidence = 68m;
        candidate.ExpectedFillsPerDay = 1.8m;
        candidate.ExpectedGoldPerDayCopper = (int)Math.Round(candidate.EstimatedProfit * candidate.ExpectedFillsPerDay);
        candidate.ExitQualityScore = 70m;
        candidate.CapitalEfficiencyScore = 44m;
        candidate.AdvisorScore = Math.Round((candidate.ExpectedGoldPerDayCopper / 100m) * 0.70m * 0.44m, 2);
        candidate.StrategyTag = AdvisorStrategyTag.CraftMargin;
        ApplyMarketValue(candidate);

        return candidate;
    }

    private FlipCandidate SeedValue(int itemId, string name, string itemType, string rarity, int fairValue, int currentSell, int highestBuy, int buyDepth, int sellDepth, int historicalSamples) {
        var candidate = new FlipCandidate() {
            ItemId = itemId,
            ItemName = name,
            ItemType = itemType,
            Rarity = rarity,
            HighestBuy = highestBuy,
            LowestSell = currentSell,
            FairValueCopper = fairValue,
            NetResaleValue = _scoringService.CalculateNetResaleValue(fairValue),
            EstimatedProfit = _scoringService.CalculateNetResaleValue(fairValue) - currentSell,
            SpreadPercent = currentSell <= 0 ? 0m : Math.Round(((_scoringService.CalculateNetResaleValue(fairValue) - currentSell) / (decimal)currentSell) * 100m, 1),
            BuyStackSize = buyDepth,
            SellStackSize = sellDepth,
            AcquisitionCostCopper = currentSell,
            MarketDepth = Math.Min(buyDepth, sellDepth),
            BuyDeltaCopper = highestBuy - Math.Max(1, highestBuy - 7),
            SellDeltaCopper = currentSell - Math.Max(1, currentSell - 11),
            PreviousSeenUtc = DateTimeOffset.UtcNow.AddMinutes(-5),
            Source = "mock value data",
            OpportunityMode = OpportunityMode.Value,
            DiscountPercent = fairValue <= 0 ? 0m : Math.Round(((fairValue - currentSell) / (decimal)fairValue) * 100m, 1),
            HistoricalSampleCount = historicalSamples,
            FairValueRecentMedianCopper = fairValue,
            FairValueWeightedCopper = fairValue + 8,
            VolatilityPercent = 10.8m,
            SoldThroughConfidence = 70m
        };

        candidate.LiquidityScore = _scoringService.CalculateLiquidityScoreForDisplay(candidate.BuyStackSize, candidate.SellStackSize);
        candidate.StabilityScore = _scoringService.CalculateStabilityScoreForDisplay(candidate.BuyStackSize, candidate.SellStackSize);
        candidate.VolumeScore = CalculateVolumeScore(candidate.MarketDepth);
        candidate.DemandPressure = CalculateDemandPressure(candidate.BuyStackSize, candidate.SellStackSize);
        candidate.TurnoverScore = CalculateTurnoverScore(candidate.DemandPressure, candidate.AcquisitionCostCopper);
        candidate.ConfidenceScore = CalculateConfidenceScore(candidate.LiquidityScore, candidate.StabilityScore, candidate.VolumeScore, candidate.TurnoverScore, Math.Min(1.0m, historicalSamples / 8m));
        candidate.ValueScore = Math.Round(candidate.EstimatedProfit * (candidate.DiscountPercent / 18m) * (candidate.ConfidenceScore / 100m), 2);
        candidate.FastFlipScore = candidate.ValueScore;
        candidate.Score = candidate.ValueScore;
        candidate.ExpectedFillsPerDay = 1.2m;
        candidate.ExpectedGoldPerDayCopper = (int)Math.Round(candidate.EstimatedProfit * candidate.ExpectedFillsPerDay);
        candidate.ExitQualityScore = 68m;
        candidate.CapitalEfficiencyScore = 58m;
        candidate.AdvisorScore = Math.Round((candidate.ExpectedGoldPerDayCopper / 100m) * 0.68m * 0.58m, 2);
        candidate.StrategyTag = AdvisorStrategyTag.ValueReversion;
        ApplyMarketValue(candidate);

        return candidate;
    }

    private FlipCandidate SeedCooldown(int itemId, string name, string itemType, string rarity, int craftCost, int outputSell, int buyDepth, int sellDepth) {
        var candidate = SeedCraft(itemId, name, itemType, rarity, craftCost, outputSell, buyDepth, sellDepth, 0, 9);
        candidate.OpportunityMode = OpportunityMode.Cooldown;
        candidate.ExpectedFillsPerDay = 1.0m;
        candidate.ExpectedGoldPerDayCopper = candidate.EstimatedProfit;
        candidate.ExitQualityScore = 64m;
        candidate.CapitalEfficiencyScore = 35m;
        candidate.AdvisorScore = Math.Round((candidate.ExpectedGoldPerDayCopper / 100m) * 0.64m * 0.35m, 2);
        candidate.StrategyTag = AdvisorStrategyTag.Cooldown;
        candidate.InvestmentHorizonDays = 1;
        candidate.SeasonWindowState = "Daily cooldown";
        ApplyMarketValue(candidate);
        return candidate;
    }

    private FlipCandidate SeedInvestment(int itemId, string name, string itemType, string rarity, int lowestSell, int fairValue, int buyDepth, int sellDepth, int acquisitionCost, decimal discountPercent, string seasonWindowState) {
        var candidate = new FlipCandidate() {
            ItemId = itemId,
            ItemName = name,
            ItemType = itemType,
            Rarity = rarity,
            HighestBuy = lowestSell - 25,
            LowestSell = lowestSell,
            AcquisitionCostCopper = acquisitionCost,
            FairValueCopper = fairValue,
            FairValueRecentMedianCopper = fairValue - 60,
            FairValueWeightedCopper = fairValue,
            DiscountPercent = discountPercent,
            NetResaleValue = _scoringService.CalculateNetResaleValue(fairValue),
            EstimatedProfit = Math.Max(0, _scoringService.CalculateNetResaleValue(fairValue) - acquisitionCost),
            SpreadPercent = acquisitionCost <= 0 ? 0m : Math.Round((Math.Max(0, _scoringService.CalculateNetResaleValue(fairValue) - acquisitionCost) / (decimal)acquisitionCost) * 100m, 1),
            BuyStackSize = buyDepth,
            SellStackSize = sellDepth,
            MarketDepth = Math.Min(buyDepth, sellDepth),
            Source = "mock investment data",
            OpportunityMode = OpportunityMode.Investment,
            StrategyTag = AdvisorStrategyTag.Seasonal,
            InvestmentHorizonDays = 21,
            SeasonWindowState = seasonWindowState,
            VolatilityPercent = 14.5m,
            SoldThroughConfidence = 62m
        };

        candidate.LiquidityScore = _scoringService.CalculateLiquidityScoreForDisplay(candidate.BuyStackSize, candidate.SellStackSize);
        candidate.StabilityScore = _scoringService.CalculateStabilityScoreForDisplay(candidate.BuyStackSize, candidate.SellStackSize);
        candidate.VolumeScore = CalculateVolumeScore(candidate.MarketDepth);
        candidate.DemandPressure = CalculateDemandPressure(candidate.BuyStackSize, candidate.SellStackSize);
        candidate.TurnoverScore = CalculateTurnoverScore(candidate.DemandPressure, candidate.AcquisitionCostCopper);
        candidate.ConfidenceScore = CalculateConfidenceScore(candidate.LiquidityScore, candidate.StabilityScore, candidate.VolumeScore, candidate.TurnoverScore, 0.60m);
        candidate.ExpectedFillsPerDay = 0.35m;
        candidate.ExpectedGoldPerDayCopper = (int)Math.Round(candidate.EstimatedProfit * candidate.ExpectedFillsPerDay);
        candidate.ExitQualityScore = 59m;
        candidate.CapitalEfficiencyScore = 22m;
        candidate.AdvisorScore = Math.Round((candidate.ExpectedGoldPerDayCopper / 100m) * 0.59m * 0.22m, 2);
        candidate.ValueScore = candidate.AdvisorScore;
        candidate.FastFlipScore = candidate.AdvisorScore;
        candidate.Score = candidate.AdvisorScore;
        ApplyMarketValue(candidate);
        return candidate;
    }

    private static void ApplyMarketValue(FlipCandidate candidate) {
        if (candidate == null) {
            return;
        }

        var reference = candidate.FairValueWeightedCopper > 0 ? candidate.FairValueWeightedCopper : candidate.FairValueCopper;
        var price = candidate.OpportunityMode == OpportunityMode.Investment ? candidate.LowestSell : candidate.AcquisitionCostCopper;
        candidate.MarketValueReferenceCopper = reference;
        var value = MarketValueHelper.Calculate(price, reference);
        candidate.MarketValuePercent = value.Percent;
        candidate.MarketValueBand = value.Band;
        candidate.MarketValueLabel = value.Label;

        if (candidate.PriceHistory.Count == 0) {
            candidate.PriceHistory = BuildMockPriceHistory(candidate);
            candidate.HistoricalSampleCount = Math.Max(candidate.HistoricalSampleCount, candidate.PriceHistory.Count);
        }
    }

    private static List<PriceSnapshotEntry> BuildMockPriceHistory(FlipCandidate candidate) {
        var points = new List<PriceSnapshotEntry>();
        var now = DateTimeOffset.UtcNow;
        var baseSell = Math.Max(1, candidate.LowestSell);
        var baseBuy = Math.Max(1, candidate.HighestBuy > 0 ? candidate.HighestBuy : candidate.AcquisitionCostCopper);

        for (var i = 7; i >= 0; i--) {
            var wave = ((i % 4) - 1.5m) / 100m;
            var sell = Math.Max(1, (int)Math.Round(baseSell * (1m + wave)));
            var buy = Math.Max(1, (int)Math.Round(baseBuy * (1m + wave / 2m)));
            points.Add(new PriceSnapshotEntry() {
                ItemId = candidate.ItemId,
                HighestBuy = buy,
                LowestSell = sell,
                BuyQuantity = Math.Max(1, candidate.BuyStackSize),
                SellQuantity = Math.Max(1, candidate.SellStackSize),
                RecordedAtUtc = now.AddHours(-i * 3)
            });
        }

        if (points.Count > 0) {
            points[points.Count - 1].HighestBuy = baseBuy;
            points[points.Count - 1].LowestSell = baseSell;
        }

        return points;
    }

    private static decimal CalculateVolumeScore(int marketDepth) {
        var depthScore = Math.Min(1m, (decimal)Math.Log10(Math.Max(10, marketDepth)) / 4.5m);
        return Math.Max(0.15m, Math.Round(depthScore, 2));
    }

    private static decimal CalculateDemandPressure(int buyDepth, int sellDepth) {
        var ratio = buyDepth / (decimal)Math.Max(1, sellDepth);
        return Math.Round(Math.Max(0.05m, Math.Min(3.00m, ratio)), 2);
    }

    private static decimal CalculateTurnoverScore(decimal demandPressure, int acquisitionCostCopper) {
        var pressureScore = Math.Max(0.20m, Math.Min(1.35m, demandPressure / 1.15m));
        var affordabilityScore = acquisitionCostCopper <= 0
            ? 1m
            : Math.Max(0.15m, Math.Min(1.00m, 200000m / Math.Max(5000m, acquisitionCostCopper)));

        return Math.Round(pressureScore * affordabilityScore, 2);
    }

    private static decimal CalculateFastFlipScore(FlipCandidate candidate) {
        return Math.Round(candidate.EstimatedProfit * candidate.VolumeScore * candidate.TurnoverScore * candidate.LiquidityScore * candidate.StabilityScore, 2);
    }

    private static decimal CalculateConfidenceScore(decimal liquidityScore, decimal stabilityScore, decimal volumeScore, decimal turnoverScore, decimal historyConfidence) {
        var turnoverNormalized = Math.Max(0.15m, Math.Min(1.00m, turnoverScore / 1.35m));
        var blended =
            (liquidityScore * 0.24m) +
            (stabilityScore * 0.24m) +
            (volumeScore * 0.22m) +
            (turnoverNormalized * 0.20m) +
            (Math.Max(0.10m, Math.Min(1.00m, historyConfidence)) * 0.10m);
        return Math.Round(Math.Max(0.10m, Math.Min(1.00m, blended)) * 100m, 1);
    }
}
