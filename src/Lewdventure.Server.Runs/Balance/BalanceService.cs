using Server.Battles;
using Server.Logging;
using Server.Services;

namespace Server.Balance
{
    internal sealed class BalanceService
    {
        private const string BaseLayer = "база персонажа";
        private const string AccountLayer = "+ бонусы аккаунта";
        private const string EquipmentLayer = "+ снаряжение";
        private const string SummonsLayer = "+ саммоны";
        private const string RunLayer = "+ забег: перки, бонусы, статусы";

        private readonly BalanceSnapshotFactory _balanceSnapshotFactory;
        private readonly BalanceStatistics _balanceStatistics;
        private readonly BattleScriptAnalyzer _battleScriptAnalyzer;

        public BalanceService(BalanceSnapshotFactory balanceSnapshotFactory, BalanceStatistics balanceStatistics, BattleScriptAnalyzer battleScriptAnalyzer)
        {
            _balanceSnapshotFactory = balanceSnapshotFactory;
            _balanceStatistics = balanceStatistics;
            _battleScriptAnalyzer = battleScriptAnalyzer;
        }

        public bool TryBuildCharacteristics(BalanceSetup setup, BalanceTarget target, IConfigDistributor configDistributor, out List<BalanceCharacteristicsLayer> layers, out string error)
        {
            layers = new List<BalanceCharacteristicsLayer>();

            if (configDistributor.Characters.TryGet(setup.CharacterId, out _) == false)
            {
                error = $"Character {setup.CharacterId} is missing in configs.";

                return false;
            }

            var composition = new BattleComposition(configDistributor, new SilentCoreLog());
            var layerSetup = new BalanceSetup { CharacterId = setup.CharacterId, PromoteLevel = setup.PromoteLevel };

            layers.Add(BuildLayer(BaseLayer, layerSetup, target, configDistributor, composition));

            layerSetup.AccountBonuses = setup.AccountBonuses;
            layers.Add(BuildLayer(AccountLayer, layerSetup, target, configDistributor, composition));

            layerSetup.Equipment = setup.Equipment;
            layers.Add(BuildLayer(EquipmentLayer, layerSetup, target, configDistributor, composition));

            layerSetup.Summons = setup.Summons;
            layers.Add(BuildLayer(SummonsLayer, layerSetup, target, configDistributor, composition));

            layerSetup.RunPerks = setup.RunPerks;
            layerSetup.RunBonuses = setup.RunBonuses;
            layerSetup.RunStatuses = setup.RunStatuses;
            layerSetup.CurrentHealth = setup.CurrentHealth;
            layers.Add(BuildLayer(RunLayer, layerSetup, target, configDistributor, composition));

            error = string.Empty;

            return true;
        }

        public bool TryRunBattle(BalanceSetup setup, BalanceTarget target, ulong seed, bool includeLog, IConfigDistributor configDistributor, out BalanceBattleResult result, out string error)
        {
            result = null!;

            var composition = new BattleComposition(configDistributor, new SilentCoreLog());

            if (TryPrepare(setup, target, configDistributor, composition, out var data, out var heroHealthStart, out error) == false)
                return false;

            result = RunBattle(data, target, seed, heroHealthStart, includeLog, composition);

            return true;
        }

