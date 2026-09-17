namespace Tests.Golden.Infrastructure
{
    internal sealed class GoldenCaseCatalog
    {
        private const string RequestFileName = "request.json";

        private readonly GoldenPaths _goldenPaths;

        public GoldenCaseCatalog(GoldenPaths goldenPaths)
        {
            _goldenPaths = goldenPaths;
        }

        public List<GoldenCase> LoadAll()
        {
            var directories = Directory.GetDirectories(_goldenPaths.CasesDirectory);
            var cases = new List<GoldenCase>(directories.Length);

            Array.Sort(directories, StringComparer.Ordinal);

            for (int i = 0; i < directories.Length; i++)
            {
                var directory = directories[i];
                var requestPath = Path.Combine(directory, RequestFileName);

                if (File.Exists(requestPath) == false)
                    continue;

                cases.Add(new GoldenCase(Path.GetFileName(directory), directory, File.ReadAllText(requestPath)));
            }

            return cases;
        }

        public GoldenCase Load(string name)
        {
            var directory = Path.Combine(_goldenPaths.CasesDirectory, name);

            return new GoldenCase(name, directory, File.ReadAllText(Path.Combine(directory, RequestFileName)));
        }
    }
}
