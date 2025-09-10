namespace FinanceMaker.Common.Models.Ideas.IdeaOutputs;

public class GeneralOutputIdea
{
    public string Ticker { get; set; }
    public string Description { get; set; }
    public static GeneralOutputIdea Empty => new GeneralOutputIdea(string.Empty, string.Empty);
    public GeneralOutputIdea(string description, string ticker)
    {
        Description = description;
        Ticker = ticker;
    }
}
