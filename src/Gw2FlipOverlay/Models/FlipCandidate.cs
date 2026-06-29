using System;
using System.Collections.Generic;

namespace Gw2FlipOverlay.Models;

public sealed class FlipCandidate {

    public int ItemId { get; set; }

    public string ItemName { get; set; } = string.Empty;

    public int HighestBuy { get; set; }

    public int LowestSell { get; set; }

    public int NetResaleValue { get; set; }

    public int EstimatedProfit { get; set; }

    public decimal SpreadPercent { get; set; }

    public int BuyStackSize { get; set; }

    public int SellStackSize { get; set; }

    public decimal LiquidityScore { get; set; }

    public decimal StabilityScore { get; set; }

    public decimal Score { get; set; }

    public string Source { get; set; } = string.Empty;

    public string ItemType { get; set; } = string.Empty;

    public string Rarity { get; set; } = string.Empty;

    public string IconUrl { get; set; } = string.Empty;

    public int BuyDeltaCopper { get; set; }

    public int SellDeltaCopper { get; set; }

    public DateTimeOffset? PreviousSeenUtc { get; set; }

    public int AcquisitionCostCopper { get; set; }

    public int MarketDepth { get; set; }

    public decimal VolumeScore { get; set; }

    public decimal TurnoverScore { get; set; }

    public decimal DemandPressure { get; set; }

    public decimal FastFlipScore { get; set; }

    public OpportunityMode OpportunityMode { get; set; }

    public int FairValueCopper { get; set; }

    public decimal DiscountPercent { get; set; }

    public decimal ValueScore { get; set; }

    public int HistoricalSampleCount { get; set; }

    public decimal ConfidenceScore { get; set; }

    public int OwnedQuantity { get; set; }

    public int CurrentBuyOrderQuantity { get; set; }

    public int CurrentSellOrderQuantity { get; set; }

    public int AvailableCapitalCopper { get; set; }

    public int FairValueWeightedCopper { get; set; }

    public int FairValueRecentMedianCopper { get; set; }

    public int MarketValueReferenceCopper { get; set; }

    public decimal MarketValuePercent { get; set; }

    public MarketValueBand MarketValueBand { get; set; }

    public string MarketValueLabel { get; set; } = string.Empty;

    public decimal VolatilityPercent { get; set; }

    public decimal SoldThroughConfidence { get; set; }

    public RecommendationState RecommendationState { get; set; }

    public string RecommendationNote { get; set; } = string.Empty;

    public decimal ExpectedFillsPerDay { get; set; }

    public int ExpectedGoldPerDayCopper { get; set; }

    public int BaseExpectedGoldPerDayCopper { get; set; }

    public decimal ExitQualityScore { get; set; }

    public decimal CapitalEfficiencyScore { get; set; }

    public decimal ExposurePenaltyScore { get; set; } = 1m;

    public decimal AdvisorScore { get; set; }

    public decimal BaseAdvisorScore { get; set; }

    public string AdvisorWhyNow { get; set; } = string.Empty;

    public string AdvisorWhyNot { get; set; } = string.Empty;

    public string AdvisorWhatChanged { get; set; } = string.Empty;

    public string AdvisorRiskNotes { get; set; } = string.Empty;

    public AdvisorStrategyTag StrategyTag { get; set; }

    public int InvestmentHorizonDays { get; set; }

    public string SeasonWindowState { get; set; } = string.Empty;

    public decimal AlertScore { get; set; }

    public List<string> InsightBadges { get; set; } = new List<string>();

    public List<AlertMatch> AlertMatches { get; set; } = new List<AlertMatch>();

    public List<PriceSnapshotEntry> PriceHistory { get; set; } = new List<PriceSnapshotEntry>();

    public List<CraftIngredientNeed> CraftIngredients { get; set; } = new List<CraftIngredientNeed>();

    public int MissingCraftCostCopper { get; set; }

    public int CraftFromOwnedCount { get; set; }
}
