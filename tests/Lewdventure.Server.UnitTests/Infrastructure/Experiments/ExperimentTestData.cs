using Server.Infrastructure.Experiments;
using Server.Infrastructure.Mongo.Experiments;

namespace Tests.Unit.Infrastructure.Experiments
{
    internal sealed class ExperimentTestData
    {
        public const string MasterKey = "master";
        public const int Rolls = 10000;

        public ExperimentDocument CreateRunning(string experimentId, params ExperimentGroupDocument[] groups)
        {
            var experiment = new ExperimentDocument
            {
                Id = experimentId,
                Status = ExperimentDocument.RunningStatus,
            };

            experiment.Groups.AddRange(groups);

            return experiment;
        }

        public ExperimentGroupDocument CreateGroup(string groupId, double percent, bool newPlayersOnly, params string[] countries)
        {
            var group = new ExperimentGroupDocument
            {
                Id = groupId,
                SnapshotVersion = "sha256:" + groupId,
                Percent = percent,
                Status = ExperimentGroupDocument.RecruitingStatus,
            };

            group.Filter.NewPlayersOnly = newPlayersOnly;
            group.Filter.Countries.AddRange(countries);

            return group;
        }

        public Dictionary<string, double> Distribute(ExperimentRegistry registry, bool isNewPlayer, string country)
        {
            var candidates = new List<ExperimentCandidate>();
            var picker = new ExperimentGroupPicker();
            var shares = new Dictionary<string, double>(StringComparer.Ordinal);

            registry.CollectCandidates(isNewPlayer, country, new List<string>(), candidates);

            for (int i = 0; i < Rolls; i++)
            {
                var roll = (i + 0.5d) / Rolls * ExperimentAllocationValidator.MaxPercent;
                var picked = picker.Pick(candidates, roll);
                var key = picked == null ? MasterKey : picked.ExperimentId + "/" + picked.Group.Id;

                shares.TryGetValue(key, out var count);
                shares[key] = count + ExperimentAllocationValidator.MaxPercent / Rolls;
            }

            return shares;
        }
    }
}
