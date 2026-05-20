using Gw2FlipOverlay.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Gw2FlipOverlay.Services;

public sealed class PortfolioService {

    public PortfolioSnapshot BuildSnapshot(
        AccountSnapshot accountSnapshot,
        IReadOnlyDictionary<int, FlipCandidate> candidateMap,
        IReadOnlyList<TransactionLedgerEntry> ledgerEntries,
        IReadOnlyList<PortfolioSnapshot> history,
        DateTimeOffset capturedAtUtc) {
        var account = accountSnapshot ?? new AccountSnapshot();
        var candidates = candidateMap ?? new Dictionary<int, FlipCandidate>();
        var ledger = ledgerEntries ?? Array.Empty<TransactionLedgerEntry>();
        var rows = new List<PortfolioRow>();
        var outstandingBuyCopper = 0;
        var outstandingSellGrossCopper = 0;
        var outstandingSellNetCopper = 0;
        var holdingsValueCopper = 0;

        foreach (var pair in account.OrderByItemId.OrderBy(entry => entry.Key)) {
            var itemId = pair.Key;
            var order = pair.Value ?? new ItemOrderSnapshot() { ItemId = itemId };
            candidates.TryGetValue(itemId, out var candidate);
            var itemName = ResolveItemName(account, candidate, itemId);
            var fairValue = candidate?.FairValueWeightedCopper ?? 0;

            if (order.CurrentBuyQuantity > 0 && order.CurrentBuyUnitPrice > 0) {
                var gross = GetOrderGrossValue(order.CurrentBuyQuantity, order.CurrentBuyUnitPrice, order.CurrentBuyTotalCopper);
                var marketValue = MarketValueHelper.Calculate(order.CurrentBuyUnitPrice, fairValue);
                outstandingBuyCopper += gross;
                rows.Add(new PortfolioRow() {
                    Kind = PortfolioRowKind.OpenBuy,
                    ItemId = itemId,
                    ItemName = itemName,
                    Quantity = order.CurrentBuyQuantity,
                    UnitPriceCopper = order.CurrentBuyUnitPrice,
                    GrossValueCopper = gross,
                    NetValueCopper = gross,
                    FairValueCopper = fairValue,
                    MarketValuePercent = marketValue.Percent,
                    MarketValueLabel = $"{marketValue.Label} vs fair buy",
                    Notes = "Capital locked in active buy orders."
                });
            }

            if (order.CurrentSellQuantity > 0 && order.CurrentSellUnitPrice > 0) {
                var gross = GetOrderGrossValue(order.CurrentSellQuantity, order.CurrentSellUnitPrice, order.CurrentSellTotalCopper);
                var net = (int)Math.Floor(gross * 0.85m);
                var marketValue = MarketValueHelper.Calculate(order.CurrentSellUnitPrice, fairValue);
                outstandingSellGrossCopper += gross;
                outstandingSellNetCopper += net;
                rows.Add(new PortfolioRow() {
                    Kind = PortfolioRowKind.OpenSell,
                    ItemId = itemId,
                    ItemName = itemName,
                    Quantity = order.CurrentSellQuantity,
                    UnitPriceCopper = order.CurrentSellUnitPrice,
                    GrossValueCopper = gross,
                    NetValueCopper = net,
                    FairValueCopper = fairValue,
                    MarketValuePercent = marketValue.Percent,
                    MarketValueLabel = $"{marketValue.Label} vs fair listing",
                    Notes = "Expected net proceeds from current sell listings."
                });
            }
        }

        foreach (var pair in account.OwnedCounts.OrderBy(entry => entry.Key)) {
            var itemId = pair.Key;
            var quantity = pair.Value;

            if (quantity <= 0) {
                continue;
            }

            candidates.TryGetValue(itemId, out var candidate);
            var itemName = ResolveItemName(account, candidate, itemId);
            var currentNetFloor = candidate?.LowestSell > 0
                ? (int)Math.Floor(candidate.LowestSell * 0.85m)
                : 0;
            var fairNet = candidate?.FairValueWeightedCopper > 0
                ? (int)Math.Floor(candidate.FairValueWeightedCopper * 0.85m)
                : 0;
            var conservativeUnit = ConservativeUnitValue(currentNetFloor, fairNet);
            var netValue = conservativeUnit * quantity;
            var marketValue = MarketValueHelper.Calculate(conservativeUnit, fairNet > 0 ? fairNet : candidate?.FairValueWeightedCopper ?? 0);
            holdingsValueCopper += netValue;
            rows.Add(new PortfolioRow() {
                Kind = PortfolioRowKind.Holding,
                ItemId = itemId,
                ItemName = itemName,
                Quantity = quantity,
                UnitPriceCopper = conservativeUnit,
                GrossValueCopper = conservativeUnit * quantity,
                NetValueCopper = netValue,
                FairValueCopper = candidate?.FairValueWeightedCopper ?? 0,
                MarketValuePercent = marketValue.Percent,
                MarketValueLabel = $"{marketValue.Label} vs fair holding",
                Notes = "Conservative mark-to-market value for held inventory."
            });
        }

        var summary = new PortfolioSummary() {
            IsAuthenticated = account.IsAuthenticated,
            WalletCopper = account.AvailableCopper,
            OutstandingBuyCopper = outstandingBuyCopper,
            OutstandingSellGrossCopper = outstandingSellGrossCopper,
            OutstandingSellNetCopper = outstandingSellNetCopper,
            HoldingsValueCopper = holdingsValueCopper,
            NetWorthCopper = account.AvailableCopper + outstandingBuyCopper + outstandingSellNetCopper + holdingsValueCopper,
            RealizedProfitCopper = ledger.Sum(entry => entry.RealizedProfitCopper),
            UnrealizedProfitCopper = ledger.Sum(entry => entry.UnrealizedProfitCopper),
            StatusMessage = account.IsAuthenticated
                ? "Portfolio view is using authenticated wallet, open orders, and holdings."
                : "Portfolio view is partial because no authenticated API key is available."
        };

        summary.DailyDeltaCopper = FindDelta(summary.NetWorthCopper, history, capturedAtUtc.AddDays(-1));
        summary.WeeklyDeltaCopper = FindDelta(summary.NetWorthCopper, history, capturedAtUtc.AddDays(-7));
        summary.MonthlyDeltaCopper = FindDelta(summary.NetWorthCopper, history, capturedAtUtc.AddDays(-30));

        var trend = history?
            .Select(snapshot => new PortfolioTrendPoint() {
                CapturedAtUtc = snapshot.CapturedAtUtc,
                NetWorthCopper = snapshot.Summary?.NetWorthCopper ?? 0
            })
            .OrderBy(point => point.CapturedAtUtc)
            .ToList() ?? new List<PortfolioTrendPoint>();

        trend.Add(new PortfolioTrendPoint() {
            CapturedAtUtc = capturedAtUtc,
            NetWorthCopper = summary.NetWorthCopper
        });

        return new PortfolioSnapshot() {
            CapturedAtUtc = capturedAtUtc,
            Summary = summary,
            Trend = trend
                .GroupBy(point => point.CapturedAtUtc)
                .Select(group => group.Last())
                .OrderBy(point => point.CapturedAtUtc)
                .ToList(),
            HistoricalResults = BuildHistoricalResults(ledger),
            Rows = rows
                .OrderByDescending(row => row.NetValueCopper)
                .ThenBy(row => row.ItemName)
                .ToList()
        };
    }

