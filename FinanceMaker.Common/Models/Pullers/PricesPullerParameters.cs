using FinanceMaker.Common.Models.Pullers.Enums;

namespace FinanceMaker.Common;

public record PricesPullerParameters
{
    public string Ticker { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public Period Period { get; set; }

    public PricesPullerParameters(string ticker, DateTime startTime, DateTime endTime, Period period)
    {
        Ticker = ticker.Replace(".", "-");
        StartTime = startTime;
        EndTime = endTime;
        Period = period;
    }
    public PricesPullerParameters()
    {
        Ticker = string.Empty;
    }

    public static PricesPullerParameters GetTodayParams(string ticker)
    {
        var today = DateTime.Now.AddMinutes(1);

        return new(ticker, today.Subtract(TimeSpan.FromDays(1)), today, Period.OneMinute);
    }
}
