using Server.Balance;

namespace Server.Api.Balance
{
    internal sealed class BalanceJobProgress : IBalanceProgress
    {
        private int _done;
        private int _total;

        public int Done => Volatile.Read(ref _done);

        public int Total => Volatile.Read(ref _total);

        public void Report(int done, int total)
        {
            Volatile.Write(ref _total, total);
            Volatile.Write(ref _done, done);
        }
    }
}
