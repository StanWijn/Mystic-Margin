using Gw2FlipOverlay.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Gw2FlipOverlay.Services;

public sealed class AdvisorService {

    private static readonly IReadOnlyList<SeasonalStrategyDefinition> SeasonalDefinitions = new[] {
        new SeasonalStrategyDefinition("Super Adventure Festival", 3, 28, 4, 18, new[] { "Super ", "Bauble", "Moto", "Continue Coin" }),
        new SeasonalStrategyDefinition("Dragon Bash", 6, 15, 7, 10, new[] { "Jorbreaker", "Dragon Coffer", "Holographic", "Dragon Bash" }),
        new SeasonalStrategyDefinition("Festival of the Four Winds", 7, 25, 8, 28, new[] { "Zephyrite", "Queen's Gauntlet", "Festival Token" }),
        new SeasonalStrategyDefinition("Halloween", 10, 10, 11, 10, new[] { "Candy Corn", "Trick-or-Treat", "Plastic " }),
        new SeasonalStrategyDefinition("Wintersday", 12, 1, 1, 12, new[] { "Wintersday", "Snowflake", "Toy-Shell", "Watchwork" }),
        new SeasonalStrategyDefinition("Lunar New Year", 1, 20, 3, 15, new[] { "Lucky Envelope", "Red Lantern", "Firecracker", "Lucky Red Bag" })
    };

    public AdvisorBriefing BuildBriefing(
        IReadOnlyDictionary<OpportunityMode, MarketScanResult> scanResultsByMode,
        AccountSnapshot accountSnapshot,
        PortfolioSummary portfolioSummary,
        AdvisorBriefing previousBriefing,
        DateTimeOffset nowUtc) {
        var results = scanResultsByMode ?? new Dictionary<OpportunityMode, MarketScanResult>();
        var account = accountSnapshot ?? new AccountSnapshot();
        var briefing = new AdvisorBriefing() {
            GeneratedAtUtc = nowUtc,
            PortfolioSummary = portfolioSummary ?? new PortfolioSummary()
        };

        var dailySuggestions = BuildDailySuggestions(results, account, nowUtc);
        var exitActions = BuildExitSuggestions(results, account, nowUtc);
        var cooldownPicks = BuildSectionSuggestions(results, OpportunityMode.Cooldown, AdvisorSection.CooldownPicks, account, nowUtc, 3);
        var investmentWatch = BuildInvestmentSuggestions(results, account, nowUtc);

        briefing.DailySuggestions.AddRange(dailySuggestions);
        briefing.ExitActions.AddRange(exitActions);
        briefing.CooldownPicks.AddRange(cooldownPicks);
        briefing.InvestmentWatch.AddRange(investmentWatch);
        briefing.FastFlipLane = BuildLaneSummary(results, OpportunityMode.Flip, "Fast flip", nowUtc);
        briefing.CooldownLane = BuildLaneSummary(results, OpportunityMode.Cooldown, "Cooldown", nowUtc);
        briefing.InvestmentLane = BuildLaneSummary(results, OpportunityMode.Investment, "Investment", nowUtc);
        briefing.WalletPlan = BuildWalletPlan(account, dailySuggestions, cooldownPicks, investmentWatch);
        briefing.Summary = BuildSummary(dailySuggestions, exitActions, cooldownPicks, investmentWatch, account, briefing.PortfolioSummary);
        briefing.DigestLines.AddRange(BuildDigest(previousBriefing, briefing));

        if (briefing.DigestLines.Count == 0) {
            briefing.DigestLines.Add("Advisor digest is stable. Scan again after fresh market movement to catch new rotations.");
        }

        return briefing;
    }

