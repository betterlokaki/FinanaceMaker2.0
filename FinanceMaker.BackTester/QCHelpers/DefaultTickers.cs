namespace FinanceMaker.BackTester.QCHelpers;

/// <summary>
/// Contains default tickers for various trading strategies.
/// </summary>
public static class DefaultTickers
{
    /// <summary>
    /// Default tickers for range-based algorithms.
    /// </summary>
    public static readonly string[] RangeAlgorithmTickers =
    [
        "PLTR",
        "HUT",
        "NVDA"
    ];

    /// <summary>
    /// Tickers that have shown problematic behavior and should be handled with care.
    /// </summary>
    public static readonly string[] ProblematicTickers =
    [
    ];
}
