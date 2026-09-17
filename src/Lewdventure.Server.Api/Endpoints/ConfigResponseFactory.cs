using Server.GameConfigs;
using Server.Infrastructure.Mongo.ConfigSnapshots;

namespace Server.Api.Endpoints
{
    internal sealed class ConfigResponseFactory
    {
        private readonly ConfigSnapshotHasher _configSnapshotHasher;

        public ConfigResponseFactory(ConfigSnapshotHasher configSnapshotHasher)
        {
            _configSnapshotHasher = configSnapshotHasher;
        }

        public IResult CreatePublishResult(ConfigPublishResult result)
        {
            var changes = new List<object>();

            for (int i = 0; i < result.Changes.Count; i++)
            {
                var change = result.Changes[i];

                if (change.HasChanges == false)
                    continue;

                changes.Add(new
                {
                    domain = change.Domain,
                    rowsBefore = change.RowsBefore,
                    rowsAfter = change.RowsAfter,
                    added = change.Added,
                    removed = change.Removed,
                    changed = change.Changed,
                });
            }

            var body = new
            {
                succeeded = result.Succeeded,
                version = result.Version,
                shortVersion = _configSnapshotHasher.ToShortVersion(result.Version),
                previousVersion = result.PreviousVersion,
                stored = result.Stored,
                activated = result.Activated,
                errors = result.Errors,
                warnings = result.Warnings,
                changes,
            };

            return result.Succeeded ? Results.Ok(body) : Results.UnprocessableEntity(body);
        }

        public object CreateStatus(GameConfigSet current, string activeVersion)
        {
            return new
            {
                loadedVersion = current.Version,
                loadedShortVersion = _configSnapshotHasher.ToShortVersion(current.Version),
                source = current.Source,
                loadedAt = current.LoadedAt,
                activeVersion,
                inSync = string.IsNullOrEmpty(activeVersion) || string.Equals(activeVersion, current.Version, StringComparison.Ordinal),
            };
        }
    }
}
