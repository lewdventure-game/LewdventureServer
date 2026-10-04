using System.Diagnostics.CodeAnalysis;
using Server.Infrastructure.Mongo.Experiments;

namespace Server.Infrastructure.Experiments
{
    internal sealed class ExperimentRegistry
    {
        private IReadOnlyList<ExperimentDocument> _running = Array.Empty<ExperimentDocument>();

        public IReadOnlyList<ExperimentDocument> Running => Volatile.Read(ref _running);

        public void Replace(IReadOnlyList<ExperimentDocument> running)
        {
            Interlocked.Exchange(ref _running, running);
        }

        public bool TryGetActiveGroup(string experimentId, string groupId, [NotNullWhen(true)] out ExperimentGroupDocument? group)
        {
            var running = Running;

            for (int i = 0; i < running.Count; i++)
            {
                if (string.Equals(running[i].Id, experimentId, StringComparison.Ordinal) == false)
                    continue;

                var groups = running[i].Groups;

                for (int j = 0; j < groups.Count; j++)
                {
                    if (string.Equals(groups[j].Id, groupId, StringComparison.Ordinal) == false)
                        continue;

                    if (string.Equals(groups[j].Status, ExperimentGroupDocument.RemovedStatus, StringComparison.Ordinal))
                        break;

                    group = groups[j];

                    return true;
                }

                break;
            }

            group = null;

            return false;
        }

        public void CollectCandidates(bool isNewPlayer, string country, List<string> seenExperimentIds, List<ExperimentCandidate> candidates)
        {
            var running = Running;

            for (int i = 0; i < running.Count; i++)
            {
                var experiment = running[i];

                if (seenExperimentIds.Contains(experiment.Id))
                    continue;

                for (int j = 0; j < experiment.Groups.Count; j++)
                {
                    var group = experiment.Groups[j];

                    if (IsRecruiting(group, isNewPlayer, country))
                        candidates.Add(new ExperimentCandidate(experiment.Id, group));
                }
            }
        }

        private bool IsRecruiting(ExperimentGroupDocument group, bool isNewPlayer, string country)
        {
            if (string.Equals(group.Status, ExperimentGroupDocument.RecruitingStatus, StringComparison.Ordinal) == false)
                return false;

            if (group.Filter.NewPlayersOnly && isNewPlayer == false)
                return false;

            return group.Filter.Countries.Count == 0 || group.Filter.Countries.Contains(country);
        }
    }
}
