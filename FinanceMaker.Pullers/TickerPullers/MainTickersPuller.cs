using FinanceMaker.Common.Models.Pullers;
using FinanceMaker.Pullers.TickerPullers.Interfaces;

namespace FinanceMaker.Pullers.TickerPullers
{
    public sealed class MainTickersPuller : IParamtizedTickersPuller, ITickerPuller, IRelatedTickersPuller
    {
        private readonly IParamtizedTickersPuller[] m_ParametizedTickersPullers;
        private readonly ITickerPuller[] m_TickerPullers;
        private readonly IRelatedTickersPuller[] m_RelatedTickersPullers;

        public MainTickersPuller(IParamtizedTickersPuller[] parametizedTickersPullers,
                                 ITickerPuller[] tickerPullers,
                                 IRelatedTickersPuller[] relatedTickersPullers)
        {
            m_ParametizedTickersPullers = parametizedTickersPullers;
            m_TickerPullers = tickerPullers;
            m_RelatedTickersPullers = relatedTickersPullers;
        }

        public async Task<IEnumerable<string>> GetRelatedTickers(string ticker, CancellationToken cancellationToken)
        {
            var relatedTickersTasks = m_RelatedTickersPullers.Select(puller => puller.GetRelatedTickers(ticker, cancellationToken))
                                                             .ToArray();
            var relatedTickers = await Task.WhenAll(relatedTickersTasks);
            var relatedTickersFlat = relatedTickers.SelectMany(ticker => ticker)
                                                    .ToArray();

            return relatedTickersFlat;


        }

        public async Task<IEnumerable<string>> ScanTickers(CancellationToken cancellationToken)
        {

            var relatedTickersTasks = m_TickerPullers.Select(puller => puller.ScanTickers(cancellationToken))
                                                             .ToArray();
            var relatedTickers = await Task.WhenAll(relatedTickersTasks);
            var relatedTickersFlat = relatedTickers.SelectMany(ticker => ticker)
                                                    .ToArray();

            return relatedTickersFlat;
        }

        public async Task<IEnumerable<string>> ScanTickers(TickersPullerParameters scannerParams, CancellationToken cancellationToken)
        {

            var relatedTickersTasks = m_ParametizedTickersPullers.Select(puller => puller.ScanTickers(scannerParams, cancellationToken))
                                                             .ToList();
            var p = m_TickerPullers.Select(_ => _.ScanTickers(cancellationToken));
            relatedTickersTasks.AddRange(p);
            var relatedTickers = await Task.WhenAll(relatedTickersTasks);
            var relatedTickersFlat = relatedTickers.SelectMany(ticker => ticker)
                                                   .Where(ticker => !string.IsNullOrWhiteSpace(ticker)
                                                                    && !string.IsNullOrEmpty(ticker)
                                                                    && ticker.Length >= 1)
                                                   .ToArray();

            return relatedTickersFlat;
        }
    }
}

