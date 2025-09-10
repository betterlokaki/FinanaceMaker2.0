using FinanceMaker.Common;
using FinanceMaker.Common.Models.Finance;
using FinanceMaker.Pullers.PricesPullers.Interfaces;
using YahooFinanceApi;
using Period = FinanceMaker.Common.Models.Pullers.Enums.Period;
using YahooPeriod = YahooFinanceApi.Period;

namespace FinanceMaker.Pullers.PricesPullers
{
    public class YahooPricesPuller : IPricesPuller
    {
        private readonly IDictionary<Period, YahooPeriod> m_PeriodToPeriod;

        public YahooPricesPuller()
        {
            m_PeriodToPeriod = new Dictionary<Period, YahooPeriod>()
            {
                {  Period.Daily, YahooPeriod.Daily },
                {  Period.Weekly, YahooPeriod.Weekly },
                {  Period.Monthly, YahooPeriod.Monthly }
            };
        }


        public async Task<IEnumerable<FinanceCandleStick>> GetTickerPrices(PricesPullerParameters pricesPullerParameters,
                                                                           CancellationToken cancellationToken)
        {
            var period = pricesPullerParameters.Period;
            var endDate = pricesPullerParameters.EndTime.Subtract(TimeSpan.FromDays(1));
            if (!m_PeriodToPeriod.TryGetValue(period, out YahooPeriod yahooPeriod))
            {
                throw new NotImplementedException($"Yahoo api doesn't support {Enum.GetName(period)} as a period");
            }
            var historicalData = await Yahoo.GetHistoricalAsync(pricesPullerParameters.Ticker, pricesPullerParameters.StartTime, endDate, yahooPeriod, cancellationToken);

            var tickerCandles = historicalData.Select(data => new FinanceCandleStick(data.DateTime, (float)data.Open, (float)data.Close, (float)data.High, (float)data.Low, (int)data.Volume))
                                              .ToArray();

            return tickerCandles;
        }

        public bool IsRelevant(PricesPullerParameters args)
        {
            return m_PeriodToPeriod.ContainsKey(args.Period);
        }
    }
}