    private static List<AdvisorSuggestion> BuildDailySuggestions(IReadOnlyDictionary<OpportunityMode, MarketScanResult> results, AccountSnapshot account, DateTimeOffset nowUtc) {
        var immediate = new List<AdvisorSuggestion>();
        immediate.AddRange(BuildSectionSuggestions(results, OpportunityMode.Flip, AdvisorSection.DailyBriefing, account, nowUtc, 2));
        immediate.AddRange(BuildSectionSuggestions(results, OpportunityMode.Craft, AdvisorSection.DailyBriefing, account, nowUtc, 2));
        immediate.AddRange(BuildSectionSuggestions(results, OpportunityMode.Value, AdvisorSection.DailyBriefing, account, nowUtc, 2));

        return immediate
            .GroupBy(suggestion => suggestion.ItemId)
            .Select(group => group.OrderByDescending(suggestion => suggestion.AdvisorScore).First())
            .OrderByDescending(suggestion => suggestion.AdvisorScore)
            .Take(5)
            .ToList();
    }

    private static List<AdvisorSuggestion> BuildExitSuggestions(IReadOnlyDictionary<OpportunityMode, MarketScanResult> results, AccountSnapshot account, DateTimeOffset nowUtc) {
        var candidates = results.Values
            .SelectMany(result => result?.Candidates ?? Array.Empty<FlipCandidate>())
            .Where(candidate => candidate.OwnedQuantity > 0 || candidate.CurrentSellOrderQuantity > 0)
            .OrderByDescending(candidate => candidate.CurrentSellOrderQuantity + candidate.OwnedQuantity)
            .ThenByDescending(candidate => candidate.ExpectedGoldPerDayCopper)
            .Take(8)
            .ToList();

        var suggestions = new List<AdvisorSuggestion>();

        foreach (var candidate in candidates) {
            var action = candidate.RecommendationState == RecommendationState.SellExisting
                ? AdvisorActionType.Sell
                : AdvisorActionType.Hold;
            suggestions.Add(CreateSuggestion(AdvisorSection.ExitActions, action, candidate, nowUtc, account));
        }

        return suggestions
            .OrderByDescending(suggestion => suggestion.Action == AdvisorActionType.Sell ? 1 : 0)
            .ThenByDescending(suggestion => suggestion.AdvisorScore)
            .Take(4)
            .ToList();
    }

    private static List<AdvisorSuggestion> BuildInvestmentSuggestions(IReadOnlyDictionary<OpportunityMode, MarketScanResult> results, AccountSnapshot account, DateTimeOffset nowUtc) {
        var suggestions = BuildSectionSuggestions(results, OpportunityMode.Investment, AdvisorSection.InvestmentWatch, account, nowUtc, 6);
        return suggestions
            .OrderByDescending(suggestion => suggestion.Action == AdvisorActionType.Accumulate ? 1 : 0)
            .ThenByDescending(suggestion => suggestion.AdvisorScore)
            .Take(4)
            .ToList();
    }

    private static List<AdvisorSuggestion> BuildSectionSuggestions(IReadOnlyDictionary<OpportunityMode, MarketScanResult> results, OpportunityMode mode, AdvisorSection section, AccountSnapshot account, DateTimeOffset nowUtc, int takeCount) {
        if (!results.TryGetValue(mode, out var scanResult) || scanResult?.Candidates == null) {
            return new List<AdvisorSuggestion>();
        }

        return scanResult.Candidates
            .Select(candidate => CreateSuggestion(section, ChooseAction(candidate), candidate, nowUtc, account))
            .OrderByDescending(suggestion => suggestion.AdvisorScore)
            .Take(takeCount)
            .ToList();
    }

