namespace FinanceMaker.Common.Models.Interactive;

public class IBKRPosition
{
    public string Symbol { get; set; } = string.Empty;
    public decimal Position { get; set; }
    public decimal AvgPrice { get; set; }
}
