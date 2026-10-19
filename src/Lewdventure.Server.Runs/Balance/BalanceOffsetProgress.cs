namespace Server.Balance
{
    internal sealed class BalanceOffsetProgress : IBalanceProgress
    {
        private readonly IBalanceProgress _inner;
        private readonly int _offset;
        private readonly int _total;

        public BalanceOffsetProgress(IBalanceProgress inner, int offset, int total)
        {
            _inner = inner;
            _offset = offset;
            _total = total;
        }

        public void Report(int done, int total)
        {
            _inner.Report(_offset + done, _total);
        }
    }
}
