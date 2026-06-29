using System;
using System.Collections.Generic;

namespace Gw2FlipOverlay.Models;

public sealed class AccountSnapshot {

    public DateTimeOffset CapturedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public bool HasApiKey { get; set; }

    public bool IsAuthenticated { get; set; }

    public string StatusMessage { get; set; } = string.Empty;

    public int AvailableCopper { get; set; }

    public Dictionary<int, int> OwnedCounts { get; set; } = new Dictionary<int, int>();

    public Dictionary<int, string> ItemNames { get; set; } = new Dictionary<int, string>();

    public Dictionary<int, ItemOrderSnapshot> OrderByItemId { get; set; } = new Dictionary<int, ItemOrderSnapshot>();

    public List<CommerceTransactionRecord> HistoricalBuys { get; set; } = new List<CommerceTransactionRecord>();

    public List<CommerceTransactionRecord> HistoricalSells { get; set; } = new List<CommerceTransactionRecord>();
}

public sealed class ItemOrderSnapshot {

    public int ItemId { get; set; }

    public int CurrentBuyQuantity { get; set; }

    public int CurrentSellQuantity { get; set; }

    public long CurrentBuyTotalCopper { get; set; }

    public long CurrentSellTotalCopper { get; set; }

    public int CurrentBuyUnitPrice { get; set; }

    public int CurrentSellUnitPrice { get; set; }

    public DateTimeOffset? CurrentBuyOldestCreatedUtc { get; set; }

    public DateTimeOffset? CurrentSellOldestCreatedUtc { get; set; }
}

public sealed class CommerceTransactionRecord {

    public long Id { get; set; }

    public int ItemId { get; set; }

    public int Price { get; set; }

    public int Quantity { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset? FulfilledAtUtc { get; set; }

    public bool IsSell { get; set; }

    public bool IsCurrent { get; set; }
}
