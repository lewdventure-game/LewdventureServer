using System.Globalization;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Tests.Golden.Infrastructure
{
    internal sealed class GoldenVerifier
    {
        private readonly UTF8Encoding _encoding = new(false);
        private readonly GoldenPaths _goldenPaths;
        private readonly GoldenSettings _goldenSettings;

        public GoldenVerifier(GoldenPaths goldenPaths, GoldenSettings goldenSettings)
        {
            _goldenPaths = goldenPaths;
            _goldenSettings = goldenSettings;
        }

        public void Verify(GoldenCase goldenCase, ulong seed, GoldenResponse response)
        {
            var responsePath = goldenCase.GetResponsePath(seed);

            if (_goldenSettings.IsUpdateMode)
            {
                Update(goldenCase, responsePath, response);

                return;
            }

            if (File.Exists(responsePath) == false)
                Assert.Fail($"[Golden] missing golden file case = {goldenCase.Name} seed = {seed}; run with LEWD_GOLDEN_UPDATE=1 in a dedicated change");

            Assert.That(response.StatusCode, Is.EqualTo(goldenCase.GetExpectedStatusCode()), $"[Golden] status mismatch case = {goldenCase.Name} seed = {seed} body = {response.Body}");

            var expected = File.ReadAllText(responsePath, _encoding);

            if (string.Equals(expected, response.Body, StringComparison.Ordinal))
                return;

            var actualPath = WriteActual(goldenCase, seed, response.Body);

            Assert.Fail($"[Golden] body mismatch case = {goldenCase.Name} seed = {seed} {DescribeDifference(expected, response.Body)} actual = {actualPath}");
        }

        private void Update(GoldenCase goldenCase, string responsePath, GoldenResponse response)
        {
            File.WriteAllText(responsePath, response.Body, _encoding);

            if (response.StatusCode == 200)
            {
                if (File.Exists(goldenCase.ExpectedStatusPath))
                    File.Delete(goldenCase.ExpectedStatusPath);

                return;
            }

            File.WriteAllText(goldenCase.ExpectedStatusPath, response.StatusCode.ToString(CultureInfo.InvariantCulture), _encoding);
        }

        private string WriteActual(GoldenCase goldenCase, ulong seed, string body)
        {
            var directory = Path.Combine(_goldenPaths.DiffDirectory, goldenCase.Name);

            Directory.CreateDirectory(directory);

            var path = Path.Combine(directory, $"seed-{seed}.actual.json");

            File.WriteAllText(path, body, _encoding);

            return path;
        }

        private string DescribeDifference(string expected, string actual)
        {
            var length = Math.Min(expected.Length, actual.Length);
            var index = 0;

            while (index < length && expected[index] == actual[index])
                index++;

            var stepDescription = DescribeFirstDifferentStep(expected, actual);

            return $"firstDifferentChar = {index} expectedLength = {expected.Length} actualLength = {actual.Length} {stepDescription}";
        }

        private string DescribeFirstDifferentStep(string expected, string actual)
        {
            try
            {
                var expectedSteps = JObject.Parse(expected)["steps"] as JArray;
                var actualSteps = JObject.Parse(actual)["steps"] as JArray;

                if (expectedSteps == null || actualSteps == null)
                    return "steps = n/a";

                var count = Math.Min(expectedSteps.Count, actualSteps.Count);

                for (int i = 0; i < count; i++)
                {
                    if (JToken.DeepEquals(expectedSteps[i], actualSteps[i]) == false)
                        return $"firstDifferentStep = {i} expectedStep = {expectedSteps[i].ToString(Formatting.None)} actualStep = {actualSteps[i].ToString(Formatting.None)}";
                }

                return $"stepsEqualPrefix = {count} expectedSteps = {expectedSteps.Count} actualSteps = {actualSteps.Count}";
            }
            catch (JsonReaderException)
            {
                return "steps = unparsable";
            }
        }
    }
}
