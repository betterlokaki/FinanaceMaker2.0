using FinanceMaker.Common.Models.Pullers;

namespace FinanceMaker.Common.Models.Ideas.IdeaInputs;

public class TechnicalIdeaInput: GeneralInputIdea
{
    public static TechnicalIdeaInput BestBuyers => new TechnicalIdeaInput
    {
        TechnicalParams = TickersPullerParameters.BestBuyer
    };

    public static TechnicalIdeaInput BestSellers => new TechnicalIdeaInput
    {
        TechnicalParams = TickersPullerParameters.BestSellers
    };

    public required TickersPullerParameters  TechnicalParams {get; set;}
}
