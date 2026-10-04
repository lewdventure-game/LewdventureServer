using Server.Infrastructure.Experiments;
using Server.Infrastructure.Mongo.Experiments;

namespace Server.Api.Endpoints
{
    internal sealed class ExperimentResponseFactory
    {
        public ExperimentDocument CreateDraft(ExperimentCreateRequest request)
        {
            var draft = new ExperimentDocument
            {
                Id = request.Id.Trim(),
                Name = request.Name.Trim(),
                Description = request.Description.Trim(),
            };

            for (int i = 0; i < request.Groups.Count; i++)
            {
                var group = request.Groups[i];
                var countries = new List<string>(group.Countries.Count);

                for (int j = 0; j < group.Countries.Count; j++)
                    countries.Add(group.Countries[j].Trim().ToUpperInvariant());

                draft.Groups.Add(new ExperimentGroupDocument
                {
                    Id = group.Id.Trim(),
                    Name = group.Name.Trim(),
                    SnapshotVersion = group.SnapshotVersion.Trim(),
                    Percent = group.Percent,
                    Filter = new ExperimentFilterDocument
                    {
                        NewPlayersOnly = group.NewPlayersOnly,
                        Countries = countries,
                    },
                });
            }

            return draft;
        }

        public ExperimentResponse Create(ExperimentSummary summary)
        {
            var response = Create(summary.Experiment);

            for (int i = 0; i < response.Groups.Count; i++)
            {
                if (summary.Participants.TryGetValue(response.Groups[i].Id, out var participants))
                    response.Groups[i].Participants = participants;
            }

            return response;
        }

        public ExperimentResponse Create(ExperimentDocument experiment)
        {
            var response = new ExperimentResponse
            {
                Id = experiment.Id,
                Name = experiment.Name,
                Description = experiment.Description,
                Status = experiment.Status,
                CreatedAt = experiment.CreatedAt,
                StartedAt = experiment.StartedAt,
                FinishedAt = experiment.FinishedAt,
                UpdatedBy = experiment.UpdatedBy,
            };

            for (int i = 0; i < experiment.Groups.Count; i++)
            {
                var group = experiment.Groups[i];

                response.Groups.Add(new ExperimentGroupResponse
                {
                    Id = group.Id,
                    Name = group.Name,
                    SnapshotVersion = group.SnapshotVersion,
                    Percent = group.Percent,
                    NewPlayersOnly = group.Filter.NewPlayersOnly,
                    Countries = new List<string>(group.Filter.Countries),
                    Status = group.Status,
                    FrozenAt = group.FrozenAt,
                    RemovedAt = group.RemovedAt,
                });
            }

            return response;
        }
    }
}
