namespace Server.Infrastructure.Players
{
    internal readonly struct SceneOwner
    {
        private readonly SceneOwnerKind _kind;
        private readonly int _id;

        public SceneOwner(SceneOwnerKind kind, int id)
        {
            _kind = kind;
            _id = id;
        }

        public SceneOwnerKind Kind => _kind;

        public int Id => _id;
    }
}
