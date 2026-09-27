namespace Server.Battles
{
    public interface IBattleScriptDigest
    {
        public string Compute(IBattleScriptResponse script);
    }
}
