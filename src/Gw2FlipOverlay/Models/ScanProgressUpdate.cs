namespace Gw2FlipOverlay.Models;

public sealed class ScanProgressUpdate {

    public string StatusMessage { get; set; } = string.Empty;

    public MarketScanResult PartialResult { get; set; }
}
