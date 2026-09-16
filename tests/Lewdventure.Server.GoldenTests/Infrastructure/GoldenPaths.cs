namespace Tests.Golden.Infrastructure
{
    internal sealed class GoldenPaths
    {
        private const string ProjectDirectoryName = "Lewdventure.Server.GoldenTests";
        private const string ProjectFileName = "Lewdventure.Server.GoldenTests.csproj";

        public GoldenPaths()
        {
            ProjectDirectory = FindProjectDirectory();
            RepositoryDirectory = Path.GetFullPath(Path.Combine(ProjectDirectory, "..", ".."));
            ServerProjectDirectory = RepositoryDirectory;
            CasesDirectory = Path.Combine(ProjectDirectory, "Golden", "Cases");
            FixturePath = Path.Combine(ProjectDirectory, "Golden", "Fixtures", "config-snapshot.v1.json");
            DiffDirectory = Path.Combine(ProjectDirectory, "TestResults", "golden-diff");
        }

        public string ProjectDirectory { get; }

        public string RepositoryDirectory { get; }

        public string ServerProjectDirectory { get; }

        public string CasesDirectory { get; }

        public string FixturePath { get; }

        public string DiffDirectory { get; }

        private string FindProjectDirectory()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);

            while (directory != null)
            {
                if (File.Exists(Path.Combine(directory.FullName, ProjectFileName)))
                    return directory.FullName;

                var siblingDirectory = Path.Combine(directory.FullName, "tests", ProjectDirectoryName);

                if (File.Exists(Path.Combine(siblingDirectory, ProjectFileName)))
                    return siblingDirectory;

                directory = directory.Parent;
            }

            throw new InvalidOperationException($"[Golden] project directory with {ProjectFileName} not found from {AppContext.BaseDirectory}");
        }
    }
}
