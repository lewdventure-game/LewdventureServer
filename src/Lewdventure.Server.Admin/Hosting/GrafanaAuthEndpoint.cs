using Server.Admin.Accounts;

namespace Server.Admin.Hosting
{
    internal sealed class GrafanaAuthEndpoint
    {
        public const string Route = "/auth/grafana";

        private const string UserHeader = "X-WEBAUTH-USER";
        private const string RoleHeader = "X-WEBAUTH-ROLE";
        private const string ForwardedUriHeader = "X-Forwarded-Uri";
        private const string GrafanaAdminRole = "Admin";
        private const string GrafanaViewerRole = "Viewer";

        public void Map(WebApplication application)
        {
            application.MapGet(Route, Handle).AllowAnonymous();
        }

        private IResult Handle(HttpContext httpContext)
        {
            var user = httpContext.User;

            if (user.Identity == null || user.Identity.IsAuthenticated == false || string.IsNullOrEmpty(user.Identity.Name))
            {
                var returnUrl = httpContext.Request.Headers[ForwardedUriHeader].ToString();

                if (returnUrl.StartsWith('/') == false || returnUrl.StartsWith("//", StringComparison.Ordinal))
                    returnUrl = "/grafana/";

                return Results.Redirect("/Login?ReturnUrl=" + Uri.EscapeDataString(returnUrl));
            }

            httpContext.Response.Headers[UserHeader] = user.Identity.Name;
            httpContext.Response.Headers[RoleHeader] = user.IsInRole(AdminRoles.Admin) ? GrafanaAdminRole : GrafanaViewerRole;

            return Results.Ok();
        }
    }
}