    private static AdvisorSuggestion CreateSuggestion(AdvisorSection section, AdvisorActionType action, FlipCandidate candidate, DateTimeOffset nowUtc, AccountSnapshot account) {
        var seasonState = ResolveSeasonWindow(candidate.ItemName, nowUtc);
        var advisorScore = Math.Round(
            candidate.AdvisorScore +
            (candidate.CurrentSellOrderQuantity > 0 ? 8m : 0m) +
            (candidate.OwnedQuantity > 0 ? 6m : 0m) +
            (section == AdvisorSection.DailyBriefing ? 10m : 0m) +
            (section == AdvisorSection.CooldownPicks ? 12m : 0m),
            2);

        return new AdvisorSuggestion() {
            Section = section,
            Action = action,
            StrategyTag = candidate.StrategyTag,
            ItemId = candidate.ItemId,
            ItemName = candidate.ItemName,
            OpportunityMode = candidate.OpportunityMode,
            RecommendationState = candidate.RecommendationState,
            ConfidenceScore = candidate.ConfidenceScore,
            EstimatedProfitCopper = candidate.EstimatedProfit,
            ExpectedGoldPerDayCopper = candidate.ExpectedGoldPerDayCopper,
            ExpectedFillsPerDay = candidate.ExpectedFillsPerDay,
            CapitalRequiredCopper = candidate.AcquisitionCostCopper,
            MarketValuePercent = candidate.MarketValuePercent,
            MarketValueLabel = candidate.MarketValueLabel,
            PortfolioImpactCopper = action == AdvisorActionType.Sell
                ? candidate.EstimatedProfit
                : -candidate.AcquisitionCostCopper,
            LiquidityImpactLabel = BuildLiquidityImpactLabel(action),
            BriefReason = BuildBriefReason(candidate, action, seasonState),
            RiskNotes = candidate.AdvisorRiskNotes,
            WhyNow = candidate.AdvisorWhyNow,
            WhyNot = candidate.AdvisorWhyNot,
            WhatChanged = candidate.AdvisorWhatChanged,
            UsesOwnedMaterials = candidate.CraftFromOwnedCount > 0,
            UsesWalletCapital = candidate.AcquisitionCostCopper > 0 && account.AvailableCopper >= candidate.AcquisitionCostCopper,
            UsesOpenOrders = candidate.CurrentBuyOrderQuantity > 0 || candidate.CurrentSellOrderQuantity > 0,
            SeasonWindowState = string.IsNullOrWhiteSpace(candidate.SeasonWindowState) ? seasonState : candidate.SeasonWindowState,
            InvestmentHorizonDays = candidate.InvestmentHorizonDays,
            AdvisorScore = advisorScore,
            PriceHistory = candidate.PriceHistory?.ToList() ?? new List<PriceSnapshotEntry>(),
            GeneratedAtUtc = nowUtc
        };
    }

    private static AdvisorActionType ChooseAction(FlipCandidate candidate) {
        return candidate.OpportunityMode switch {
            OpportunityMode.Craft => AdvisorActionType.Craft,
            OpportunityMode.Cooldown => AdvisorActionType.Craft,
            OpportunityMode.Investment => candidate.DiscountPercent >= 3m ? AdvisorActionType.Accumulate : AdvisorActionType.Hold,
            _ => candidate.RecommendationState switch {
                RecommendationState.SellExisting => AdvisorActionType.Sell,
                RecommendationState.Hold => AdvisorActionType.Hold,
                RecommendationState.Skip => AdvisorActionType.Skip,
                _ => AdvisorActionType.Buy
            }
        };
    }

    private static string BuildLaneSummary(IReadOnlyDictionary<OpportunityMode, MarketScanResult> results, OpportunityMode mode, string laneLabel, DateTimeOffset nowUtc) {
        if (!results.TryGetValue(mode, out var scanResult) || scanResult?.Candidates == null || scanResult.Candidates.Count == 0) {
            return $"{laneLabel} lane has no fresh candidates yet. Run a scan in this mode to populate the advisor.";
        }

        var top = scanResult.Candidates
            .OrderByDescending(candidate => candidate.AdvisorScore)
            .ThenByDescending(candidate => candidate.ExpectedGoldPerDayCopper)
            .First();

        return string.Format(
            CultureInfo.InvariantCulture,
            "{0} lane: {1} leads with {2} expected daily gold, {3:N1}% ROI, and {4:N0}% market value. {5}",
            laneLabel,
            top.ItemName,
            FormatCoin(top.ExpectedGoldPerDayCopper),
            top.SpreadPercent,
            top.MarketValuePercent,
            ResolveSeasonWindow(top.ItemName, nowUtc));
    }

