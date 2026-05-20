using Gw2FlipOverlay.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Gw2FlipOverlay.Services;

public sealed class MockAccountDataProvider : IAccountDataProvider {

    public string SourceName => "mock account";

    public Task<AccountSnapshot> GetSnapshotAsync(string apiKey, CancellationToken cancellationToken) {
        var snapshot = new AccountSnapshot() {
            CapturedAtUtc = DateTimeOffset.UtcNow,
            HasApiKey = true,
            IsAuthenticated = true,
            StatusMessage = "Loaded mock wallet, holdings, orders, and TP history.",
            AvailableCopper = 523400
        };

        snapshot.ItemNames = new Dictionary<int, string>() {
            [19721] = "Glob of Ectoplasm",
            [24358] = "Orichalcum Ingot",
            [19697] = "Vicious Claw",
            [24502] = "Cured Hardened Leather Square",
            [24295] = "Bolt of Silk",
            [44941] = "Watchwork Sprocket"
        };

        snapshot.OwnedCounts = new Dictionary<int, int>() {
            [19721] = 85,
            [24358] = 160,
            [19697] = 48,
            [24502] = 22,
            [24295] = 110,
            [44941] = 40
        };

        snapshot.OrderByItemId = new Dictionary<int, ItemOrderSnapshot>() {
            [19721] = new ItemOrderSnapshot() { ItemId = 19721, CurrentBuyQuantity = 25, CurrentSellQuantity = 40, CurrentBuyTotalCopper = 46000, CurrentSellTotalCopper = 92440, CurrentBuyUnitPrice = 1840, CurrentSellUnitPrice = 2311 },
            [24358] = new ItemOrderSnapshot() { ItemId = 24358, CurrentBuyQuantity = 0, CurrentSellQuantity = 80, CurrentBuyTotalCopper = 0, CurrentSellTotalCopper = 38960, CurrentBuyUnitPrice = 0, CurrentSellUnitPrice = 487 },
            [24502] = new ItemOrderSnapshot() { ItemId = 24502, CurrentBuyQuantity = 12, CurrentSellQuantity = 18, CurrentBuyTotalCopper = 9120, CurrentSellTotalCopper = 17820, CurrentBuyUnitPrice = 760, CurrentSellUnitPrice = 990 }
        };

        snapshot.HistoricalBuys.Add(new CommerceTransactionRecord() { Id = 1, ItemId = 19721, Price = 1820, Quantity = 120, CreatedAtUtc = DateTimeOffset.UtcNow.AddDays(-3), FulfilledAtUtc = DateTimeOffset.UtcNow.AddDays(-3), IsSell = false });
        snapshot.HistoricalBuys.Add(new CommerceTransactionRecord() { Id = 2, ItemId = 24358, Price = 360, Quantity = 200, CreatedAtUtc = DateTimeOffset.UtcNow.AddDays(-2), FulfilledAtUtc = DateTimeOffset.UtcNow.AddDays(-2), IsSell = false });
        snapshot.HistoricalBuys.Add(new CommerceTransactionRecord() { Id = 3, ItemId = 24502, Price = 770, Quantity = 40, CreatedAtUtc = DateTimeOffset.UtcNow.AddDays(-1), FulfilledAtUtc = DateTimeOffset.UtcNow.AddDays(-1), IsSell = false });
        snapshot.HistoricalSells.Add(new CommerceTransactionRecord() { Id = 101, ItemId = 19721, Price = 2295, Quantity = 35, CreatedAtUtc = DateTimeOffset.UtcNow.AddDays(-1), FulfilledAtUtc = DateTimeOffset.UtcNow.AddDays(-1), IsSell = true });
        snapshot.HistoricalSells.Add(new CommerceTransactionRecord() { Id = 102, ItemId = 24358, Price = 482, Quantity = 60, CreatedAtUtc = DateTimeOffset.UtcNow.AddHours(-20), FulfilledAtUtc = DateTimeOffset.UtcNow.AddHours(-18), IsSell = true });
        snapshot.HistoricalSells.Add(new CommerceTransactionRecord() { Id = 103, ItemId = 24502, Price = 972, Quantity = 12, CreatedAtUtc = DateTimeOffset.UtcNow.AddHours(-10), FulfilledAtUtc = DateTimeOffset.UtcNow.AddHours(-7), IsSell = true });

        return Task.FromResult(snapshot);
    }
}
