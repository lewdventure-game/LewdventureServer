namespace Tests.Golden.Infrastructure
{
    internal sealed class GoldenCase
    {
        private const string ExpectedStatusFileName = "expected-status.txt";

        public GoldenCase(string name, string directory, string requestText)
        {
            Name = name;
            Directory = directory;
            RequestText = requestText;
        }

        public string Name { get; }

        public string Directory { get; }

        public string RequestText { get; }

        public string ExpectedStatusPath => Path.Combine(Directory, ExpectedStatusFileName);

        public int GetExpectedStatusCode()
        {
            if (File.Exists(ExpectedStatusPath) == false)
                return 200;

            return int.Parse(File.ReadAllText(ExpectedStatusPath).Trim());
        }

        public string GetResponsePath(ulong seed)
        {
            return Path.Combine(Directory, $"seed-{seed}.response.json");
        }

        public override string ToString()
        {
            return Name;
        }
    }
}
