using Server.GameConfigs;
using Server.Infrastructure.Experiments;
using Server.Infrastructure.Mongo.Players;
using Server.Infrastructure.Players;

namespace Server.Infrastructure.Analytics
{
    internal sealed class AnalyticsContextResolver
    {
        private readonly ExperimentRegistry _experimentRegistry;
        private readonly IGameConfigSetProvider _gameConfigSetProvider;
        private readonly PlayerConfigVersionResolver _playerConfigVersionResolver;
        private readonly UserRepository _userRepository;

        public AnalyticsContextResolver(
            ExperimentRegistry experimentRegistry,
            IGameConfigSetProvider gameConfigSetProvider,
            PlayerConfigVersionResolver playerConfigVersionResolver,
            UserRepository userRepository)
        {
            _experimentRegistry = experimentRegistry;
            _gameConfigSetProvider = gameConfigSetProvider;
            _playerConfigVersionResolver = playerConfigVersionResolver;
            _userRepository = userRepository;
        }

        public async Task<AnalyticsPlayerContext> ResolveAsync(string userId, CancellationToken cancellationToken)
        {
            var context = new AnalyticsPlayerContext();
            var user = await _userRepository.GetAnalyticsProjectionAsync(userId, cancellationToken);
            var version = await _playerConfigVersionResolver.ResolveAsync(userId, cancellationToken);

            context.ConfigVersion = string.IsNullOrEmpty(version) ? _gameConfigSetProvider.Current.Version : version;

            if (user == null)
                return context;

            context.Country = user.LastCountry;

            if (user.Experiment != null && _experimentRegistry.TryGetActiveGroup(user.Experiment.ExperimentId, user.Experiment.GroupId, out _))
            {
                context.ExperimentId = user.Experiment.ExperimentId;
                context.GroupId = user.Experiment.GroupId;
            }

            return context;
        }
    }
}
