using FinanceMaker.Common;
using FinanceMaker.Common.Models.Finance;
using FinanceMaker.Common.Models.Pullers.Enums;
using FinanceMaker.Pullers.PricesPullers;
using Microsoft.AspNetCore.Mvc;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace FinanaceMaker.Server.Controllers
{
    [Route("api/[controller]")]
    public class PricesController : Controller
    {
        private readonly MainPricesPuller m_PricesPuller;

        public PricesController(MainPricesPuller pricesPuller)
        {
            m_PricesPuller = pricesPuller;
        }

        // GET: api/values
        [HttpGet]
        public Task<IEnumerable<FinanceCandleStick>> Get([FromQuery] string ticker,
                                                          [FromQuery] DateTime start,
                                                          [FromQuery] DateTime end,
                                                          [FromQuery] Period period,
                                                          CancellationToken cancellationToken)
        {
            var parameters = new PricesPullerParameters(ticker, start, end, period);
            
            return m_PricesPuller.GetTickerPrices(parameters, cancellationToken);
        }
    }
}

