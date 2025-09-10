namespace FinanceMaker.Common.Models.Trades.Trader;

/// <summary>
/// Contains all the relevant data for the current position, for any new data you think you need feel free to add it here
/// </summary>
public class Position
{
    public float BuyingPower { get; set; }
    public required IEnumerable<string> OpenedPositions { get; set; }
    public required IEnumerable<string> Orders { get; set; }
}
