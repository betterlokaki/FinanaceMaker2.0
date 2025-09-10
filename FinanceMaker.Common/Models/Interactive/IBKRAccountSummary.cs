namespace FinanceMaker.Common.Models.Interactive;

public class IBKRAccountSummary
{
    public string Id { get; set; } = string.Empty;
    public decimal AvailableFunds { get; set; }
    public decimal NetLiquidation { get; set; }
    public decimal Equity { get; set; }
    public decimal BuyingPower { get; set; }
}
