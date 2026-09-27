using Server.Infrastructure.Mongo.Players;

namespace Server.Infrastructure.Players
{
    internal sealed class PlayerUpdateResult
    {
        public PlayerUpdateResult(PlayerProfileDocument? profile, bool conflict, List<string> errors)
        {
            Profile = profile;
            Conflict = conflict;
            Errors = errors;
        }

        public PlayerProfileDocument? Profile { get; }

        public bool Conflict { get; }

        public List<string> Errors { get; }

        public bool Succeeded => Conflict == false && Errors.Count == 0 && Profile != null;
    }
}
