using FinanceMaker.Algorithms;
using FinanceMaker.Common;
using FinanceMaker.Common.Extensions;
using FinanceMaker.Common.Models.Finance;
using FinanceMaker.Common.Models.Ideas.Enums;
using FinanceMaker.Common.Models.Ideas.IdeaInputs;
using FinanceMaker.Common.Models.Ideas.IdeaOutputs;
using FinanceMaker.Common.Models.Pullers.Enums;
using FinanceMaker.Ideas.Ideas.Abstracts;
using FinanceMaker.Pullers.TickerPullers;
using FinanceMaker.Pullers.TickerPullers.Interfaces;


namespace FinanceMaker.Ideas.Ideas;

public class KeyLevelsEntryExitOutputIdea<TInput, TOutput> :
     IdeaBase<TInput, TOutput>
     where TInput : TechnicalIdeaInput
     where TOutput : EntryExitOutputIdea

{
    private readonly RangeAlgorithmsRunner m_AlgoRunner;
    private readonly IParamtizedTickersPuller m_Puller;
    private readonly Func<string, PricesPullerParameters> m_PricesPullerParams;

    public override IdeaTypes Type => IdeaTypes.EntryExit;

    public KeyLevelsEntryExitOutputIdea(MainTickersPuller puller,
                                        RangeAlgorithmsRunner algoRunner)
    {
        m_Puller = puller;
        m_AlgoRunner = algoRunner;
        m_PricesPullerParams = (ticker) => new PricesPullerParameters(ticker,
                                                                      DateTime.Now.AddYears(-5),
                                                                      DateTime.Now,
                                                                      Period.Daily);
    }


    protected override async Task<IEnumerable<TOutput>> CreateIdea(TInput input,
                                                                   CancellationToken cancellationToken)
    {
        var scannerParams = input.TechnicalParams;
        var relevantTickers = await m_Puller.ScanTickers(scannerParams, cancellationToken);
        // Now we want to run some algos on the relevant stocks 

        var ideas = new List<EntryExitOutputIdea>(relevantTickers.GetNonEnumeratedCount());

        foreach (var ticker in relevantTickers)
        {
            var rangeParams = m_PricesPullerParams.Invoke(ticker);
            var algoInput = new RangeAlgorithmInput(rangeParams, Algorithm.KeyLevels);

            var keyLevelRunner = m_AlgoRunner.Resolve(algoInput);
            var keyLevels = await keyLevelRunner.Run(algoInput, cancellationToken);

            if (keyLevels is not KeyLevelCandleSticks candleSticks) continue;

            // For now let's keep it simple and will just use one keyLevel with a presentage of 2%
            var (closestKeyLevels, _) = candleSticks.GetClosestToLastKeyLevels(maxPresentage: 2,
                                                                               numberOfKeyLevels: 1);

            if (closestKeyLevels.NullOrEmpty()) continue;

            var idea = CreateSingleIdea(ticker, candleSticks, closestKeyLevels);

            if (idea.IsEmpty()) continue;

            ideas.Add(idea);
        }

        return ideas.OfType<TOutput>()
                    .ToArray();
    }

    protected virtual EntryExitOutputIdea CreateSingleIdea(
        string ticker,
        IEnumerable<EMACandleStick> candleSticks,
        IEnumerable<double> closestKeyLevels)
    {

        if (candleSticks is not KeyLevelCandleSticks keyLevelsCandles)
        {
            return EntryExitOutputIdea.Empy;
        }
        var current = candleSticks.Last();
        var entryPrice = closestKeyLevels.First();
        // Probably I will change this for something better but for now lets take like 3 %

        // This means we probably wanna short the ticker
        var exitPrice = 0f;
        var stopLoss = 0f;

        Func<float, bool> findKeyLevelDel
                = (double)current.Close <= entryPrice ?
                    _ => _ != entryPrice && _ - entryPrice * 0.93 <= 0 :
                     _ => _ != entryPrice && _ - entryPrice * 1.07 > 0;
        Func<IEnumerable<float>, float> mostRelevant = _ =>
        {
            if (_.GetNonEnumeratedCount() == 0)
            {
                return 0;
            }

            return current.Close <= entryPrice
                    ? _.MaxBy(_ => _ - entryPrice * 0.97)
                    : _.MinBy(_ => _ - entryPrice * 1.03);
        };
        exitPrice = mostRelevant.Invoke(keyLevelsCandles.KeyLevels.Where(findKeyLevelDel));

        stopLoss = current.Close <= entryPrice ? (float)(entryPrice * 1.07) : (float)(entryPrice * 0.93);

        // No good levels were found to trade 
        if (exitPrice == 0 || stopLoss == 0)
        {
            return EntryExitOutputIdea.Empy;
        }

        return new EntryExitOutputIdea($"We enter at: {entryPrice} we will take profit at: {exitPrice} but we are ready to loose at {stopLoss}",
                                       ticker,
                                       (float)entryPrice,
                                       exitPrice,
                                       stopLoss);
    }

}
