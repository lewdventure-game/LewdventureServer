namespace Server.Battles
{
    internal sealed class EquippedEntityRef
    {
        public EquippedEntityRef(string entityType, int entityId)
        {
            EntityType = entityType;
            EntityId = entityId;
        }

        public string EntityType { get; }

        public int EntityId { get; }
    }
}
