using Gw2FlipOverlay.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Gw2FlipOverlay.Services;

public sealed class CandidateInsightService {

    public void EnrichCandidates(MarketScanResult scanResult, FlipQueryOptions queryOptions, AccountSnapshot accountSnapshot, MarketScanResult previousScanResult, AccountSnapshot previousAccountSnapshot, IReadOnlyCollection<int> watchlistIds) {
        if (scanResult == null) {
            return;
        }

        var snapshot = accountSnapshot ?? new AccountSnapshot();
        var previousCandidates = previousScanResult?.Candidates?.ToDictionary(candidate => candidate.ItemId) ?? new Dictionary<int, FlipCandidate>();
        var previousOrders = previousAccountSnapshot?.OrderByItemId ?? new Dictionary<int, ItemOrderSnapshot>();
        var watchlist = new HashSet<int>(watchlistIds ?? Array.Empty<int>());

        foreach (var candidate in scanResult.Candidates) {
            candidate.AvailableCapitalCopper = snapshot.AvailableCopper;
            candidate.OwnedQuantity = snapshot.OwnedCounts.TryGetValue(candidate.ItemId, out var ownedQuantity) ? ownedQuantity : 0;

            if (snapshot.OrderByItemId.TryGetValue(candidate.ItemId, out var orderSnapshot)) {
                candidate.CurrentBuyOrderQuantity = orderSnapshot.CurrentBuyQuantity;
                candidate.CurrentSellOrderQuantity = orderSnapshot.CurrentSellQuantity;
            }

            candidate.InsightBadges.Clear();
            candidate.AlertMatches.Clear();
            candidate.AlertScore = 0m;

            if (candidate.FairValueWeightedCopper > 0 && candidate.AcquisitionCostCopper < candidate.FairValueWeightedCopper) {
                candidate.InsightBadges.Add("Below fair value");
            }

            if (candidate.TurnoverScore >= 0.80m) {
                candidate.InsightBadges.Add("High turnover");
            }

            if (candidate.VolatilityPercent >= 18m) {
                candidate.InsightBadges.Add("Depth unstable");
            }

            if (previousCandidates.TryGetValue(candidate.ItemId, out var previousCandidate) &&
                candidate.SpreadPercent + 4m < previousCandidate.SpreadPercent) {
                candidate.InsightBadges.Add("Spread collapsing");
            }

            ApplyAlerts(candidate, queryOptions, previousCandidates.TryGetValue(candidate.ItemId, out previousCandidate) ? previousCandidate : null, previousOrders.TryGetValue(candidate.ItemId, out var previousOrder) ? previousOrder : null, watchlist.Contains(candidate.ItemId));
            ApplyRecommendation(candidate, queryOptions);
            ApplyCraftCoverage(candidate, snapshot);
            ApplyAdvisorNarrative(candidate, previousCandidates.TryGetValue(candidate.ItemId, out previousCandidate) ? previousCandidate : null);
            ApplyExposure(candidate, snapshot, queryOptions);
        }
    }

    private static void ApplyCraftCoverage(FlipCandidate candidate, AccountSnapshot snapshot) {
        if (candidate.CraftIngredients == null || candidate.CraftIngredients.Count == 0) {
            return;
        }

        var craftableCount = int.MaxValue;
        var missingCost = 0;

        foreach (var ingredient in candidate.CraftIngredients) {
            var ownedCount = snapshot.OwnedCounts.TryGetValue(ingredient.ItemId, out var totalOwned) ? totalOwned : 0;
            ingredient.OwnedCount = ownedCount;
            ingredient.MissingCount = Math.Max(0, ingredient.RequiredCount - ownedCount);
            ingredient.MissingCostCopper = ingredient.MissingCount * ingredient.UnitBuyPriceCopper;
            missingCost += ingredient.MissingCostCopper;
            craftableCount = Math.Min(craftableCount, ingredient.RequiredCount <= 0 ? 0 : ownedCount / ingredient.RequiredCount);
        }

        candidate.MissingCraftCostCopper = missingCost;
        candidate.CraftFromOwnedCount = craftableCount == int.MaxValue ? 0 : craftableCount;
    }

