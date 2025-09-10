namespace FinanceMaker.Common.Models.Interactive;

public class IBKROrderRequest
{
    public string AcctId { get; set; } = string.Empty;
    public string ConId { get; set; } = string.Empty;
    public string OrderType { get; set; } = "LMT";
    public string Side { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal Price { get; set; }
    public string Tif { get; set; } = "GTC";
    public bool OutsideRth { get; set; }
    public decimal StopPrice { get; set; }
    public decimal TakeProfit { get; set; }
}
