namespace Server.Configs
{
    internal interface ISkinMapper : IConfigMapper
    {
        public int Id { get; }

        public string ArtName { get; }
    }
}
