using System;
using System.Threading;
using System.Threading.Tasks;
using FinanceMaker.Publisher.Traders.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NCrontab;

public class Worker
{
    private NCrontab.CrontabSchedule m_CrontabSchedule;
    private readonly ILogger<Worker> m_Logger;
    private readonly ITrader m_Trader;
    private readonly MarketStatus m_MarketStatus;

    public Worker(ILogger<Worker> logger, ITrader workerTrader, MarketStatus marketStatus)
    {
        m_Logger = logger;
        m_Trader = workerTrader;
        m_MarketStatus = marketStatus;
        m_CrontabSchedule = CrontabSchedule.Parse("59 1 * * *");

    }

    public async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var isMarketOpen = await m_MarketStatus.IsMarketOpenAsync(stoppingToken);
#if DEBUG
            isMarketOpen = true;
#endif
            if (isMarketOpen)
            {
                try
                {
                    await m_Trader.Trade(stoppingToken);

                }
                catch { }
            }

            await Task.Delay(2 * 1000);

        }
    }
}
