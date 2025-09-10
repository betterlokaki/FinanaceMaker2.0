using FinanceMaker.Common.Models.Ideas.Enums;
using FinanceMaker.Common.Models.Ideas.IdeaInputs;
using FinanceMaker.Common.Models.Ideas.IdeaOutputs;

namespace FinanceMaker.Ideas.Ideas.Interfaces;

public interface IIdea
{
    IdeaTypes Type { get; }

    Task<IEnumerable<GeneralOutputIdea>> CreateIdea(GeneralInputIdea input,
                                       CancellationToken cancellationToken);
}
