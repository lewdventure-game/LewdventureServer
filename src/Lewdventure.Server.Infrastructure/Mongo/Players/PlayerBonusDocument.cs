namespace Server.Infrastructure.Mongo.Players
{
    internal sealed class PlayerBonusDocument
    {
        public int BonusId { get; set; }

        public int Count { get; set; }

        public DateTime GrantedAt { get; set; }
    }
}
