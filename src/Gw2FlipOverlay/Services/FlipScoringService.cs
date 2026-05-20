using Gw2FlipOverlay.Models;
using System;

namespace Gw2FlipOverlay.Services;

public sealed class FlipScoringService {

    public FlipCandidate CreatePriceCandidate(int itemId, int highestBuy, int lowestSell) {
        var netResaleValue = CalculateNetResaleValue(lowestSell);
        var estimatedProfit = netResaleValue - highestBuy;
        var spreadPercent = highestBuy <= 0
            ? 0m
            : Math.Round((estimatedProfit / (decimal) highestBuy) * 100m, 1);

        var spreadWeight = Clamp(spreadPercent / 100m, 0.05m, 0.75m);
        var baseScore = estimatedProfit > 0
            ? Math.Round(estimatedProfit * (1m + spreadWeight), 2)
            : 0m;

        return new FlipCandidate() {
            ItemId = itemId,
            HighestBuy = highestBuy,
            LowestSell = lowestSell,
            NetResaleValue = netResaleValue,
            EstimatedProfit = estimatedProfit,
            SpreadPercent = spreadPercent,
            LiquidityScore = 0.35m,
            StabilityScore = 0.35m,
            Score = baseScore
        };
    }

    public FlipCandidate FinalizeCandidate(FlipCandidate candidate, string itemName, int buyStackSize, int sellStackSize, string sourceName) {
        candidate.ItemName = string.IsNullOrWhiteSpace(itemName)
            ? $"Item {candidate.ItemId}"
            : itemName;
        candidate.BuyStackSize = buyStackSize;
        candidate.SellStackSize = sellStackSize;
        candidate.LiquidityScore = CalculateLiquidityScore(buyStackSize, sellStackSize);
        candidate.StabilityScore = CalculateStabilityScore(buyStackSize, sellStackSize);
        candidate.Score = Math.Round(candidate.EstimatedProfit * candidate.LiquidityScore * candidate.StabilityScore, 2);
        candidate.Source = sourceName;

        return candidate;
    }

    public int CalculateNetResaleValue(int lowestSell) {
        return (int) Math.Floor(lowestSell * 0.85m);
    }

    public decimal CalculateLiquidityScoreForDisplay(int buyStackSize, int sellStackSize) {
        return CalculateLiquidityScore(buyStackSize, sellStackSize);
    }

    public decimal CalculateStabilityScoreForDisplay(int buyStackSize, int sellStackSize) {
        return CalculateStabilityScore(buyStackSize, sellStackSize);
    }

    private decimal CalculateLiquidityScore(int buyStackSize, int sellStackSize) {
        var totalStack = Math.Max(1, buyStackSize + sellStackSize);
        var scaled = (decimal) Math.Log10(totalStack + 1) / 2.5m;
        return Clamp(scaled, 0.15m, 1.00m);
    }

    private decimal CalculateStabilityScore(int buyStackSize, int sellStackSize) {
        var totalStack = buyStackSize + sellStackSize;

        if (totalStack <= 0) {
            return 0.15m;
        }

        var balance = 1m - (Math.Abs(buyStackSize - sellStackSize) / (decimal) totalStack);
        return Clamp(balance, 0.15m, 1.00m);
    }

    private static decimal Clamp(decimal value, decimal minimum, decimal maximum) {
        if (value < minimum) {
            return minimum;
        }

        if (value > maximum) {
            return maximum;
        }

        return value;
    }
}
