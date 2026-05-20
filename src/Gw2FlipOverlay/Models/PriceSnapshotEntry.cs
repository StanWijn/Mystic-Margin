using System;

namespace Gw2FlipOverlay.Models;

public sealed class PriceSnapshotEntry {

    public int ItemId { get; set; }

    public int HighestBuy { get; set; }

    public int LowestSell { get; set; }

    public int BuyQuantity { get; set; }

    public int SellQuantity { get; set; }

    public DateTimeOffset RecordedAtUtc { get; set; }
}
