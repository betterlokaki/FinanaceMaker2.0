// Licensed to the .NET Foundation under one or more agreements.

namespace FinanceMaker.Publisher.Traders.Interfaces
{
    public interface ITrader
    {
        /// <summary>
        /// Tells the trader to start trading by its strategy, it should get and idea from the all the idea creators we have
        /// and then starts its play by them
        /// </summary>
        /// <param name="cancellationToken">It should run until you cancel it</param>
        /// <returns></returns>
        Task Trade(CancellationToken cancellationToken);
    }
}