        public bool TryRunSeries(BalanceSetup setup, BalanceTarget target, ulong firstSeed, int battles, IConfigDistributor configDistributor, CancellationToken cancellationToken, out BalanceSeriesResult series, out string error)
        {
            series = new BalanceSeriesResult { FirstSeed = firstSeed };

            var composition = new BattleComposition(configDistributor, new SilentCoreLog());

            if (TryPrepare(setup, target, configDistributor, composition, out var data, out var heroHealthStart, out error) == false)
                return false;

            var turns = new List<float>(battles);
            var turnsOnWin = new List<float>(battles);
            var healthOnWin = new List<float>(battles);
            var damagePerTurn = new List<float>(battles);
            var damageTaken = new List<float>(battles);
            var damageBySource = new Dictionary<string, float>(StringComparer.Ordinal);
            var totalDamage = 0f;
            var criticalHits = 0;
            var misses = 0;

            for (int i = 0; i < battles; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var result = RunBattle(data, target, firstSeed + (ulong)i, heroHealthStart, false, composition);

                series.Battles += 1;
                turns.Add(result.Turns);
                damagePerTurn.Add(result.Turns == 0 ? 0f : result.DamageDealt / result.Turns);
                damageTaken.Add(result.DamageTaken);
                totalDamage += result.DamageDealt;
                criticalHits += result.CriticalHits;
                misses += result.Misses;

                foreach (var pair in result.DamageBySource)
                    damageBySource[pair.Key] = (damageBySource.TryGetValue(pair.Key, out var sum) ? sum : 0f) + pair.Value;

                if (result.IsWin)
                {
                    series.Wins += 1;
                    turnsOnWin.Add(result.Turns);
                    healthOnWin.Add(result.HeroHealthEnd);
                }
                else if (series.LossSeeds.Count < 20)
                {
                    series.LossSeeds.Add(result.Seed);
                }
            }

            series.WinRate = series.Battles == 0 ? 0f : (float)series.Wins / series.Battles;
            series.Turns = _balanceStatistics.Distribute(turns);
            series.TurnsOnWin = _balanceStatistics.Distribute(turnsOnWin);
            series.HeroHealthEndOnWin = _balanceStatistics.Distribute(healthOnWin);
            series.DamagePerTurn = _balanceStatistics.Distribute(damagePerTurn);
            series.DamageTaken = _balanceStatistics.Distribute(damageTaken);
            series.CriticalHitsPerBattle = series.Battles == 0 ? 0f : (float)criticalHits / series.Battles;
            series.MissesPerBattle = series.Battles == 0 ? 0f : (float)misses / series.Battles;

            foreach (var pair in damageBySource)
                series.DamageShareBySource[pair.Key] = totalDamage <= 0f ? 0f : pair.Value / totalDamage;

            return true;
        }

        private bool TryPrepare(BalanceSetup setup, BalanceTarget target, IConfigDistributor configDistributor, BattleComposition composition, out BattleSimulationData data, out float heroHealthStart, out string error)
        {
            heroHealthStart = 0f;

            if (_balanceSnapshotFactory.TryBuild(setup, target, configDistributor, composition.BattleParameterParser, out data, out error) == false)
                return false;

            if (composition.BattleSimulationValidator.TryValidate(data, out var validationError) == false)
            {
                error = validationError;

                return false;
            }

            heroHealthStart = 0f < setup.CurrentHealth ? setup.CurrentHealth : BuildState(setup, target, configDistributor, composition).MaxHealth;

            return true;
        }

        private BalanceBattleResult RunBattle(BattleSimulationData data, BalanceTarget target, ulong seed, float heroHealthStart, bool includeLog, BattleComposition composition)
        {
            var replayData = new BattleReplayData
            {
                TeamA = data.TeamA,
                TeamB = data.TeamB,
                StoryLevelId = data.StoryLevelId,
                StageId = data.StageId,
                Seed = seed,
            };
            var script = composition.BattleSimulatorService.Replay(replayData);

            return _battleScriptAnalyzer.Analyze(script, data, heroHealthStart, includeLog);
        }

        private BalanceCharacteristicsLayer BuildLayer(string name, BalanceSetup setup, BalanceTarget target, IConfigDistributor configDistributor, BattleComposition composition)
        {
            return new BalanceCharacteristicsLayer { Name = name, Characteristics = BuildState(setup, target, configDistributor, composition) };
        }

        private ICharacteristicState BuildState(BalanceSetup setup, BalanceTarget target, IConfigDistributor configDistributor, BattleComposition composition)
        {
            var unit = _balanceSnapshotFactory.BuildMainUnit(setup, target, configDistributor);
            var unitState = composition.UnitStateBuilder.Build(unit, BattleSide.Attacking, false, target.StoryLevelId, target.StageId);

            composition.UnitStateBuilder.ApplyTeamSummons(unitState, _balanceSnapshotFactory.CollectSummonIds(setup), unit);

            return unitState.CharacteristicState;
        }
    }
}
