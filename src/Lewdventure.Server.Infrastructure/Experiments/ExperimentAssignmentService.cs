using Server.Infrastructure.Analytics;
using Server.Infrastructure.Mongo.Players;

namespace Server.Infrastructure.Experiments
{
    internal sealed class ExperimentAssignmentService
    {
        private readonly ExperimentGroupPicker _experimentGroupPicker;
        private readonly ExperimentRegistry _experimentRegistry;
        private readonly ILogger<ExperimentAssignmentService> _logger;
        private readonly PlayerAnalytics _playerAnalytics;
        private readonly UserRepository _userRepository;

        public ExperimentAssignmentService(
            ExperimentGroupPicker experimentGroupPicker,
            ExperimentRegistry experimentRegistry,
            ILogger<ExperimentAssignmentService> logger,
            PlayerAnalytics playerAnalytics,
            UserRepository userRepository)
        {
            _experimentGroupPicker = experimentGroupPicker;
            _experimentRegistry = experimentRegistry;
            _logger = logger;
            _playerAnalytics = playerAnalytics;
            _userRepository = userRepository;
        }

        public async Task AssignAsync(UserDocument user, bool isNewPlayer, string country, DateTime now, CancellationToken cancellationToken)
        {
            if (user.Qa != null || IsInActiveGroup(user))
                return;

            var candidates = new List<ExperimentCandidate>();

            _experimentRegistry.CollectCandidates(isNewPlayer, country, user.ExperimentsSeen, candidates);

            if (candidates.Count == 0)
                return;

            var picked = _experimentGroupPicker.Pick(candidates, Random.Shared.NextDouble() * ExperimentAllocationValidator.MaxPercent);
            var seen = CollectExperimentIds(candidates);
            var assignment = picked == null ? null : new UserExperimentDocument
            {
                ExperimentId = picked.ExperimentId,
                GroupId = picked.Group.Id,
                AssignedAt = now,
                Country = country,
            };

            var updated = await _userRepository.UpdateExperimentAsync(user.Id, user.Experiment, assignment, seen, now, cancellationToken);

            if (updated == false)
            {
                _logger.LogInformation("[Experiment] assignment skipped, user changed concurrently userId = {UserId}", user.Id);

                return;
            }

            if (assignment != null)
            {
                user.Experiment = assignment;
                _playerAnalytics.TrackExperimentAssigned(user.Id, assignment, isNewPlayer);
                _logger.LogInformation("[Experiment] assigned userId = {UserId} experiment = {ExperimentId} group = {GroupId} country = {Country} newPlayer = {NewPlayer}", user.Id, assignment.ExperimentId, assignment.GroupId, country, isNewPlayer);
            }

            for (int i = 0; i < seen.Count; i++)
            {
                if (user.ExperimentsSeen.Contains(seen[i]) == false)
                    user.ExperimentsSeen.Add(seen[i]);
            }
        }

        private bool IsInActiveGroup(UserDocument user)
        {
            if (user.Experiment == null)
                return false;

            return _experimentRegistry.TryGetActiveGroup(user.Experiment.ExperimentId, user.Experiment.GroupId, out _);
        }

        private List<string> CollectExperimentIds(List<ExperimentCandidate> candidates)
        {
            var experimentIds = new List<string>();

            for (int i = 0; i < candidates.Count; i++)
            {
                if (experimentIds.Contains(candidates[i].ExperimentId) == false)
                    experimentIds.Add(candidates[i].ExperimentId);
            }

            return experimentIds;
        }
    }
}
