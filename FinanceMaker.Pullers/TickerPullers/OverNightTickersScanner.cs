using FinanceMaker.Common.Models.Pullers;

namespace FinanceMaker.Pullers.TickerPullers;

public class NivTickersPuller : FinvizTickersPuller
{
    public NivTickersPuller(IHttpClientFactory requestService) : base(requestService)
    {
        m_FinvizUrl = "https://finviz.com/screener.ashx?v=111&f=sh_curvol_o5000%2Csh_float_u50%2Csh_relvol_o3%2Csh_short_o10%2Cta_change_u1&ft=4";
    }

    public override async Task<IEnumerable<string>> ScanTickers(TickersPullerParameters scannerParams, CancellationToken cancellationToken)
    {
        // Stop trying removin' the await, it's not gonna work
        // The c# can't convert Task<IEnumerable<string>> to Task<string[]>
        // But I think this docummentation will recover your memory 
        // https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/concepts/async/ 
        // (It's just from the copilot and it looks cool don't really click on it)
        // I love eating the same food every day, but I don't think you do
        // I think you should try to make it more dynamic, but I don't know how
        

        return await GetTickers(m_FinvizUrl, cancellationToken);
    }
}
