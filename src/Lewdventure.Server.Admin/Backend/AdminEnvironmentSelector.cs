using Microsoft.Extensions.Options;
using Server.Admin.Options;

namespace Server.Admin.Backend
{
    internal sealed class AdminEnvironmentSelector
    {
        public const string QueryName = "env";
        public const string CookieName = "lewd_admin_env";

        private readonly AdminPanelOptions _options;

        public AdminEnvironmentSelector(IOptions<AdminPanelOptions> options)
        {
            _options = options.Value;
        }

        public List<string> CollectNames()
        {
            var names = new List<string>(_options.Environments.Count);

            for (int i = 0; i < _options.Environments.Count; i++)
                names.Add(_options.Environments[i].Name);

            return names;
        }

        public string Resolve(HttpContext httpContext)
        {
            var requested = httpContext.Request.Query[QueryName].ToString();

            if (IsKnown(requested))
            {
                httpContext.Response.Cookies.Append(CookieName, requested, new CookieOptions
                {
                    HttpOnly = true,
                    Secure = true,
                    SameSite = SameSiteMode.Strict,
                    MaxAge = TimeSpan.FromDays(30),
                });

                return requested;
            }

            if (httpContext.Request.Cookies.TryGetValue(CookieName, out var stored) && IsKnown(stored))
                return stored!;

            return _options.Environments.Count == 0 ? string.Empty : _options.Environments[0].Name;
        }

        private bool IsKnown(string? name)
        {
            if (string.IsNullOrEmpty(name))
                return false;

            for (int i = 0; i < _options.Environments.Count; i++)
            {
                if (string.Equals(_options.Environments[i].Name, name, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }
    }
}