    private static string BuildWalletPlan(AccountSnapshot account, IReadOnlyList<AdvisorSuggestion> daily, IReadOnlyList<AdvisorSuggestion> cooldowns, IReadOnlyList<AdvisorSuggestion> investments) {
        var wallet = Math.Max(0, account?.AvailableCopper ?? 0);
        var fastAllocation = (int)Math.Round(wallet * 0.55m);
        var cooldownAllocation = (int)Math.Round(wallet * 0.20m);
        var investmentAllocation = Math.Max(0, wallet - fastAllocation - cooldownAllocation);
        var dailyLabel = daily.Count > 0 ? daily[0].ItemName : "flip pool";
        var cooldownLabel = cooldowns.Count > 0 ? cooldowns[0].ItemName : "cooldown craft";
        var investmentLabel = investments.Count > 0 ? investments[0].ItemName : "seasonal watch";

        return $"Wallet plan: keep about {FormatCoin(fastAllocation)} liquid for {dailyLabel}, {FormatCoin(cooldownAllocation)} for {cooldownLabel}, and {FormatCoin(investmentAllocation)} for {investmentLabel}.";
    }

    private static string BuildSummary(IReadOnlyList<AdvisorSuggestion> daily, IReadOnlyList<AdvisorSuggestion> exits, IReadOnlyList<AdvisorSuggestion> cooldowns, IReadOnlyList<AdvisorSuggestion> investments, AccountSnapshot account, PortfolioSummary portfolioSummary) {
        var top = daily.FirstOrDefault();
        var exit = exits.FirstOrDefault();
        var cooldown = cooldowns.FirstOrDefault();
        var investment = investments.FirstOrDefault();
        var wallet = account?.AvailableCopper ?? 0;
        var netWorth = portfolioSummary?.NetWorthCopper ?? 0;

        return $"Today's desk favors {(top?.ItemName ?? "no immediate pick")} for active gold, {(cooldown?.ItemName ?? "no cooldown craft")} for daily value, {(investment?.ItemName ?? "no seasonal watch")} for medium holds, with wallet at {FormatCoin(wallet)}, net worth at {FormatCoin(netWorth)}, and {(exit?.ItemName ?? "no urgent exits")} as the main exit review.";
    }

    private static IReadOnlyList<string> BuildDigest(AdvisorBriefing previous, AdvisorBriefing current) {
        var lines = new List<string>();
        var previousTop = previous?.DailySuggestions?.FirstOrDefault();
        var currentTop = current?.DailySuggestions?.FirstOrDefault();

        if (currentTop != null && (previousTop == null || previousTop.ItemId != currentTop.ItemId)) {
            lines.Add($"New top pick: {currentTop.ItemName} replaced {(previousTop?.ItemName ?? "the prior board")} in the daily briefing.");
        } else if (currentTop != null && previousTop != null && currentTop.ExpectedGoldPerDayCopper > previousTop.ExpectedGoldPerDayCopper) {
            lines.Add($"{currentTop.ItemName} is strengthening with better expected daily gold than the last briefing.");
        }

        var previousExit = previous?.ExitActions?.FirstOrDefault(suggestion => suggestion.Action == AdvisorActionType.Sell);
        var currentExit = current?.ExitActions?.FirstOrDefault(suggestion => suggestion.Action == AdvisorActionType.Sell);

        if (currentExit != null && (previousExit == null || previousExit.ItemId != currentExit.ItemId)) {
            lines.Add($"Exit window improving on {currentExit.ItemName}; the advisor now prefers selling stock there first.");
        }

        var previousInvestment = previous?.InvestmentWatch?.FirstOrDefault();
        var currentInvestment = current?.InvestmentWatch?.FirstOrDefault();

        if (currentInvestment != null &&
            !string.Equals(currentInvestment.SeasonWindowState, previousInvestment?.SeasonWindowState, StringComparison.OrdinalIgnoreCase)) {
            lines.Add($"{currentInvestment.ItemName} moved into a new seasonal state: {currentInvestment.SeasonWindowState}.");
        }

        return lines;
    }

