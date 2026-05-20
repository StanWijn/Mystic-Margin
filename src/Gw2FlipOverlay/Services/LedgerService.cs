using Gw2FlipOverlay.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Gw2FlipOverlay.Services;

public sealed class LedgerService {

    public IReadOnlyList<TransactionLedgerEntry> BuildEntries(AccountSnapshot snapshot, IReadOnlyDictionary<int, FlipCandidate> candidateMap) {
        var accountSnapshot = snapshot ?? new AccountSnapshot();
        var candidateLookup = candidateMap ?? new Dictionary<int, FlipCandidate>();
        var itemIds = new HashSet<int>(accountSnapshot.HistoricalBuys.Select(row => row.ItemId));

        foreach (var itemId in accountSnapshot.HistoricalSells.Select(row => row.ItemId)) {
            itemIds.Add(itemId);
        }

        foreach (var itemId in accountSnapshot.OwnedCounts.Keys) {
            itemIds.Add(itemId);
        }

        var entries = new List<TransactionLedgerEntry>();

        foreach (var itemId in itemIds) {
            var buys = accountSnapshot.HistoricalBuys.Where(row => row.ItemId == itemId).ToList();
            var sells = accountSnapshot.HistoricalSells.Where(row => row.ItemId == itemId).ToList();
            var boughtQuantity = buys.Sum(row => row.Quantity);
            var soldQuantity = sells.Sum(row => row.Quantity);
            var averageBuy = boughtQuantity <= 0 ? 0 : (int)Math.Round(buys.Sum(row => row.Price * row.Quantity) / (decimal)boughtQuantity);
            var averageSell = soldQuantity <= 0 ? 0 : (int)Math.Round(sells.Sum(row => row.Price * row.Quantity) / (decimal)soldQuantity);
            var heldQuantity = accountSnapshot.OwnedCounts.TryGetValue(itemId, out var owned) ? owned : Math.Max(0, boughtQuantity - soldQuantity);
            var realizedGross = sells.Sum(row => row.Price * row.Quantity) - (averageBuy * soldQuantity);
            var fees = sells.Sum(row => (int)Math.Floor(row.Price * row.Quantity * 0.15m));
            var currentSellFloor = candidateLookup.TryGetValue(itemId, out var candidate)
                ? candidate.LowestSell
                : averageSell;
            var unrealized = heldQuantity <= 0 || averageBuy <= 0 || currentSellFloor <= 0
                ? 0
                : (((int)Math.Floor(currentSellFloor * 0.85m)) - averageBuy) * heldQuantity;
            var orders = accountSnapshot.OrderByItemId.TryGetValue(itemId, out var itemOrders)
                ? itemOrders
                : new ItemOrderSnapshot() { ItemId = itemId };

            entries.Add(new TransactionLedgerEntry() {
                ItemId = itemId,
                ItemName = ResolveItemName(accountSnapshot, candidate, itemId),
                HeldQuantity = heldQuantity,
                BoughtQuantity = boughtQuantity,
                SoldQuantity = soldQuantity,
                AverageBuyPriceCopper = averageBuy,
                AverageSellPriceCopper = averageSell,
                FeesPaidCopper = Math.Max(0, fees),
                RealizedProfitCopper = realizedGross - Math.Max(0, fees),
                UnrealizedProfitCopper = unrealized,
                CurrentSellFloorCopper = currentSellFloor,
                CurrentOpenBuyQuantity = orders.CurrentBuyQuantity,
                CurrentOpenSellQuantity = orders.CurrentSellQuantity,
                MarketConfidenceScore = candidate?.ConfidenceScore ?? 0m,
                RecommendationState = candidate?.RecommendationState ?? RecommendationState.Hold,
                Notes = candidate?.RecommendationNote ?? "No active market candidate matched this item in the current scan universe.",
                LastBoughtUtc = buys.OrderByDescending(row => row.FulfilledAtUtc ?? row.CreatedAtUtc).FirstOrDefault()?.FulfilledAtUtc ?? buys.OrderByDescending(row => row.CreatedAtUtc).FirstOrDefault()?.CreatedAtUtc,
                LastSoldUtc = sells.OrderByDescending(row => row.FulfilledAtUtc ?? row.CreatedAtUtc).FirstOrDefault()?.FulfilledAtUtc ?? sells.OrderByDescending(row => row.CreatedAtUtc).FirstOrDefault()?.CreatedAtUtc
            });
        }

        return entries
            .OrderByDescending(entry => entry.RealizedProfitCopper + entry.UnrealizedProfitCopper)
            .ThenByDescending(entry => entry.HeldQuantity)
            .ToList();
    }

    private static string ResolveItemName(AccountSnapshot accountSnapshot, FlipCandidate candidate, int itemId) {
        if (!string.IsNullOrWhiteSpace(candidate?.ItemName)) {
            return candidate.ItemName;
        }

        if (accountSnapshot?.ItemNames?.TryGetValue(itemId, out var accountName) == true && !string.IsNullOrWhiteSpace(accountName)) {
            return accountName;
        }

        return $"Item {itemId}";
    }
}
