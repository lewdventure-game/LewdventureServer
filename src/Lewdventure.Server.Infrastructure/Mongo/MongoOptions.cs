using System.ComponentModel.DataAnnotations;

namespace Server.Infrastructure.Mongo
{
    internal sealed class MongoOptions
    {
        public const string SectionName = "Mongo";

        public bool Enabled { get; set; }

        public string ConnectionString { get; set; } = string.Empty;

        public string DatabaseName { get; set; } = string.Empty;

        public string ApplicationName { get; set; } = "lewdventure-server";

        [Range(1, 120)]
        public int ServerSelectionTimeoutSeconds { get; set; } = 5;

        public bool RequireReplicaSet { get; set; } = true;

        public bool ApplyIndexesOnStartup { get; set; } = true;
    }
}
