using Tests.Golden.Infrastructure;

namespace Tests.Golden
{
    internal static class GoldenCaseSource
    {
        public static IEnumerable<TestCaseData> ReplayCases()
        {
            var paths = new GoldenPaths();
            var settings = new GoldenSettings();
            var cases = new GoldenCaseCatalog(paths).LoadAll();

            for (int i = 0; i < cases.Count; i++)
            {
                for (int j = 0; j < settings.Seeds.Count; j++)
                {
                    var seed = settings.Seeds[j];

                    yield return new TestCaseData(cases[i].Name, seed).SetName($"Replay {cases[i].Name} seed {seed}");
                }
            }
        }

        public static IEnumerable<TestCaseData> SuccessfulCases()
        {
            var paths = new GoldenPaths();
            var cases = new GoldenCaseCatalog(paths).LoadAll();

            for (int i = 0; i < cases.Count; i++)
            {
                var goldenCase = cases[i];

                if (File.Exists(goldenCase.GetResponsePath(1UL)) == false)
                    continue;

                if (goldenCase.GetExpectedStatusCode() != 200)
                    continue;

                yield return new TestCaseData(goldenCase.Name).SetName($"RoundTrip {goldenCase.Name}");
            }
        }
    }
}
