namespace Tests.Unit.Api
{
    internal sealed class ApiDirectoryLocator
    {
        public string Find()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);

            while (directory != null)
            {
                var candidate = Path.Combine(directory.FullName, "src", "Lewdventure.Server.Api");

                if (File.Exists(Path.Combine(candidate, "appsettings.json")))
                    return candidate;

                directory = directory.Parent;
            }

            throw new InvalidOperationException("src/Lewdventure.Server.Api not found");
        }
    }
}
