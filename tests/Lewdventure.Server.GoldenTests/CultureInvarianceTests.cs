using System.Globalization;
using Tests.Golden.Infrastructure;

namespace Tests.Golden
{
    [TestFixture]
    [Category("Golden")]
    [NonParallelizable]
    public sealed class CultureInvarianceTests
    {
        [TestCase("ru-RU")]
        [TestCase("de-DE")]
        [TestCase("en-US")]
        public async Task ReplayUnderCulture_MatchesGolden(string cultureName)
        {
            var previousCulture = CultureInfo.CurrentCulture;
            var previousUiCulture = CultureInfo.CurrentUICulture;
            var previousDefaultCulture = CultureInfo.DefaultThreadCurrentCulture;
            var previousDefaultUiCulture = CultureInfo.DefaultThreadCurrentUICulture;
            var culture = CultureInfo.GetCultureInfo(cultureName);

            try
            {
                CultureInfo.DefaultThreadCurrentCulture = culture;
                CultureInfo.DefaultThreadCurrentUICulture = culture;
                CultureInfo.CurrentCulture = culture;
                CultureInfo.CurrentUICulture = culture;

                using var host = new GoldenTestHost();

                if (host.Settings.IsUpdateMode)
                    Assert.Ignore("[Golden] update mode");

                var cases = host.Catalog.LoadAll();
                var seed = 1337UL;

                for (int i = 0; i < cases.Count; i++)
                {
                    var response = await host.ReplayAsync(cases[i], seed);

                    host.Verifier.Verify(cases[i], seed, response);
                }
            }
            finally
            {
                CultureInfo.DefaultThreadCurrentCulture = previousDefaultCulture;
                CultureInfo.DefaultThreadCurrentUICulture = previousDefaultUiCulture;
                CultureInfo.CurrentCulture = previousCulture;
                CultureInfo.CurrentUICulture = previousUiCulture;
            }
        }
    }
}
