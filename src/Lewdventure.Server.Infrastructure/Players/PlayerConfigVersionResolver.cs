using Server.Infrastructure.Mongo.Runs;

namespace Server.Infrastructure.Players
{
    internal sealed class PlayerConfigVersionResolver
    {
        private readonly RunRepository _runRepository;

        public PlayerConfigVersionResolver(RunRepository runRepository)
        {
            _runRepository = runRepository;
        }

        public async Task<string> ResolveAsync(string userId, CancellationToken cancellationToken)
        {
            return await _runRepository.GetActiveConfigVersionAsync(userId, cancellationToken);
        }
    }
}
