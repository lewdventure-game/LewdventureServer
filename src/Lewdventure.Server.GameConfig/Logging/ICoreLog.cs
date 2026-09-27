namespace Server.Logging
{
    public interface ICoreLog
    {
        public void Debug(string message);

        public void Information(string message);

        public void Warning(string message);

        public void Warning(Exception exception, string message);

        public void Error(string message);

        public void Error(Exception exception, string message);
    }
}
