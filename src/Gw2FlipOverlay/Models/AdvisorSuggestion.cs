using System;
using System.Collections.Generic;

namespace Gw2FlipOverlay.Models;

public sealed class AdvisorSuggestion {

    public AdvisorSection Section { get; set; }

    public AdvisorActionType Action { get; set; }

    public AdvisorStrategyTag StrategyTag { get; set; }

    public int ItemId { get; set; }

    public string ItemName { get; set; } = string.Empty;

    public OpportunityMode OpportunityMode { get; set; }

    public RecommendationState RecommendationState { get; set; }

    public decimal ConfidenceScore { get; set; }

    public int EstimatedProfitCopper { get; set; }

    public int ExpectedGoldPerDayCopper { get; set; }

    public decimal ExpectedFillsPerDay { get; set; }

    public int CapitalRequiredCopper { get; set; }

    public decimal MarketValuePercent { get; set; }

    public string MarketValueLabel { get; set; } = string.Empty;

    public int PortfolioImpactCopper { get; set; }

    public string LiquidityImpactLabel { get; set; } = string.Empty;

    public string BriefReason { get; set; } = string.Empty;

    public string RiskNotes { get; set; } = string.Empty;

    public string WhyNow { get; set; } = string.Empty;

    public string WhyNot { get; set; } = string.Empty;

    public string WhatChanged { get; set; } = string.Empty;

    public bool UsesOwnedMaterials { get; set; }

    public bool UsesWalletCapital { get; set; }

    public bool UsesOpenOrders { get; set; }

    public string SeasonWindowState { get; set; } = string.Empty;

    public int InvestmentHorizonDays { get; set; }

    public decimal AdvisorScore { get; set; }

    public List<PriceSnapshotEntry> PriceHistory { get; set; } = new List<PriceSnapshotEntry>();

    public DateTimeOffset GeneratedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
