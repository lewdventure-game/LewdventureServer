using Server.Configs;

namespace Server.Statuses
{
    public interface IStatusMapper : IConfigMapper
    {
        public int Id { get; }

        public StatusType StatusType { get; }

        public int TriggerOrder { get; }

        public string IconArtName { get; }

        public string Parameters { get; }

        public StatusTargetType StatusTarget { get; }
    }
}