    private static void ApplyAlerts(FlipCandidate candidate, FlipQueryOptions queryOptions, FlipCandidate previousCandidate, ItemOrderSnapshot previousOrder, bool isWatched) {
        var rules = queryOptions?.AlertRules ?? Array.Empty<AlertRule>();

        if (!isWatched && !(queryOptions?.WatchlistOnly ?? false)) {
            return;
        }

        foreach (var rule in rules.Where(rule => rule != null && rule.Enabled)) {
            switch (rule.Kind) {
                case AlertRuleKind.DiscountPercent:
                    if (candidate.DiscountPercent >= rule.Threshold) {
                        AddAlert(candidate, rule.Name, $"{candidate.DiscountPercent:N1}% below recent fair value.", candidate.DiscountPercent / Math.Max(1m, rule.Threshold));
                    }
                    break;
                case AlertRuleKind.SpreadPercent:
                    if (candidate.SpreadPercent >= rule.Threshold) {
                        AddAlert(candidate, rule.Name, $"{candidate.SpreadPercent:N1}% ROI meets sniper spread threshold.", candidate.SpreadPercent / Math.Max(1m, rule.Threshold));
                    }
                    break;
                case AlertRuleKind.DemandPressureJump:
                    if (previousCandidate != null && candidate.DemandPressure - previousCandidate.DemandPressure >= rule.Threshold) {
                        AddAlert(candidate, rule.Name, $"Demand pressure jumped from {previousCandidate.DemandPressure:N2} to {candidate.DemandPressure:N2}.", (candidate.DemandPressure - previousCandidate.DemandPressure) / Math.Max(0.10m, rule.Threshold));
                    }
                    break;
                case AlertRuleKind.OrderFillImprovement:
                    if (previousOrder != null && previousOrder.CurrentSellQuantity - candidate.CurrentSellOrderQuantity >= rule.Threshold) {
                        AddAlert(candidate, rule.Name, $"Open sell quantity dropped from {previousOrder.CurrentSellQuantity:N0} to {candidate.CurrentSellOrderQuantity:N0}.", (previousOrder.CurrentSellQuantity - candidate.CurrentSellOrderQuantity) / Math.Max(1m, rule.Threshold));
                    }
                    break;
            }
        }
    }

    private static void AddAlert(FlipCandidate candidate, string ruleName, string message, decimal severity) {
        var alert = new AlertMatch() {
            RuleName = ruleName,
            Message = message,
            Severity = Math.Round(Math.Max(0.10m, severity), 2)
        };
        candidate.AlertMatches.Add(alert);
        candidate.AlertScore += alert.Severity;
    }

    private static void ApplyRecommendation(FlipCandidate candidate, FlipQueryOptions queryOptions) {
        if (candidate.OpportunityMode == OpportunityMode.Craft && candidate.CraftIngredients.Count > 0 && candidate.CraftFromOwnedCount > 0 && candidate.MissingCraftCostCopper > candidate.EstimatedProfit) {
            candidate.RecommendationState = RecommendationState.CraftFromStockOnly;
            candidate.RecommendationNote = "Profitable mainly from owned materials; rebuying ingredients trims the edge too far.";
            return;
        }

        if (candidate.AvailableCapitalCopper > 0 && candidate.AcquisitionCostCopper > candidate.AvailableCapitalCopper) {
            candidate.RecommendationState = RecommendationState.Skip;
            candidate.RecommendationNote = "Buy-in is larger than liquid wallet capital.";
            return;
        }

        if (queryOptions.MaxOpenSellQuantity > 0 && candidate.CurrentSellOrderQuantity >= queryOptions.MaxOpenSellQuantity) {
            candidate.RecommendationState = RecommendationState.SellExisting;
            candidate.RecommendationNote = "You already have enough listed; clear existing sells before re-entering.";
            return;
        }

        if (queryOptions.MaxOwnedQuantity > 0 && candidate.OwnedQuantity >= queryOptions.MaxOwnedQuantity) {
            candidate.RecommendationState = RecommendationState.Hold;
            candidate.RecommendationNote = "Inventory exposure is already at or above the current cap.";
            return;
        }

        if (queryOptions.MaxVolatilityPercent > 0 && candidate.VolatilityPercent >= queryOptions.MaxVolatilityPercent) {
            candidate.RecommendationState = RecommendationState.Skip;
            candidate.RecommendationNote = "Recent volatility is above your preset tolerance.";
            return;
        }

        if (candidate.CurrentSellOrderQuantity > 0 && candidate.OwnedQuantity > candidate.CurrentSellOrderQuantity) {
            candidate.RecommendationState = RecommendationState.SellExisting;
            candidate.RecommendationNote = "You already own extra stock that can be exited before buying more.";
            return;
        }

        candidate.RecommendationState = RecommendationState.Buy;
        candidate.RecommendationNote = "Healthy spread, acceptable exposure, and enough wallet room to act.";
    }

