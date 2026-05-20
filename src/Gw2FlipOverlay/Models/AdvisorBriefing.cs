using System;
using System.Collections.Generic;

namespace Gw2FlipOverlay.Models;

public sealed class AdvisorBriefing {

    public DateTimeOffset GeneratedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public string Summary { get; set; } = string.Empty;

    public string FastFlipLane { get; set; } = string.Empty;

    public string CooldownLane { get; set; } = string.Empty;

    public string InvestmentLane { get; set; } = string.Empty;

    public string WalletPlan { get; set; } = string.Empty;

    public PortfolioSummary PortfolioSummary { get; set; } = new PortfolioSummary();

    public List<string> DigestLines { get; set; } = new List<string>();

    public List<AdvisorSuggestion> DailySuggestions { get; set; } = new List<AdvisorSuggestion>();

    public List<AdvisorSuggestion> ExitActions { get; set; } = new List<AdvisorSuggestion>();

    public List<AdvisorSuggestion> CooldownPicks { get; set; } = new List<AdvisorSuggestion>();

    public List<AdvisorSuggestion> InvestmentWatch { get; set; } = new List<AdvisorSuggestion>();
}
