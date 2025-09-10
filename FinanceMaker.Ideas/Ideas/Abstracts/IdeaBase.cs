using FinanceMaker.Common.Models.Ideas.Enums;
using FinanceMaker.Common.Models.Ideas.IdeaInputs;
using FinanceMaker.Common.Models.Ideas.IdeaOutputs;
using FinanceMaker.Ideas.Ideas.Interfaces;

namespace FinanceMaker.Ideas.Ideas.Abstracts;

public abstract class IdeaBase<TInput, TOutput> : IIdea where TInput : GeneralInputIdea
                                                where TOutput : GeneralOutputIdea
{
    public abstract IdeaTypes Type { get; }

    public async Task<IEnumerable<GeneralOutputIdea>> CreateIdea(GeneralInputIdea input, CancellationToken cancellationToken)
    {
        if (input is null || input is not TInput actualInput)
        {
            throw new ArgumentException($"input is not type of - {typeof(TInput)}.");
        }

        var idea = await CreateIdea(actualInput, cancellationToken);

        return idea;
    }


    protected abstract Task<IEnumerable<TOutput>> CreateIdea(TInput input, CancellationToken cancellationToken);
}
