using System;

namespace FinanceMaker.BackTester.QCHelpers;

public sealed class MultiAlgoModel
{
    public List<string> Tickers { get; set; }
    public Action<FinanceData> OnData { get; set; }

    public MultiAlgoModel(List<string> tickers, Action<FinanceData> onData)
    {
        Tickers = tickers;
        OnData = onData;
    }
}
