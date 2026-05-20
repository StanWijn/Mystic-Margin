namespace Gw2FlipOverlay.Models;

public sealed class PortfolioRow {

    public PortfolioRowKind Kind { get; set; }

    public int ItemId { get; set; }

    public string ItemName { get; set; } = string.Empty;

    public int Quantity { get; set; }

    public int UnitPriceCopper { get; set; }

    public int GrossValueCopper { get; set; }

    public int NetValueCopper { get; set; }

    public int FairValueCopper { get; set; }

    public decimal MarketValuePercent { get; set; }

    public string MarketValueLabel { get; set; } = string.Empty;

    public string Notes { get; set; } = string.Empty;
}
