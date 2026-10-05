using Microsoft.Extensions.Logging.Abstractions;
using Server.Infrastructure.Mongo.Players;
using Server.Infrastructure.Qa;

namespace Tests.Unit.Qa
{
    [TestFixture]
    public sealed class QaAliasTests
    {
        private readonly QaAccountService _service = new(NullLogger<QaAccountService>.Instance, TimeProvider.System, new TokenGenerator(), null!);

        [TestCase(" QA-Masha-1 ", "qa-masha-1")]
        [TestCase("tester_2", "tester_2")]
        [TestCase(null, "")]
        public void NormalizeAlias_TrimsAndLowers(string? alias, string expected)
        {
            Assert.That(_service.NormalizeAlias(alias), Is.EqualTo(expected));
        }

        [TestCase("qa-masha-1", true)]
        [TestCase("ab", true)]
        [TestCase("a", false)]
        [TestCase("qa masha", false)]
        [TestCase("кирилл", false)]
        [TestCase("abcdefghijklmnopqrstuvwxyz0123456", false)]
        public void IsValidAlias_ChecksCharactersAndLength(string alias, bool expected)
        {
            Assert.That(_service.IsValidAlias(alias), Is.EqualTo(expected));
        }
    }
}
