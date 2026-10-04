using Server.Infrastructure.Experiments;
using Server.Infrastructure.Mongo.Players;
using Server.Infrastructure.Mongo.Runs;

namespace Server.Infrastructure.Players
{
    internal sealed class PlayerConfigVersionResolver
    {
        private readonly ExperimentRegistry _experimentRegistry;
        private readonly RunRepository _runRepository;
        private readonly UserRepository _userRepository;

        public PlayerConfigVersionResolver(ExperimentRegistry experimentRegistry, RunRepository runRepository, UserRepository userRepository)
        {
            _experimentRegistry = experimentRegistry;
            _runRepository = runRepository;
            _userRepository = userRepository;
        }

        public async Task<string> ResolveAsync(string userId, CancellationToken cancellationToken)
        {
            var runVersion = await _runRepository.GetActiveConfigVersionAsync(userId, cancellationToken);

            if (string.IsNullOrEmpty(runVersion) == false)
                return runVersion;

            if (_experimentRegistry.Running.Count == 0)
                return string.Empty;

            var experiment = await _userRepository.GetExperimentAsync(userId, cancellationToken);

            if (experiment == null)
                return string.Empty;

            if (_experimentRegistry.TryGetActiveGroup(experiment.ExperimentId, experiment.GroupId, out var group))
                return group.SnapshotVersion;

            return string.Empty;
        }
    }
}
