namespace Translator.Middlewares
{
    public class CrosMiddle
    {
        private readonly RequestDelegate _next;
        public CrosMiddle(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext context)
        {
            if (!context.Response.Headers.ContainsKey("Access-Control-Allow-Origin"))
            {
                context.Response.Headers.AccessControlAllowOrigin = "*";
            }
            await _next(context);
        }
    }
}
