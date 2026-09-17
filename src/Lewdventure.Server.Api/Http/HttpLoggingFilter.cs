using Server.Api.Endpoints;

namespace Server.Api.Http
{
    internal sealed class HttpLoggingFilter
    {
        public bool ShouldLog(HttpContext context)
        {
            return context.Request.Path.StartsWithSegments(ApiRoutes.Health) == false;
        }
    }
}
