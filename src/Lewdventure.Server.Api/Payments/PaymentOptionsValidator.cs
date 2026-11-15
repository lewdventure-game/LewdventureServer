using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Options;
using Server.Infrastructure.Mongo;
using Server.Infrastructure.Players;

namespace Server.Api.Payments
{
    internal sealed class PaymentOptionsValidator : IValidateOptions<PaymentOptions>
    {
        private readonly IOptions<AuthOptions> _authOptions;
        private readonly IOptions<MongoOptions> _mongoOptions;

        public PaymentOptionsValidator(IOptions<AuthOptions> authOptions, IOptions<MongoOptions> mongoOptions)
        {
            _authOptions = authOptions;
            _mongoOptions = mongoOptions;
        }

        public ValidateOptionsResult Validate([AllowNull] string name, PaymentOptions options)
        {
            var mongo = _mongoOptions.Value;

            if (options.Configured && (_authOptions.Value.Enabled == false || mongo.Enabled == false ||
                mongo.RequireReplicaSet == false || mongo.ApplyIndexesOnStartup == false))
                return ValidateOptionsResult.Fail("Payments require player authentication and Mongo replica set with startup indexes.");

            if ((options.SteamAppId != 0 || options.SteamPurchasesEnabled) &&
                (options.SteamAppId == 0 || string.IsNullOrWhiteSpace(options.SteamPublisherKey)))
                return ValidateOptionsResult.Fail("Steam payments require an App ID and a publisher key.");

            if ((options.NutakuTitleId.Length != 0 || options.NutakuPurchasesEnabled) &&
                (IsIdentifier(options.NutakuTitleId) == false || IsHeaderValue(options.NutakuServerKey) == false))
                return ValidateOptionsResult.Fail("Nutaku payments require a valid CMS title ID and S2S key.");

            var skus = new HashSet<string>(StringComparer.Ordinal);
            var steamItems = new HashSet<uint>();

            foreach (var offer in options.Offers)
            {
                if (IsIdentifier(offer.Sku) == false || skus.Add(offer.Sku) == false || IsIdentifier(offer.Entitlement) == false ||
                    string.IsNullOrWhiteSpace(offer.Name) || 128 < offer.Name.Length)
                    return ValidateOptionsResult.Fail("Payment offers require a unique SKU, an entitlement and a name of at most 128 characters.");

                if (offer.NutakuGold != 0 && (offer.NutakuGold < 10 || 30000 < offer.NutakuGold))
                    return ValidateOptionsResult.Fail("Nutaku offer prices must be between 10 and 30000 Gold.");

                if (offer.SteamItemId != 0 && steamItems.Add(offer.SteamItemId) == false ||
                    (offer.SteamItemId == 0) != (offer.SteamPrices.Count == 0))
                    return ValidateOptionsResult.Fail("Steam offers require a unique item ID and at least one currency price.");

                foreach (var (currency, price) in offer.SteamPrices)
                {
                    if (IsCurrency(currency) == false || price <= 0 || int.MaxValue < price || price % GetPriceStep(currency) != 0)
                        return ValidateOptionsResult.Fail("Steam offer prices must be positive integers in valid currency increments.");
                }
            }

            return ValidateOptionsResult.Success;
        }

        private bool IsIdentifier(string value)
        {
            if (value.Length == 0 || 64 < value.Length)
                return false;

            foreach (var character in value)
            {
                if (char.IsAsciiLetterOrDigit(character) == false && character != '-' && character != '_')
                    return false;
            }

            return true;
        }

        private bool IsCurrency(string currency)
        {
            return currency.Length == 3 && char.IsAsciiLetterUpper(currency[0]) &&
                char.IsAsciiLetterUpper(currency[1]) && char.IsAsciiLetterUpper(currency[2]);
        }

        private bool IsHeaderValue(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            foreach (var character in value)
            {
                if (character <= ' ' || '~' < character || character == ',')
                    return false;
            }

            return true;
        }

        private int GetPriceStep(string currency)
        {
            return currency switch
            {
                "CLP" or "COP" or "IDR" or "INR" or "JPY" or "KZT" or "TWD" or "UAH" or "UYU" => 100,
                "CRC" => 500,
                "KRW" => 1000,
                "VND" => 50000,
                _ => 1,
            };
        }
    }
}
