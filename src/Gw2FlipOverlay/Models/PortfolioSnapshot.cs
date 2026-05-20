using System;
using System.Collections.Generic;

namespace Gw2FlipOverlay.Models;

public sealed class PortfolioSnapshot {

    public DateTimeOffset CapturedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public PortfolioSummary Summary { get; set; } = new PortfolioSummary();

    public List<PortfolioTrendPoint> Trend { get; set; } = new List<PortfolioTrendPoint>();

    public List<PortfolioRow> Rows { get; set; } = new List<PortfolioRow>();

    public List<HistoricalInvestmentResult> HistoricalResults { get; set; } = new List<HistoricalInvestmentResult>();
}
