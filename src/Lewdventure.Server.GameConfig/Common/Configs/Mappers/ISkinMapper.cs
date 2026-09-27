namespace Server.Configs
{
    public interface ISkinMapper : IConfigMapper
    {
        public int Id { get; }

        public string ArtName { get; }
    }
}
