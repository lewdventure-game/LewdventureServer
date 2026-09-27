namespace Server.Battles
{
    public interface IBattleRollRecorder
    {
        public void Record(int rollIndex, string rollName, float value);
    }
}
