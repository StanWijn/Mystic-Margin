using Gw2FlipOverlay.Models;
using System;

namespace Gw2FlipOverlay.Services;

public static class MarketValueHelper {

    public static (decimal Percent, MarketValueBand Band, string Label) Calculate(int currentValueCopper, int referenceCopper) {
        if (referenceCopper <= 0 || currentValueCopper <= 0) {
            return (100m, MarketValueBand.Fair, "MV 100% Fair");
        }

        var percent = Math.Round((currentValueCopper / (decimal)referenceCopper) * 100m, 1);

        if (percent < 85m) {
            return (percent, MarketValueBand.Cheap, $"MV {percent:N0}% Cheap");
        }

        if (percent < 95m) {
            return (percent, MarketValueBand.BelowFair, $"MV {percent:N0}% Below");
        }

        if (percent <= 105m) {
            return (percent, MarketValueBand.Fair, $"MV {percent:N0}% Fair");
        }

        if (percent <= 120m) {
            return (percent, MarketValueBand.Rich, $"MV {percent:N0}% Rich");
        }

        return (percent, MarketValueBand.Overheated, $"MV {percent:N0}% Hot");
    }
}
