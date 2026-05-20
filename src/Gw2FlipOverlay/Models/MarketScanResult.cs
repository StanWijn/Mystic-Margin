using System;
using System.Collections.Generic;

namespace Gw2FlipOverlay.Models;

public sealed class MarketScanResult {

    public IReadOnlyList<FlipCandidate> Candidates { get; set; } = Array.Empty<FlipCandidate>();

    public int TotalPriceRows { get; set; }

    public int SavedSnapshotCount { get; set; }

    public string SnapshotRootPath { get; set; } = string.Empty;

    public string SourceName { get; set; } = string.Empty;

    public DateTimeOffset GeneratedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public OpportunityMode OpportunityMode { get; set; }

    public int UniverseCandidateCount { get; set; }

    public int FilteredCandidateCount { get; set; }

    public string ActivePresetName { get; set; } = string.Empty;
}
