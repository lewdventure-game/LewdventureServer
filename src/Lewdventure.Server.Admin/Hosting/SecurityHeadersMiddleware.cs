namespace Server.Admin.Hosting
{
    internal sealed class SecurityHeadersMiddleware
    {
        private const string ContentSecurityPolicy = "default-src 'self'; style-src 'self'; script-src 'self'; img-src 'self' data:; frame-ancestors 'none'; form-action 'self'; base-uri 'self'";

        private readonly RequestDelegate _next;

        public SecurityHeadersMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var headers = context.Response.Headers;

            headers["Content-Security-Policy"] = ContentSecurityPolicy;
            headers["X-Frame-Options"] = "DENY";
            headers["X-Content-Type-Options"] = "nosniff";
            headers["Referrer-Policy"] = "no-referrer";
            headers["Cache-Control"] = "no-store";

            await _next(context);
        }
    }
}
