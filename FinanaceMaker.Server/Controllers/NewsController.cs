using FinanceMaker.Common.Models.Pullers;
using FinanceMaker.Common.Models.Pullers.News.NewsResult;
using FinanceMaker.Pullers.NewsPullers;
using Microsoft.AspNetCore.Mvc;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace FinanaceMaker.Server.Controllers
{
    [Route("api/[controller]")]
    public class NewsController : Controller
    {
        private readonly MainNewsPuller m_NewsPuller;

        public NewsController(MainNewsPuller newsPuller)
        {
            m_NewsPuller = newsPuller;
        }

        [HttpGet]
        public Task<IEnumerable<NewsResult>> GetTickerNews([FromQuery] string ticker, CancellationToken cancellationToken)
        {
            var newsParams = NewsPullerParameters.GetTodayParams(ticker);

            return m_NewsPuller.PullNews(newsParams, cancellationToken);
        }
    }
}

