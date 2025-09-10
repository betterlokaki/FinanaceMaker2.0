namespace FinanceMaker.Common.Extensions
{
    public static class HttpClientExtensions
    {
        public static HttpClient AddBrowserUserAgent(this HttpClient client)
        {
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");

            return client;
        }
    }
}
