namespace Gw2FlipOverlay.Models;

public sealed class PortfolioSummary {

    public bool IsAuthenticated { get; set; }

    public int WalletCopper { get; set; }

    public int OutstandingBuyCopper { get; set; }

    public int OutstandingSellGrossCopper { get; set; }

    public int OutstandingSellNetCopper { get; set; }

    public int HoldingsValueCopper { get; set; }

    public int NetWorthCopper { get; set; }

    public int RealizedProfitCopper { get; set; }

    public int UnrealizedProfitCopper { get; set; }

    public int DailyDeltaCopper { get; set; }

    public int WeeklyDeltaCopper { get; set; }

    public int MonthlyDeltaCopper { get; set; }

    public string StatusMessage { get; set; } = string.Empty;
}
