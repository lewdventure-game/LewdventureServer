using Server.Infrastructure.Mongo.Players;
using Server.Infrastructure.Mongo.Runs;

namespace Server.Infrastructure.Players
{
    internal sealed class PlayerExport
    {
        public PlayerExport(
            string userId,
            string status,
            DateTime createdAt,
            int devices,
            PlayerProfileDocument? profile,
            List<RunDocument> runs,
            List<PlayerLedgerDocument> ledger)
        {
            UserId = userId;
            Status = status;
            CreatedAt = createdAt;
            Devices = devices;
            Profile = profile;
            Runs = runs;
            Ledger = ledger;
        }

        public string UserId { get; }

        public string Status { get; }

        public DateTime CreatedAt { get; }

        public int Devices { get; }

        public PlayerProfileDocument? Profile { get; }

        public List<RunDocument> Runs { get; }

        public List<PlayerLedgerDocument> Ledger { get; }
    }
}
