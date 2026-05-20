namespace Gw2FlipOverlay.Models;

public sealed class AlertMatch {

    public string RuleName { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public decimal Severity { get; set; }
}
