using FinanceMaker.Common.Models.Algorithms.Analyzers;
using FinanceMaker.Common.Models.Ideas.Enums;

namespace FinanceMaker.Common.Models.Ideas.IdeaOutputs;

public class EntryExitOutputIdea : GeneralOutputIdea
{
    public static EntryExitOutputIdea Empy
        => new EntryExitOutputIdea(string.Empty,
            string.Empty,
            0,
            0,
            0,
            0);
    public float Entry { get; set; }
    public float Exit { get; set; }
    public float Stoploss { get; set; }
    public int Quantity { get; set; }
    public IdeaTradeType Trade => Exit > Entry ? IdeaTradeType.Long : IdeaTradeType.Short;
    public float ProfitPressent => Trade == IdeaTradeType.Long ?
                                    100 * (Exit / Entry) : 100 * (Entry / Exit);
    public NewsAnalyzed[] Analyzed { get; set; }
    public EntryExitOutputIdea(string description,
                               string ticker,
                               float entry,
                               float exit,
                               float stoploss,
                               int quantity = 0) : base(description, ticker)
    {
        Entry = entry;
        Exit = exit;
        Stoploss = stoploss;
        Analyzed = [];
        Quantity = quantity;
    }

    public bool IsEmpty()
       => string.IsNullOrEmpty(Ticker) && string.IsNullOrEmpty(Description);
}
