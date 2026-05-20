using System;

namespace Gw2FlipOverlay.Models;

public sealed class PortfolioTrendPoint {

    public DateTimeOffset CapturedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public int NetWorthCopper { get; set; }
}
