namespace Server.Battles
{
    internal sealed class EquipmentSnapshot : IEquipmentSnapshot
    {
        private readonly int _id;

        private readonly int _level;

        public int Id => _id;

        public int Level => _level;

        internal EquipmentSnapshot(int id, int level)
        {
            _id = id;
            _level = level;
        }
    }
}
