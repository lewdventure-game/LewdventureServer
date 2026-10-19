namespace Server.Balance
{
    internal interface IBalanceProgress
    {
        public void Report(int done, int total);
    }
}
