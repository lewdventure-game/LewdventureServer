using MongoDB.Driver;

namespace Server.Infrastructure.Mongo
{
    internal sealed class MongoClientFactory
    {
        public IMongoClient Create(MongoOptions options)
        {
            var settings = MongoClientSettings.FromConnectionString(options.ConnectionString);

            settings.ApplicationName = options.ApplicationName;
            settings.ServerSelectionTimeout = TimeSpan.FromSeconds(options.ServerSelectionTimeoutSeconds);

            return new MongoClient(settings);
        }

        public string DescribeHosts(string connectionString)
        {
            var url = MongoUrl.Create(connectionString);
            var hosts = new List<string>();

            foreach (var server in url.Servers)
                hosts.Add(server.ToString());

            return string.Join(",", hosts);
        }
    }
}
