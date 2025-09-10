using FinanceMaker.Common;
using FinanceMaker.Common.Models.Finance;
using FinanceMaker.Common.Resolvers.Abstracts;
using FinanceMaker.Pullers.PricesPullers.Interfaces;

namespace FinanceMaker.Pullers.PricesPullers
{
    public sealed class MainPricesPuller : ResolverBase<IPricesPuller, PricesPullerParameters>, IPricesPuller
    {

        public MainPricesPuller(IPricesPuller[] pricesPuller) : base(pricesPuller)
        { }

        public Task<IEnumerable<FinanceCandleStick>> GetTickerPrices(PricesPullerParameters pricesPullerParameters,
                                                                     CancellationToken cancellationToken)
        {
            var resolvedPuller = Resolve(pricesPullerParameters);

            return resolvedPuller.GetTickerPrices(pricesPullerParameters,
                                                  cancellationToken);

        }

        public bool IsRelevant(PricesPullerParameters args)
        {
            return Resolve(args) is not null;
        }
    }
}

