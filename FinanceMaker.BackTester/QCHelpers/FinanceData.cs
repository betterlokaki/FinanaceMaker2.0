using System.Diagnostics.Metrics;
using System.Globalization;
using FinanceMaker.Common.Models.Finance;
using FinanceMaker.Common.Models.Finance.Enums;
using QLNet;
using QuantConnect;
using QuantConnect.Data;
using Period = FinanceMaker.Common.Models.Pullers.Enums.Period;
namespace FinanceMaker.BackTester.QCHelpers;

public class FinanceData : BaseData
{
    public static DateTime StartDate
    {
        get; set;
    }
    public static DateTime EndDate { get; set; }
    public EMACandleStick CandleStick { get; set; } = EMACandleStick.Empty;
    public static int CounterData { get; set; }
    public static int CounterDataSource { get; private set; }

    public Period ConvertResolutionToPeriod(Resolution resolution)
    {
        return resolution switch
        {
            Resolution.Minute => Period.OneMinute,
            Resolution.Hour => Period.OneHour,
            Resolution.Daily => Period.Daily,
            _ => throw new ArgumentOutOfRangeException(nameof(resolution), resolution, null)
        };
    }
    // Override GetSource to specify the source of your data

    public override SubscriptionDataSource GetSource(SubscriptionDataConfig config, DateTime date, bool isLiveMode)
    {
        // Define the path to your custom data file
        CounterDataSource++;
        var end = date.AddDays(1).AddHours(23).AddMinutes(59);
        end = end > DateTime.Now ? DateTime.Now : end;
        var filePath = Helper.SaveCandlestickDataToCsv(config.Symbol.Value,
                                ConvertResolutionToPeriod(config.Resolution),
                                                date,
                                                end).Result;
        return new SubscriptionDataSource(filePath, SubscriptionTransportMedium.LocalFile);
    }

    // Override Reader to parse data into your CustomCandleData object
    public override BaseData Clone()
    {
        return new FinanceData
        {
            Symbol = Symbol,
            Time = Time,
            EndTime = EndTime,
            Value = Value,
            CandleStick = CandleStick.Clone()
        };
    }

    public override BaseData Reader(SubscriptionDataConfig config,
                                    string line,
                                    DateTime date,
                                    bool isLiveMode)
    {

        try
        {
            // Parse the CSV line
            var data = line.Split(',');
            var candleTime = DateTime.ParseExact(data[0], "yyyyMMdd HH:mm:ss", CultureInfo.InvariantCulture);
            var candleEndDate = config.Resolution switch
            {
                Resolution.Minute => candleTime.AddMinutes(1),
                Resolution.Hour => candleTime.AddHours(1),
                Resolution.Daily => candleTime.AddDays(1),
                _ => throw new ArgumentOutOfRangeException(nameof(config.Resolution), config.Resolution, null)
            };
            var aaa = new FinanceData
            {
                Symbol = config.Symbol,
                EndTime = candleEndDate,
                Time = candleTime,
                Value = Convert.ToDecimal(data[4], CultureInfo.InvariantCulture),
                CandleStick = new EMACandleStick
                (new FinanceCandleStick
                (
                DateTime.ParseExact(data[0], "yyyyMMdd HH:mm:ss", CultureInfo.InvariantCulture),
                Convert.ToSingle(data[1], CultureInfo.InvariantCulture),
                Convert.ToSingle(data[2], CultureInfo.InvariantCulture),
                Convert.ToSingle(data[3], CultureInfo.InvariantCulture),
                Convert.ToSingle(data[4], CultureInfo.InvariantCulture),
                Convert.ToInt64(data[5], CultureInfo.InvariantCulture)

                ), Convert.ToSingle(data[6], CultureInfo.InvariantCulture))
                {
                    EMASignal = (TrendTypes)Convert.ToInt32(data[7], CultureInfo.InvariantCulture),
                    BreakThrough = (TrendTypes)Convert.ToInt32(data[8], CultureInfo.InvariantCulture),
                    Pivot = (Pivot)Convert.ToInt32(data[9], CultureInfo.InvariantCulture)
                }
            };

            return aaa;
        }
        catch (Exception ex)
        {
            // Log or handle the exception as needed
            throw new Exception($"Error parsing line: {line}. Details: {ex.Message}", ex);
        }
    }
}
