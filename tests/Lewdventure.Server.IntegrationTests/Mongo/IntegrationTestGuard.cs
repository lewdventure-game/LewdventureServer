using MongoDB.Driver;

namespace Tests.Integration.Mongo
{
    internal sealed class IntegrationTestGuard
    {
        public const string EnabledVariable = "LEWD_IT_ENABLED";
        public const string MongoVariable = "LEWD_IT_MONGO";
        public const string AllowRemoteVariable = "LEWD_IT_ALLOW_REMOTE";
        public const string DatabasePrefix = "lewdventure_it_";

        private readonly string[] _localHosts = { "localhost", "127.0.0.1", "::1", "[::1]" };

        public void EnsureEnabled()
        {
            if (Environment.GetEnvironmentVariable(EnabledVariable) != "1")
                Assert.Ignore($"integration tests are disabled; set {EnabledVariable}=1");
        }

        public string GetExternalConnectionString()
        {
            return Environment.GetEnvironmentVariable(MongoVariable) ?? string.Empty;
        }

        public void EnsureLocal(string connectionString)
        {
            if (Environment.GetEnvironmentVariable(AllowRemoteVariable) == "1")
                return;

            var url = MongoUrl.Create(connectionString);

            foreach (var server in url.Servers)
            {
                if (Array.IndexOf(_localHosts, server.Host) < 0)
                    throw new InvalidOperationException($"integration tests refuse non-local MongoDB host {server.Host}; set {AllowRemoteVariable}=1 to allow");
            }
        }

        public string CreateDatabaseName()
        {
            return DatabasePrefix + Guid.NewGuid().ToString("N").Substring(0, 8);
        }

        public bool IsDisposableDatabase(string databaseName)
        {
            return databaseName.StartsWith(DatabasePrefix, StringComparison.Ordinal);
        }
    }
}
