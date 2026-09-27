using System.IdentityModel.Tokens.Jwt;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Server.Api.Security;
using Server.Infrastructure.Players;

namespace Tests.Unit.Api
{
    [TestFixture]
    public sealed class AuthTokenTests
    {
        private const string SigningKey = "unit-test-auth-signing-key-0123456789";

        [Test]
        public void Issue_PutsUserIdAndExpiryIntoToken()
        {
            var timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 9, 26, 10, 0, 0, TimeSpan.Zero));
            var issuer = new AccessTokenIssuer(CreateOptions(), timeProvider);

            var token = issuer.Issue("usr_test");
            var parsed = new JwtSecurityTokenHandler().ReadJwtToken(token.Value);

            Assert.That(parsed.Subject, Is.EqualTo("usr_test"));
            Assert.That(parsed.Issuer, Is.EqualTo("lewdventure-server"));
            Assert.That(parsed.Audiences, Does.Contain("lewdventure-client"));
            Assert.That(token.ExpiresAt, Is.EqualTo(timeProvider.GetUtcNow().UtcDateTime.AddMinutes(60)));
        }

        [Test]
        public void Validator_WithShortKey_Fails()
        {
            var options = new AuthOptions { Enabled = true, SigningKey = "short" };

            var result = new AuthOptionsValidator().Validate(null, options);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.FailureMessage, Does.Contain("Auth:SigningKey"));
        }

        [Test]
        public void Validator_WhenDisabled_Succeeds()
        {
            var result = new AuthOptionsValidator().Validate(null, new AuthOptions());

            Assert.That(result.Succeeded, Is.True);
        }

        private IOptions<AuthOptions> CreateOptions()
        {
            return Options.Create(new AuthOptions { Enabled = true, SigningKey = SigningKey });
        }
    }
}
