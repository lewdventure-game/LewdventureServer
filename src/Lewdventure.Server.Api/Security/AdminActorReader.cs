namespace Server.Api.Security
{
    internal sealed class AdminActorReader
    {
        public const string HeaderName = "X-Admin-Actor";

        private const string ActorPrefix = "admin:";
        private const int MaxActorLength = 64;

        public string Read(HttpContext httpContext)
        {
            var actor = httpContext.Request.Headers[HeaderName].ToString().Trim();

            if (IsValid(actor))
                return ActorPrefix + actor;

            var remoteAddress = httpContext.Connection.RemoteIpAddress;

            return ActorPrefix + (remoteAddress == null ? "unknown" : remoteAddress.ToString());
        }

        private bool IsValid(string actor)
        {
            if (actor.Length == 0 || MaxActorLength < actor.Length)
                return false;

            for (int i = 0; i < actor.Length; i++)
            {
                var symbol = actor[i];
                var isAllowed = char.IsAsciiLetterOrDigit(symbol) || symbol == '_' || symbol == '-' || symbol == '.' || symbol == '@';

                if (isAllowed == false)
                    return false;
            }

            return true;
        }
    }
}
