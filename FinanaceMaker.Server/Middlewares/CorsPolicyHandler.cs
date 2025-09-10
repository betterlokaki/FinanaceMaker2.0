namespace FinanaceMaker.Server.Middlewares
{
    // You may need to install the Microsoft.AspNetCore.Http.Abstractions package into your project
    public class CorsPolicyHandler
    {
        private readonly RequestDelegate _next;

        public CorsPolicyHandler(RequestDelegate next)
        {
            _next = next;
        }

        public Task Invoke(HttpContext httpContext)
        {
            httpContext.Response.Headers.AccessControlAllowOrigin = "*";
            httpContext.Response.Headers.AccessControlAllowMethods = "POST,GET";
            httpContext.Response.Headers.AccessControlRequestMethod = "POST,GET ";

            return _next(httpContext);
        }
    }

    // Extension method used to add the middleware to the HTTP request pipeline.
    public static class CorsPolicyHandlerExtensions
    {
        public static IApplicationBuilder UseMiddlewareClassTemplate(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<CorsPolicyHandler>();
        }
    }
}