    private static int ConservativeUnitValue(int currentNetFloorCopper, int fairNetCopper) {
        if (currentNetFloorCopper > 0 && fairNetCopper > 0) {
            return Math.Min(currentNetFloorCopper, fairNetCopper);
        }

        return Math.Max(currentNetFloorCopper, fairNetCopper);
    }

    private static int GetOrderGrossValue(int quantity, int fallbackUnitPriceCopper, long totalCopper) {
        if (quantity <= 0) {
            return 0;
        }

        if (totalCopper > 0) {
            return (int)Math.Min(int.MaxValue, totalCopper);
        }

        return quantity * fallbackUnitPriceCopper;
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

    private static int FindDelta(int currentNetWorthCopper, IReadOnlyList<PortfolioSnapshot> history, DateTimeOffset thresholdUtc) {
        if (history == null || history.Count == 0) {
            return 0;
        }

        var baseline = history
            .Where(snapshot => snapshot.CapturedAtUtc <= thresholdUtc)
            .OrderByDescending(snapshot => snapshot.CapturedAtUtc)
            .FirstOrDefault();

        if (baseline == null) {
            baseline = history
                .OrderBy(snapshot => Math.Abs((snapshot.CapturedAtUtc - thresholdUtc).Ticks))
                .FirstOrDefault();
        }

        return baseline == null
            ? 0
            : currentNetWorthCopper - (baseline.Summary?.NetWorthCopper ?? 0);
    }

    private static List<HistoricalInvestmentResult> BuildHistoricalResults(IReadOnlyList<TransactionLedgerEntry> ledgerEntries) {
        var ledger = ledgerEntries ?? Array.Empty<TransactionLedgerEntry>();

        return ledger
            .Where(entry => entry != null && (entry.BoughtQuantity > 0 || entry.SoldQuantity > 0 || entry.HeldQuantity > 0))
            .Select(entry => {
                var totalProfit = entry.RealizedProfitCopper + entry.UnrealizedProfitCopper;
                var capitalBase = Math.Max(1, entry.AverageBuyPriceCopper * Math.Max(entry.BoughtQuantity, entry.HeldQuantity));
                var lastActivity = MaxTimestamp(entry.LastBoughtUtc, entry.LastSoldUtc);
                return new HistoricalInvestmentResult() {
                    ItemId = entry.ItemId,
                    ItemName = entry.ItemName,
                    RealizedProfitCopper = entry.RealizedProfitCopper,
                    UnrealizedProfitCopper = entry.UnrealizedProfitCopper,
                    TotalProfitCopper = totalProfit,
                    ReturnOnCapitalPercent = Math.Round(totalProfit / (decimal)capitalBase * 100m, 1),
                    BoughtQuantity = entry.BoughtQuantity,
                    SoldQuantity = entry.SoldQuantity,
                    HeldQuantity = entry.HeldQuantity,
                    AverageBuyPriceCopper = entry.AverageBuyPriceCopper,
                    CurrentSellFloorCopper = entry.CurrentSellFloorCopper,
                    LastActivityUtc = lastActivity,
                    Verdict = BuildHistoricalVerdict(totalProfit, entry.UnrealizedProfitCopper, entry.HeldQuantity)
                };
            })
            .OrderByDescending(result => Math.Abs(result.TotalProfitCopper))
            .ThenByDescending(result => result.ReturnOnCapitalPercent)
            .Take(40)
            .ToList();
    }

    private static DateTimeOffset? MaxTimestamp(DateTimeOffset? first, DateTimeOffset? second) {
        if (!first.HasValue) {
            return second;
        }

        if (!second.HasValue) {
            return first;
        }

        return first.Value >= second.Value ? first : second;
    }

    private static string BuildHistoricalVerdict(int totalProfitCopper, int unrealizedProfitCopper, int heldQuantity) {
        if (totalProfitCopper > 0 && unrealizedProfitCopper >= 0) {
            return "Good investment";
        }

        if (totalProfitCopper > 0) {
            return "Good realized exit";
        }

        if (totalProfitCopper < 0 && heldQuantity > 0) {
            return "Needs exit review";
        }

        if (totalProfitCopper < 0) {
            return "Poor investment";
        }

        return "Flat result";
    }
}
