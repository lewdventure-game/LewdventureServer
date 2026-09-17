using Tests.Golden.Infrastructure;

namespace Tests.Golden
{
    [TestFixture]
    [Category("Golden")]
    public sealed class GoldenOrderIndependenceTests
    {
        [Test]
        public async Task ReplayAllCasesInReverseOrder_MatchesGolden()
        {
            using var host = new GoldenTestHost();

            if (host.Settings.IsUpdateMode)
                Assert.Ignore("[Golden] update mode");

            var cases = host.Catalog.LoadAll();
            var seed = 42UL;

            for (int i = cases.Count - 1; 0 <= i; i--)
            {
                var response = await host.ReplayAsync(cases[i], seed);

                host.Verifier.Verify(cases[i], seed, response);
            }
        }
    }
}