    private static string BuildBriefReason(FlipCandidate candidate, AdvisorActionType action, string seasonState) {
        if (!string.IsNullOrWhiteSpace(candidate.AdvisorWhyNow)) {
            return candidate.AdvisorWhyNow;
        }

        return action switch {
            AdvisorActionType.Craft => candidate.CraftFromOwnedCount > 0
                ? $"Profitable now because owned materials cover a real chunk of the recipe and the craft sits at {candidate.MarketValuePercent:N0}% of fair value."
                : $"Craft margin survives fees and still clears a practical daily return at {candidate.MarketValuePercent:N0}% of fair value.",
            AdvisorActionType.Sell => $"Open sells or held stock are already in place, so the cleanest gold comes from exiting inventory first while value is {candidate.MarketValuePercent:N0}% of fair.",
            AdvisorActionType.Accumulate => $"Seasonal timing looks favorable and the current price sits at {candidate.MarketValuePercent:N0}% of fair value. {seasonState}",
            AdvisorActionType.Skip => $"The spread exists on paper, but exposure or exit risk makes re-entry unattractive at {candidate.MarketValuePercent:N0}% of fair value.",
            AdvisorActionType.Hold => $"The setup is worth tracking, but the current window is better for patience than fresh buying at {candidate.MarketValuePercent:N0}% of fair value.",
            _ => $"Strong spread plus turnover keeps the edge actionable instead of just theoretical, with value at {candidate.MarketValuePercent:N0}% of fair."
        };
    }

    private static string BuildLiquidityImpactLabel(AdvisorActionType action) {
        return action switch {
            AdvisorActionType.Buy => "Uses liquidity",
            AdvisorActionType.Craft => "Uses capital and materials",
            AdvisorActionType.Accumulate => "Ties up capital",
            AdvisorActionType.Sell => "Improves liquidity",
            AdvisorActionType.Hold => "Neutral liquidity",
            _ => "Avoids new exposure"
        };
    }

    private static string ResolveSeasonWindow(string itemName, DateTimeOffset nowUtc) {
        foreach (var definition in SeasonalDefinitions) {
            if (!definition.Matches(itemName)) {
                continue;
            }

            return definition.IsActive(nowUtc)
                ? $"Active seasonal window: {definition.Name}"
                : $"Off-season watch: {definition.Name}";
        }

        return "No special seasonal timing";
    }

    private static string FormatCoin(int copper) {
        var absValue = Math.Abs(copper);
        var gold = absValue / 10000;
        var silver = absValue / 100 % 100;
        var bronze = absValue % 100;
        var prefix = copper < 0 ? "-" : string.Empty;
        return $"{prefix}{gold}g {silver:D2}s {bronze:D2}c";
    }

    private sealed class SeasonalStrategyDefinition {

        private readonly string[] _keywords;

        public SeasonalStrategyDefinition(string name, int startMonth, int startDay, int endMonth, int endDay, string[] keywords) {
            Name = name;
            StartMonth = startMonth;
            StartDay = startDay;
            EndMonth = endMonth;
            EndDay = endDay;
            _keywords = keywords ?? Array.Empty<string>();
        }

        public string Name { get; }

        public int StartMonth { get; }

        public int StartDay { get; }

        public int EndMonth { get; }

        public int EndDay { get; }

        public bool Matches(string itemName) {
            if (string.IsNullOrWhiteSpace(itemName)) {
                return false;
            }

            return _keywords.Any(keyword => itemName.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        public bool IsActive(DateTimeOffset timestamp) {
            var start = new DateTimeOffset(timestamp.Year, StartMonth, StartDay, 0, 0, 0, timestamp.Offset);
            var endYear = EndMonth < StartMonth ? timestamp.Year + 1 : timestamp.Year;
            var end = new DateTimeOffset(endYear, EndMonth, EndDay, 23, 59, 59, timestamp.Offset);
            var probe = timestamp;

            if (EndMonth < StartMonth && timestamp.Month < StartMonth) {
                probe = timestamp.AddYears(1);
            }

            return probe >= start && probe <= end;
        }
    }
}
