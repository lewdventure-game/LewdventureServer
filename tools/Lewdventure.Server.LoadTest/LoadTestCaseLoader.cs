using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Server.LoadTest
{
    internal sealed class LoadTestCaseLoader
    {
        private const string CasesRelativePath = "tests/Lewdventure.Server.GoldenTests/Golden/Cases";
        private const string RequestFileName = "request.json";
        private const string ResponsePrefix = "seed-";
        private const string ResponseSuffix = ".response.json";

        public string ResolveCasesPath(string configuredPath)
        {
            if (string.IsNullOrEmpty(configuredPath) == false)
                return Path.GetFullPath(configuredPath);

            var directory = new DirectoryInfo(Directory.GetCurrentDirectory());

            while (directory != null)
            {
                var candidate = Path.Combine(directory.FullName, CasesRelativePath);

                if (Directory.Exists(candidate))
                    return candidate;

                directory = directory.Parent;
            }

            throw new DirectoryNotFoundException($"Golden cases were not found. Pass --cases <path to {CasesRelativePath}>.");
        }

        public List<LoadTestRequest> Load(string casesPath)
        {
            var requests = new List<LoadTestRequest>();
            var caseDirectories = Directory.GetDirectories(casesPath);

            Array.Sort(caseDirectories, StringComparer.Ordinal);

            for (int i = 0; i < caseDirectories.Length; i++)
                AddCase(caseDirectories[i], requests);

            return requests;
        }

        private void AddCase(string caseDirectory, List<LoadTestRequest> requests)
        {
            var requestPath = Path.Combine(caseDirectory, RequestFileName);

            if (File.Exists(requestPath) == false)
                return;

            if (TryParseObject(File.ReadAllText(requestPath), out var requestObject) == false)
                return;

            var caseName = Path.GetFileName(caseDirectory);
            var responsePaths = Directory.GetFiles(caseDirectory, ResponsePrefix + "*" + ResponseSuffix);

            Array.Sort(responsePaths, StringComparer.Ordinal);

            for (int i = 0; i < responsePaths.Length; i++)
            {
                var fileName = Path.GetFileName(responsePaths[i]);
                var seedText = fileName.Substring(ResponsePrefix.Length, fileName.Length - ResponsePrefix.Length - ResponseSuffix.Length);
                var expectedResponse = File.ReadAllText(responsePaths[i]);

                if (ulong.TryParse(seedText, NumberStyles.None, CultureInfo.InvariantCulture, out var seed) == false)
                    continue;

                if (IsErrorResponse(expectedResponse))
                    continue;

                var replayObject = (JObject)requestObject.DeepClone();

                replayObject["seed"] = new JValue(seed);

                requests.Add(new LoadTestRequest(caseName + "#" + seedText, replayObject.ToString(Formatting.None), expectedResponse));
            }
        }

        private bool TryParseObject(string text, out JObject requestObject)
        {
            requestObject = null!;

            try
            {
                if (JToken.Parse(text) is JObject parsed)
                {
                    requestObject = parsed;

                    return true;
                }
            }
            catch (JsonReaderException)
            {
                return false;
            }

            return false;
        }

        private bool IsErrorResponse(string responseText)
        {
            return TryParseObject(responseText, out var responseObject) == false || responseObject.ContainsKey("error");
        }
    }
}
