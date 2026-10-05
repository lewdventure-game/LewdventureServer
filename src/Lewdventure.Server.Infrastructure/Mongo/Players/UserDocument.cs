namespace Server.Infrastructure.Mongo.Players
{
    internal sealed class UserDocument : IMongoDocument
    {
        public const int CurrentSchemaVersion = 1;
        public const string ActiveStatus = "active";
        public const string BannedStatus = "banned";

        public string Id { get; set; } = string.Empty;

        public int SchemaVersion { get; set; } = CurrentSchemaVersion;

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }

        public string Status { get; set; } = ActiveStatus;

        public string StatusReason { get; set; } = string.Empty;

        public List<UserDeviceDocument> Devices { get; set; } = new();

        public List<UserIdentityDocument> Identities { get; set; } = new();

        public string Country { get; set; } = string.Empty;

        public string LastCountry { get; set; } = string.Empty;

        public UserExperimentDocument? Experiment { get; set; }

        public List<string> ExperimentsSeen { get; set; } = new();

        public UserQaDocument? Qa { get; set; }
    }
}
