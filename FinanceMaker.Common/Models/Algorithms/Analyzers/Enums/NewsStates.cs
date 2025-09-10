using System.ComponentModel;

namespace FinanceMaker.Common.Models.Algorithms.Analyzers.Enums;

/// <summary>
/// Flags for all posibilties of news state, if you think of a new possibilty just add.
/// </summary>
public enum NewsStates
{
    [Description("None")]
    None = 0,
    [Description("Good")]
    Good = 1,
    [Description("Bad")]
    Bad = 2,
}
