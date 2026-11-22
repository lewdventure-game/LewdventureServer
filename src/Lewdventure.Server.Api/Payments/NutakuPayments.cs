using System.Globalization;
using Microsoft.Extensions.Options;
using Server.Infrastructure.Mongo.Players;
using Server.Services;

namespace Server.Api.Payments
{
    internal sealed class NutakuPayments
    {
        private readonly PaymentOptions _options;
        private readonly PaymentIdentity _identity;
        private readonly PaymentStore _store;
        private readonly TimeProvider _timeProvider;
        private readonly TokenGenerator _tokens;
        private readonly IConfigDistributor _configDistributor;

        public NutakuPayments(
            IOptions<PaymentOptions> options,
            PaymentIdentity identity,
            PaymentStore store,
            TimeProvider timeProvider,
            TokenGenerator tokens,
            IConfigDistributor configDistributor)
        {
            _options = options.Value;
            _identity = identity;
            _store = store;
            _timeProvider = timeProvider;
            _tokens = tokens;
            _configDistributor = configDistributor;
        }

        public bool IsAuthentic(HttpRequest request)
        {
            return _options.NutakuTitleId.Length != 0 &&
                request.Headers.TryGetValue("NutakuS2sKey", out var key) && key.Count == 1 &&
                _tokens.AreEqual(_tokens.Hash(key.ToString()), _tokens.Hash(_options.NutakuServerKey)) &&
                ReadQuery(request, "titleId") == _options.NutakuTitleId &&
                ReadQuery(request, "gameType") == "dl";
        }

        public string ReadSubject(HttpRequest request)
        {
            var subject = ReadQuery(request, "userId");

            return ulong.TryParse(subject, NumberStyles.None, CultureInfo.InvariantCulture, out var value) && 0 < value
                ? value.ToString(CultureInfo.InvariantCulture)
                : string.Empty;
        }

        public string ReadPaymentId(HttpRequest request)
        {
            var value = ReadQuery(request, "paymentId");

            if (value.Length == 0 || 128 < value.Length)
                return string.Empty;

            foreach (var character in value)
            {
                if (char.IsAsciiLetterOrDigit(character) == false && character != '-' && character != '_')
                    return string.Empty;
            }

            return value;
        }

        public async Task<string> CreateLoginCodeAsync(string subject, CancellationToken cancellationToken)
        {
            var code = _tokens.CreateToken();

            await _store.AddLoginCodeAsync(new NutakuLoginCode
            {
                Id = _tokens.Hash(code),
                Scope = _options.NutakuScope,
                Subject = subject,
                ExpiresAt = _timeProvider.GetUtcNow().UtcDateTime.AddMinutes(2),
            }, cancellationToken);

            return code;
        }

        public async Task<string> ConsumeLoginCodeAsync(string code, CancellationToken cancellationToken)
        {
            if (code.Length != 43)
                return string.Empty;

            var login = await _store.ConsumeLoginCodeAsync(_tokens.Hash(code), _options.NutakuScope, cancellationToken);

            return login == null ? string.Empty : login.Subject;
        }

        public async Task<bool> CreateOrderAsync(string paymentId, string subject, NutakuPaymentRequest request, CancellationToken cancellationToken)
        {
            if (request.PaymentId != paymentId || (_options.NutakuSandbox == false && request.Test != 0))
                return false;

            var previous = await _store.GetAsync(_options.NutakuScope, paymentId, cancellationToken);

            if (previous != null)
                return Matches(previous, subject, request) && previous.State != PaymentOrderState.Reversed;

            if (_options.NutakuPurchasesEnabled == false)
                return false;

            var user = await _identity.FindAsync(_options.NutakuScope, subject, cancellationToken);

            if (user.Id.Length == 0 || user.Status != UserDocument.ActiveStatus || user.Qa != null)
                return false;

            foreach (var offer in _options.Offers)
            {
                if (offer.Sku != request.SkuId || offer.Name != request.Name || offer.NutakuGold == 0 || offer.NutakuGold != request.Price)
                    continue;

                var order = await _store.SaveNewAsync(new PaymentOrder
                {
                    Id = _store.OrderId(_options.NutakuScope, paymentId),
                    Scope = _options.NutakuScope,
                    ExternalId = paymentId,
                    UserId = user.Id,
                    Subject = subject,
                    Sku = offer.Sku,
                    Name = offer.Name,
                    Entitlement = offer.Entitlement,
                    Rewards = offer.Rewards,
                    Amount = offer.NutakuGold,
                    Currency = "GOLD",
                    Test = _options.NutakuSandbox || request.Test != 0,
                    CreatedAt = _timeProvider.GetUtcNow().UtcDateTime,
                    State = PaymentOrderState.AwaitingApproval,
                }, cancellationToken);

                return order != null && Matches(order, subject, request) && order.UserId == user.Id && order.State != PaymentOrderState.Reversed;
            }

            return false;
        }

        public async Task<bool> CompleteOrderAsync(string paymentId, string subject, CancellationToken cancellationToken)
        {
            var order = await _store.GetAsync(_options.NutakuScope, paymentId, cancellationToken);

            if (order == null || order.Subject != subject)
                return false;

            if (order.State == PaymentOrderState.Granted)
                return true;

            var user = await _identity.GetAsync(order.UserId, cancellationToken);

            if (user.Id.Length == 0 || _identity.ReadSubject(user, _options.NutakuScope) != subject)
                return false;

            return await _store.SettleAsync(order, false, paymentId, "nutaku:GPHS PUT", _configDistributor, cancellationToken);
        }

        public async Task<bool> MarkCompletionUncertainAsync(string paymentId, string subject, string evidence)
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var order = await _store.GetAsync(_options.NutakuScope, paymentId, deadline.Token);

            if (order == null || order.Subject != subject)
                return false;

            await _store.MarkReviewAsync(order, "nutaku_completion_uncertain", evidence, deadline.Token);
            await _store.AddAuditAsync(order, "completion_uncertain", "server", paymentId, evidence, deadline.Token);

            return true;
        }

        private bool Matches(PaymentOrder order, string subject, NutakuPaymentRequest request)
        {
            return order.Subject == subject && order.Sku == request.SkuId && order.Name == request.Name &&
                order.Amount == request.Price && order.Test == (_options.NutakuSandbox || request.Test != 0);
        }

        private string ReadQuery(HttpRequest request, string name)
        {
            return request.Query.TryGetValue(name, out var values) && values.Count == 1 ? values.ToString() : string.Empty;
        }
    }
}
