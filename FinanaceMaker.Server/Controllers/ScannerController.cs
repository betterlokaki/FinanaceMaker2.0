using System.Net;
using FinanceMaker.Common.Models.Pullers;
using FinanceMaker.Pullers.TickerPullers;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace FinanaceMaker.Server.Controllers
{
    [Route("api/[controller]"), EnableCors]
    public class ScannerController : Controller
    {
        private readonly MainTickersPuller m_Scanner;

        public ScannerController(MainTickersPuller scanner)
        {
            m_Scanner = scanner;
        }

        // Post: api/values
        [HttpPost]
        public Task<IEnumerable<string>> Post([FromBody] TickersPullerParameters scannerParams, CancellationToken token)
        {
            return m_Scanner.ScanTickers(scannerParams, token);
        }

        [HttpOptions]
        public HttpResponseMessage Options()
        {
            return new HttpResponseMessage { StatusCode = HttpStatusCode.OK };
        }
    }
}

