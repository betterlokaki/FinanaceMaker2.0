using System.Diagnostics;
using FinanceMaker.Common;
using FinanceMaker.Common.Extensions;
using FinanceMaker.Common.Models.Finance;
using FinanceMaker.Common.Models.Pullers.Enums;
using FinanceMaker.Common.Models.Pullers.YahooFinance;
using FinanceMaker.Pullers.PricesPullers.Interfaces;

namespace FinanceMaker.Pullers;

public sealed class YahooInterdayPricesPuller : IPricesPuller
{
    private readonly IHttpClientFactory m_RequestsService;
    private readonly Dictionary<Period, string> m_RelevantPeriods;
    private readonly string m_FinanceUrl;

    public YahooInterdayPricesPuller(IHttpClientFactory requestsService)
    {
        m_RequestsService = requestsService;
        m_RelevantPeriods = new Dictionary<Period, string>
        {
            { Period.OneMinute, "1m" },
            { Period.ThreeMinutes, "3m" },
            { Period.OneHour, "1h"},
            { Period.Daily, "1d"}
        };
        m_FinanceUrl = "https://query1.finance.yahoo.com/v8/finance/chart/{0}?period1={1}&period2={2}&interval={3}&includePrePost=true&lang=en-US&region=US";
    }

    public async Task<IEnumerable<FinanceCandleStick>> GetTickerPrices(PricesPullerParameters pricesPullerParameters,
                                                                       CancellationToken cancellationToken)
    {
        var period = pricesPullerParameters.Period;

        if (!m_RelevantPeriods.TryGetValue(period, out string? yahooPeriod))
        {
            throw new NotImplementedException($"Yahoo interday api doesn't support {Enum.GetName(period)} as a period");
        }
        DateTime startDate = pricesPullerParameters.StartTime;
        DateTime endDate = pricesPullerParameters.EndTime;
        var ticker = pricesPullerParameters.Ticker;

        if ((int)period < 8)
        {
            IEnumerable<(DateTime Start, DateTime End)> CreateDateRanges(DateTime startDate, DateTime endDate)
            {
                var ranges = new List<(DateTime Start, DateTime End)>();
                DateTime currentStart = startDate;

                while (currentStart < endDate)
                {
                    DateTime currentEnd = currentStart.AddDays(7) > endDate ? endDate : currentStart.AddDays(7);
                    ranges.Add((currentStart, currentEnd));
                    currentStart = currentEnd;
                }

                return ranges;
            }

            var dateRanges = CreateDateRanges(startDate, endDate);

            var tasks = dateRanges.Select(range => PullDataFromYahoo(range.Start, range.End, ticker, yahooPeriod, cancellationToken)).ToArray();
            var results = await Task.WhenAll(tasks);
            var allCandles = results.SelectMany(yahooResponse =>
            {
                if (yahooResponse.IsEmpty())
                {
                    return [];
                }
                return CreateCandlesFromYahoo(yahooResponse);
            }).ToArray();

            return allCandles;
        }

        var yahooResponse = await PullDataFromYahoo(startDate, endDate, ticker, yahooPeriod, cancellationToken);

        if (yahooResponse.IsEmpty()) return [];

        var candles = CreateCandlesFromYahoo(yahooResponse);
        cancellationToken.ThrowIfCancellationRequested();

        return candles;
    }

    public bool IsRelevant(PricesPullerParameters args)
    {
        return m_RelevantPeriods.ContainsKey(args.Period);
    }

    private FinanceCandleStick[] CreateCandlesFromYahoo(YahooResponse yahooResponse)
    {
        var timestamps = yahooResponse.timestamp;
        var indicators = yahooResponse.indicators.quote.First();
        var candles = new FinanceCandleStick[timestamps.Length];

        for (int i = 0; i < timestamps.Length; i++)
        {
            var candleDate = DateTimeOffset.FromUnixTimeSeconds(timestamps[i]).DateTime;
            var open = indicators.open[i] ?? indicators.open[i - 1] ?? 0;
            var close = indicators.close[i] ?? indicators.close[i - 1] ?? 0;
            var low = indicators.low[i] ?? indicators.low[i - 1] ?? 0;
            var high = indicators.high[i] ?? indicators.high[i - 1] ?? 0;
            long volume = indicators.volume[i] ?? indicators.volume[i - 1] ?? 0;

            candles[i] = new FinanceCandleStick(candleDate, open, close, high, low, volume);
        }
        var haCandles = candles;
        haCandles = candles.Select((candle, index) =>
        {
            var haClose = (candle.Open + candle.Close + candle.High + candle.Low) / 4;
            var haOpen = index == 0 ? (candle.Open + candle.Close) / 2 : (haCandles[index - 1].Open + haCandles[index - 1].Close) / 2;
            var haHigh = Math.Max(candle.High, Math.Max(haOpen, haClose));
            var haLow = Math.Min(candle.Low, Math.Min(haOpen, haClose));

            return new FinanceCandleStick(candle.Time, haOpen, haClose, haHigh, haLow, candle.Volume);
        }).ToArray();

        return haCandles;
    }

    private async Task<YahooResponse> PullDataFromYahoo(DateTime startDate,
                                                        DateTime endDate,
                                                        string ticker,
                                                        string yahooPeriod,
                                                        CancellationToken cancellationToken)
    {
        var client = m_RequestsService.CreateClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Itay-Barel");

        //client.AddBrowserUserAgent();
        var startTime = ((DateTimeOffset)startDate.ToUniversalTime()).ToUnixTimeSeconds();
        var endTime = ((DateTimeOffset)endDate.ToUniversalTime()).ToUnixTimeSeconds();


        var url = string.Format(m_FinanceUrl, ticker, startTime, endTime, yahooPeriod);
        HttpResponseMessage? response;
        int maxRetries = 3;
        int delayMilliseconds = 1000;
        response = null;

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                response = await client.GetAsync(url, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    var data = await response.Content.ReadAsStringAsync(cancellationToken);
                    break;
                }
            }
            catch
            {
                if (attempt == maxRetries)
                {
                    return YahooResponse.Enpty;
                }
            }

            await Task.Delay(delayMilliseconds, cancellationToken);
        }

        if (response is null || !response.IsSuccessStatusCode)
        {
            if (response is not null)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);

                Debug.WriteLine($"Error: {error}");
            }
            return YahooResponse.Enpty;

        }

        var yahooResponse = await response.Content.ReadAsAsync<InterdayModel>(cancellationToken);

        var result = yahooResponse?.chart?.result?.FirstOrDefault();
        var indicators = result?.indicators?.quote?.FirstOrDefault();

        if (result is null || indicators is null || result.timestamp is null)
        {
            return YahooResponse.Enpty;
        }

        return result;
    }
}
