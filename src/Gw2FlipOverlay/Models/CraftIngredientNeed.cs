namespace Gw2FlipOverlay.Models;

public sealed class CraftIngredientNeed {

    public int ItemId { get; set; }

    public string ItemName { get; set; } = string.Empty;

    public int RequiredCount { get; set; }

    public int OwnedCount { get; set; }

    public int MissingCount { get; set; }

    public int UnitBuyPriceCopper { get; set; }

    public int MissingCostCopper { get; set; }
}
