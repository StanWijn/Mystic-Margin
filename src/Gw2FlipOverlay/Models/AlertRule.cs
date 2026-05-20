using System.Collections.Generic;

namespace Gw2FlipOverlay.Models;

public sealed class AlertRule {

    public string Name { get; set; } = string.Empty;

    public AlertRuleKind Kind { get; set; }

    public decimal Threshold { get; set; }

    public bool Enabled { get; set; } = true;

    public static List<AlertRule> CreateDefaultRules() {
        return new List<AlertRule>() {
            new AlertRule() {
                Name = "Below fair value",
                Kind = AlertRuleKind.DiscountPercent,
                Threshold = 8m
            },
            new AlertRule() {
                Name = "Wide spread",
                Kind = AlertRuleKind.SpreadPercent,
                Threshold = 12m
            },
            new AlertRule() {
                Name = "Pressure jump",
                Kind = AlertRuleKind.DemandPressureJump,
                Threshold = 0.20m
            },
            new AlertRule() {
                Name = "Order fill improvement",
                Kind = AlertRuleKind.OrderFillImprovement,
                Threshold = 1m
            }
        };
    }
}
