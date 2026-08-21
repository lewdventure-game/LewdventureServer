
namespace Server.Services
{
    internal interface IGameConfigService
    {
        public Task<(bool Success, string ErrorMessage)> UpdateAllConfigsAsync(bool isDevEnvironment);
    }
}
