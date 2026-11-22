using Microsoft.Extensions.Options;
using Server.Api.Security;
using Server.Infrastructure.Mongo.Players;

namespace Server.Api.Payments
{
    internal sealed class PaymentSessionFilter
    {
        private readonly PaymentIdentity _identity;
        private readonly PlayerIdentityReader _reader;
        private readonly PaymentOptions _options;

        public PaymentSessionFilter(PaymentIdentity identity, PlayerIdentityReader reader, IOptions<PaymentOptions> options)
        {
            _identity = identity;
            _reader = reader;
            _options = options.Value;
        }

        public EndpointFilterDelegate Create(EndpointFilterFactoryContext factoryContext, EndpointFilterDelegate next)
        {
            return async context =>
            {
                var httpContext = context.HttpContext;
                var scope = httpContext.User.FindFirst(PaymentIdentity.ScopeClaim)?.Value;
                var steam = _options.SteamAppId != 0 && scope == _options.SteamScope;
                var nutaku = _options.NutakuTitleId.Length != 0 && scope == _options.NutakuScope;

                if (steam == false && nutaku == false)
                    return Results.StatusCode(StatusCodes.Status403Forbidden);

                var userId = _reader.Read(httpContext.User);
                var user = await _identity.GetAsync(userId, httpContext.RequestAborted);

                if (user.Id.Length == 0 || user.Status != UserDocument.ActiveStatus || user.Qa != null)
                    return Results.StatusCode(StatusCodes.Status403Forbidden);

                var verifiedScope = steam ? _options.SteamScope : _options.NutakuScope;
                var subject = _identity.ReadSubject(user, verifiedScope);

                if (subject.Length == 0)
                    return Results.StatusCode(StatusCodes.Status403Forbidden);

                await httpContext.RequestServices.GetRequiredService<PaymentRewardRestorer>().RestoreAsync(userId, httpContext.RequestAborted);
                httpContext.Items[typeof(PaymentSession)] = new PaymentSession(userId, verifiedScope, subject, steam);

                return await next(context);
            };
        }
    }
}