    private static void ApplyAdvisorNarrative(FlipCandidate candidate, FlipCandidate previousCandidate) {
        var whyNow = new List<string>();
        var whyNot = new List<string>();

        if (candidate.SpreadPercent >= 10m) {
            whyNow.Add("strong spread");
        }

        if (candidate.ExpectedFillsPerDay >= 1.0m) {
            whyNow.Add("fast expected fills");
        }

        if (candidate.OpportunityMode == OpportunityMode.Value && candidate.DiscountPercent >= 6m) {
            whyNow.Add("below recent fair value");
        }

        if (candidate.MarketValuePercent < 85m) {
            whyNow.Add("cheap versus fair value");
        }

        if (candidate.OpportunityMode == OpportunityMode.Cooldown) {
            whyNow.Add("daily cooldown lane");
        }

        if (candidate.CraftFromOwnedCount > 0) {
            whyNow.Add("owned materials reduce rebuy cost");
        }

        if (candidate.VolatilityPercent >= 18m) {
            whyNot.Add("volatility is elevated");
        }

        if (candidate.MarketValuePercent > 120m) {
            whyNot.Add("current price is overheated");
        }

        if (candidate.CurrentSellOrderQuantity > 0) {
            whyNot.Add("you already have open sells");
        }

        if (candidate.OwnedQuantity > 0 && candidate.RecommendationState == RecommendationState.Hold) {
            whyNot.Add("inventory exposure is already high");
        }

        candidate.AdvisorWhyNow = whyNow.Count == 0
            ? $"This candidate is still one of the strongest combinations of margin, exit quality, and capital efficiency in the current scan at {candidate.MarketValuePercent:N0}% of fair value."
            : $"Why now: {string.Join(", ", whyNow)}. Current value is {candidate.MarketValuePercent:N0}% of fair.";
        candidate.AdvisorWhyNot = whyNot.Count == 0
            ? "Why not: main risk is routine TP undercutting and fee drag if the market softens."
            : $"Why not: {string.Join(", ", whyNot)}.";
        candidate.AdvisorWhatChanged = previousCandidate == null
            ? "What changed: this is new to your local scan history."
            : $"What changed: sell floor {FormatSignedCoin(candidate.SellDeltaCopper)}, demand pressure shifted from {previousCandidate.DemandPressure:N2} to {candidate.DemandPressure:N2}, and value moved from {previousCandidate.MarketValuePercent:N0}% to {candidate.MarketValuePercent:N0}% of fair.";
        candidate.AdvisorRiskNotes = candidate.VolatilityPercent >= 20m
            ? "Risk note: recent price action is unstable, so paper profit may vanish quickly."
            : $"Risk note: the main execution risk is getting undercut before the turnover thesis plays out. Current value band: {candidate.MarketValueLabel}.";
    }

    private static void ApplyExposure(FlipCandidate candidate, AccountSnapshot snapshot, FlipQueryOptions queryOptions) {
        var penalty = 1.00m;

        if (candidate.CurrentSellOrderQuantity > 0) {
            penalty *= 0.82m;
        }

        if (candidate.OwnedQuantity > 0) {
            penalty *= candidate.OwnedQuantity >= Math.Max(1, queryOptions?.MaxOwnedQuantity ?? 0)
                ? 0.65m
                : 0.88m;
        }

        if (candidate.AvailableCapitalCopper > 0 && candidate.AcquisitionCostCopper > candidate.AvailableCapitalCopper) {
            penalty *= 0.45m;
        }

        if (candidate.RecommendationState == RecommendationState.Skip) {
            penalty *= 0.35m;
        } else if (candidate.RecommendationState == RecommendationState.Hold) {
            penalty *= 0.60m;
        } else if (candidate.RecommendationState == RecommendationState.SellExisting) {
            penalty *= 0.78m;
        }

        candidate.ExposurePenaltyScore = Math.Round(Math.Max(0.15m, penalty), 2);
        candidate.ExpectedGoldPerDayCopper = (int)Math.Round(candidate.ExpectedGoldPerDayCopper * candidate.ExposurePenaltyScore);
        candidate.AdvisorScore = Math.Round(candidate.AdvisorScore * candidate.ExposurePenaltyScore, 2);
    }

    private static string FormatSignedCoin(int copper) {
        if (copper == 0) {
            return "0c";
        }

        var prefix = copper > 0 ? "+" : "-";
        var absolute = Math.Abs(copper);
        var gold = absolute / 10000;
        var silver = absolute / 100 % 100;
        var bronze = absolute % 100;
        return $"{prefix}{gold}g {silver:D2}s {bronze:D2}c";
    }
}
