using Server.Services;

namespace Tests.Unit.Api
{
    internal sealed class FakeGameConfigService : IGameConfigService
    {
        public int CallCount { get; private set; }

        public Task<(bool Success, string ErrorMessage)> UpdateAllConfigsAsync(bool isDevEnvironment)
        {
            CallCount++;

            return Task.FromResult((true, "fake"));
        }
    }
}
