namespace Server.Battles
{
    internal interface IBattleConstantsReader
    {
        public bool TryGet(string constantKey, out float value);

        public float Get(string constantKey);
    }
}
