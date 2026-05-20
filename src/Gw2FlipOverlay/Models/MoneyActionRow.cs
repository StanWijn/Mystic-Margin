namespace Gw2FlipOverlay.Models;

public sealed class MoneyActionRow {

    public int ItemId { get; set; }

    public string ItemName { get; set; } = string.Empty;

    public string Lane { get; set; } = string.Empty;

    public string Action { get; set; } = string.Empty;

    public int Quantity { get; set; }

    public int CapitalCopper { get; set; }

    public int TargetCopper { get; set; }

    public int EdgeCopper { get; set; }

    public decimal ConfidenceScore { get; set; }

    public string Notes { get; set; } = string.Empty;
}
