namespace FinanceMaker.Common.Models.Trades.Enums;

[Flags]
public enum TraderType
{
    EntryExit = 0,
    StopLoss = 1,
    Market = 2,
    Dynamic = 4
}
