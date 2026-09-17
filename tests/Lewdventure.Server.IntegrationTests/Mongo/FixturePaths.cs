namespace Tests.Integration.Mongo
{
    internal sealed class FixturePaths
    {
        public FixturePaths()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);

            while (directory != null && File.Exists(Path.Combine(directory.FullName, "LewdventureServer.slnx")) == false)
                directory = directory.Parent;

            if (directory == null)
                throw new InvalidOperationException("LewdventureServer.slnx not found");

            RepositoryDirectory = directory.FullName;
            ApiDirectory = Path.Combine(RepositoryDirectory, "src", "Lewdventure.Server.Api");
            GoldenDirectory = Path.Combine(RepositoryDirectory, "tests", "Lewdventure.Server.GoldenTests", "Golden");
            FixturePath = Path.Combine(GoldenDirectory, "Fixtures", "config-snapshot.v1.json");
        }

        public string RepositoryDirectory { get; }

        public string ApiDirectory { get; }

        public string GoldenDirectory { get; }

        public string FixturePath { get; }
    }
}
