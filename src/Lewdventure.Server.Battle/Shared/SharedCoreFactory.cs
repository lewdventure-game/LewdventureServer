using Newtonsoft.Json.Linq;
using Server.Battles;
using Server.GameConfigs;

namespace Server.Shared
{
    public sealed class SharedCoreFactory
    {
        private const string BundleConfigsProperty = "Configs";
        private const string BundleNameProperty = "Name";
        private const string BundleContentProperty = "Content";
        private const string SourceKind = "client";

        public SharedCoreResult CreateFromDomains(IReadOnlyList<CoreConfigDomain> domains, ICoreLog coreLog)
        {
            return CreateFromDomains(domains, coreLog, null);
        }

        public SharedCoreResult CreateFromDomains(IReadOnlyList<CoreConfigDomain> domains, ICoreLog coreLog, IBattleRollRecorder? battleRollRecorder)
        {
            var snapshotDomains = new List<ConfigSnapshotDomain>(domains.Count);

            for (int i = 0; i < domains.Count; i++)
                snapshotDomains.Add(new ConfigSnapshotDomain(domains[i].Domain, string.Empty, string.Empty, domains[i].RowsJson));

            var composition = new GameConfigComposition(coreLog);
            var version = composition.ConfigSnapshotHasher.ComputeVersion(snapshotDomains);
            var snapshot = new GameConfigSnapshot(version, default, SourceKind, snapshotDomains);
            var buildResult = composition.GameConfigSetBuilder.Build(snapshot, SourceKind);

            if (buildResult.Succeeded == false)
                return new SharedCoreResult(null, version, buildResult.Errors, buildResult.Warnings);

            var configSet = buildResult.ConfigSet!;
            var battleCore = new BattleCore(configSet.Distributor, coreLog, battleRollRecorder);

            return new SharedCoreResult(new SharedCore(configSet, battleCore), version, buildResult.Errors, buildResult.Warnings);
        }

        public SharedCoreResult CreateFromBundle(string bundleJson, ICoreLog coreLog)
        {
            return CreateFromBundle(bundleJson, coreLog, null);
        }

        public SharedCoreResult CreateFromBundle(string bundleJson, ICoreLog coreLog, IBattleRollRecorder? battleRollRecorder)
        {
            var domains = new List<CoreConfigDomain>();
            var errors = new List<string>();

            ReadBundle(bundleJson, domains, errors);

            if (0 < errors.Count)
                return new SharedCoreResult(null, string.Empty, errors, new List<string>());

            return CreateFromDomains(domains, coreLog, battleRollRecorder);
        }

        private void ReadBundle(string bundleJson, List<CoreConfigDomain> domains, List<string> errors)
        {
            JObject bundle;

            try
            {
                bundle = JObject.Parse(bundleJson);
            }
            catch (Newtonsoft.Json.JsonException exception)
            {
                errors.Add($"Config bundle parse failed: {exception.Message}");

                return;
            }

            if (bundle[BundleConfigsProperty] is JArray configs == false)
            {
                errors.Add($"Config bundle has no {BundleConfigsProperty} array.");

                return;
            }

            for (int i = 0; i < configs.Count; i++)
            {
                if (configs[i] is JObject entry == false)
                    continue;

                var name = entry[BundleNameProperty];
                var content = entry[BundleContentProperty];

                if (name == null || content == null)
                {
                    errors.Add($"Config bundle entry {i} has no {BundleNameProperty} or {BundleContentProperty}.");

                    continue;
                }

                domains.Add(new CoreConfigDomain(name.ToString(), content.ToString()));
            }

            if (domains.Count == 0)
                errors.Add("Config bundle has no domains.");
        }
    }
}
