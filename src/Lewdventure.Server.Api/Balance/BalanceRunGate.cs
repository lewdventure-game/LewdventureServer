namespace Server.Api.Balance
{
    internal sealed class BalanceRunGate
    {
        private readonly SemaphoreSlim _semaphore = new(1, 1);

        public async Task<bool> TryEnterAsync(CancellationToken cancellationToken)
        {
            return await _semaphore.WaitAsync(0, cancellationToken);
        }

        public async Task EnterAsync(CancellationToken cancellationToken)
        {
            await _semaphore.WaitAsync(cancellationToken);
        }

        public void Exit()
        {
            _semaphore.Release();
        }
    }
}
