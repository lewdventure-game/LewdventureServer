using Microsoft.Extensions.Logging;
using Server.Services;

namespace Server.Battles
{
    internal sealed class BattleSimulatorService : IBattleSimulatorService
    {
        private float _comboAttackCooldown;
        private float _counterAttackCooldown;
        private float _perksCooldown;
        private float _statusesCooldown;
        private float _summonsCooldown;
        private float _unitsCooldown;

        private readonly ILogger<BattleSimulatorService> _logger;
        private readonly IBattleAttackService _battleAttackService;
        private readonly IBattleBonusService _battleBonusService;
        private readonly IBattleCommandFactory _battleCommandFactory;
        private readonly IBattlePerkSimulator _battlePerkSimulator;
        private readonly IBattleScriptBuilder _battleScriptBuilder;
        private readonly IBattleStatusSimulator _battleStatusSimulator;
        private readonly IBattleSummonSimulator _battleSummonSimulator;
        private readonly IConfigDistributor _configDistributor;
        private readonly IUnitStateBuilder _unitStateBuilder;

        public BattleSimulatorService(
            ILogger<BattleSimulatorService> logger,
            IBattleAttackService battleAttackService,
            IBattleBonusService battleBonusService,
            IBattleCommandFactory battleCommandFactory,
            IBattlePerkSimulator battlePerkSimulator,
            IBattleScriptBuilder battleScriptBuilder,
            IBattleStatusSimulator battleStatusSimulator,
            IBattleSummonSimulator battleSummonSimulator,
            IConfigDistributor configDistributor,
            IUnitStateBuilder unitStateBuilder)
        {
            _logger = logger;
            _battleAttackService = battleAttackService;
            _battleBonusService = battleBonusService;
            _battleCommandFactory = battleCommandFactory;
            _battlePerkSimulator = battlePerkSimulator;
            _battleScriptBuilder = battleScriptBuilder;
            _battleStatusSimulator = battleStatusSimulator;
            _battleSummonSimulator = battleSummonSimulator;
            _configDistributor = configDistributor;
            _unitStateBuilder = unitStateBuilder;

            CacheConstants();
        }

        public IBattleScriptResponse Simulate(IBattleSimulationData data)
        {
            var seededRandomService = new SeededRandomService();

            _logger.LogDebug($"[Story][Battle] generated seed = {seededRandomService.Seed}");

            return RunSimulation(data, seededRandomService);
        }

        public IBattleScriptResponse Replay(IBattleReplayData data)
        {
            var seededRandomService = new SeededRandomService(data.Seed);

            _logger.LogDebug($"[Story][Battle] replay seed = {seededRandomService.Seed}");

            return RunSimulation(data, seededRandomService);
        }

        private IBattleScriptResponse RunSimulation(IBattleSimulationData data, ISeededRandomService seededRandomService)
        {
            var seed = seededRandomService.Seed;
            var steps = new List<BattleStep>();

            var teamA = data.TeamA;
            var teamB = data.TeamB;
            var maxTurns = CalculateMaxTurns(data.StoryLevelId);

            var stateA = BuildTeamState(teamA, BattleSide.Attacking, data.StoryLevelId, data.StageId);
            var stateB = BuildTeamState(teamB, BattleSide.Defending, data.StoryLevelId, data.StageId);

            _battleSummonSimulator.EmitInitialSpawns(steps, stateA, 0);
            _battleSummonSimulator.EmitInitialSpawns(steps, stateB, 0);
            EmitInitialStatuses(steps, stateA, 0);
            EmitInitialStatuses(steps, stateB, 0);

            var currentTurn = 0;

            while (currentTurn < maxTurns && HasAliveMainUnits(stateA) && HasAliveMainUnits(stateB))
            {
                ApplyTurnStartBonuses(steps, stateA, currentTurn);
                ApplyTurnStartBonuses(steps, stateB, currentTurn);

                SimulateSideTurn(steps, stateA, stateB, currentTurn, seededRandomService);

                if (HasAliveMainUnits(stateB) == false)
                    break;

                SimulateSideTurn(steps, stateB, stateA, currentTurn, seededRandomService);

                if (HasAliveMainUnits(stateA) == false)
                    break;

                ++currentTurn;
            }

            ApplyBattleEndBonuses(steps, stateA, currentTurn);
            ApplyBattleEndBonuses(steps, stateB, currentTurn);

            var outcomeType = ResolveOutcomeType(stateA, stateB, currentTurn, maxTurns);
            var maxTurnFromSteps = GetMaxTurnFromSteps(steps);

            EmitLivingSummonDespawns(steps, stateA, maxTurnFromSteps);
            EmitLivingSummonDespawns(steps, stateB, maxTurnFromSteps);

            _logger.LogInformation($"[Story][Battle] outcomeType = {outcomeType}, maxTurn = {maxTurnFromSteps}");

            _battleScriptBuilder.Add(
                steps,
                maxTurnFromSteps,
                BattlePhaseType.Unknown,
                new List<BattleCommand>
                {
                    _battleCommandFactory.SetBattleResult(outcomeType),
                });

            var response = new BattleScriptResponse
            {
                ProtocolVersion = 1,
                Seed = seed,
                OutcomeType = outcomeType,
                Steps = steps,
            };

            return response;
        }

        private int GetMaxTurnFromSteps(List<BattleStep> steps)
        {
            var maxTurn = 0;

            for (int i = 0; i < steps.Count; i++)
            {
                var turn = steps[i].Turn;

                if (maxTurn < turn)
                    maxTurn = turn;
            }

            return maxTurn;
        }

        private void EmitLivingSummonDespawns(List<BattleStep> steps, ITeamSimulationState team, int turn)
        {
            var summons = team.Summons;

            for (int i = 0; i < summons.Count; i++)
            {
                var summon = summons[i];

                if (summon.IsAlive() == false)
                    continue;

                _battleScriptBuilder.Add(
                    steps,
                    turn,
                    BattlePhaseType.Unknown,
                    summon,
                    new List<BattleCommand>
                    {
                        _battleCommandFactory.DespawnUnit(summon.Id, summon.SlotIndex),
                    });

                _logger.LogDebug($"[Story][Battle] despawn living summon unitId = {summon.Id} slot = {summon.SlotIndex} side = {team.BattleSide}");
            }
        }

        private int CalculateMaxTurns(int storyLevelId)
        {
            if (_configDistributor.StoryLevels.TryGet(storyLevelId, out var storyLevel) == false)
            {
                _logger.LogError($"[Error][Story][Battle] story level missing id = {storyLevelId}");

                return 0;
            }

            return storyLevel.MaxBattleTurns;
        }

        private void CacheConstants()
        {
            _counterAttackCooldown = GetConstant(ConstantKeys.CounterAttackCooldownKey);
            _comboAttackCooldown = GetConstant(ConstantKeys.ComboAttackCooldownKey);
            _perksCooldown = GetConstant(ConstantKeys.PerksCooldownKey);
            _statusesCooldown = GetConstant(ConstantKeys.StatusesCooldownKey);
            _summonsCooldown = GetConstant(ConstantKeys.SummonsCooldownKey);
            _unitsCooldown = GetConstant(ConstantKeys.UnitsCooldownKey);
        }

        private float GetConstant(string constantKey)
        {
            if (_configDistributor.Constants.TryGet(constantKey, out var constant) == false)
            {
                _logger.LogError($"[Error][Story][Battle] constant missing key = {constantKey}");

                return 0f;
            }

            return float.Parse(constant.ConstantValue, System.Globalization.CultureInfo.InvariantCulture);
        }

        private OutcomeType ResolveOutcomeType(
            ITeamSimulationState stateA,
            ITeamSimulationState stateB,
            int currentTurn,
            int maxTurns)
        {
            var aAlive = HasAliveMainUnits(stateA);
            var bAlive = HasAliveMainUnits(stateB);

            if (aAlive && bAlive == false)
                return OutcomeType.TeamAWin;

            if (aAlive == false && bAlive)
                return OutcomeType.TeamBWin;

            if (currentTurn < maxTurns)
                return OutcomeType.Draw;

            return OutcomeType.Timeout;
        }

        private ITeamSimulationState BuildTeamState(ITeamSnapshot teamSnapshot, BattleSide battleSide, int storyLevelId, int stageId)
        {
            var mainUnits = teamSnapshot.MainUnits;
            var mainUnitsCount = mainUnits.Count;
            var mainUnitStates = new List<IUnitState>(mainUnitsCount);

            for (int i = 0; i < mainUnitsCount; i++)
            {
                var mainUnit = mainUnits[i];

                _logger.LogDebug($"[Story][Battle] side = {battleSide} kind = main id = {mainUnit.Id} level = {mainUnit.Level} slot = {mainUnit.SlotIndex}");

                mainUnitStates.Add(_unitStateBuilder.Build(mainUnit, battleSide, false, storyLevelId, stageId));
            }

            var summons = teamSnapshot.Summons;
            var summonsCount = summons.Count;
            var summonStates = new List<IUnitState>(summonsCount);

            for (int i = 0; i < summonsCount; i++)
            {
                var summon = summons[i];

                _logger.LogDebug($"[Story][Battle] side = {battleSide} kind = summon id = {summon.Id} level = {summon.Level} slot = {summon.SlotIndex}");

                summonStates.Add(_unitStateBuilder.Build(summon, battleSide, true, storyLevelId, stageId));
            }

            ApplyTeamSummonBonuses(mainUnitStates, summons, summonStates);

            return new TeamSimulationState(mainUnitStates, summonStates, battleSide);
        }

        private void ApplyTeamSummonBonuses(
            List<IUnitState> mainUnitStates,
            List<IUnitSnapshot> summonSnapshots,
            List<IUnitState> summonStates)
        {
            if (summonStates.Count == 0)
                return;

            var rebuildCommands = new List<BattleCommand>();

            for (int i = 0; i < mainUnitStates.Count; i++)
            {
                var mainUnit = mainUnitStates[i];

                for (int j = 0; j < summonStates.Count; j++)
                {
                    mainUnit.RegisterEquippedEntity("summons", summonStates[j].Id);
                    _unitStateBuilder.GrantSummonAccountBonuses(mainUnit, summonSnapshots[j]);
                }

                _battleBonusService.Rebuild(mainUnit, 0, rebuildCommands, false);
                _logger.LogDebug($"[Story][Battle] team summons applied on main unitId = {mainUnit.Id} summonCount = {summonStates.Count}");
            }
        }

        private bool HasAliveMainUnits(ITeamSimulationState state)
        {
            var mainUnits = state.MainUnits;

            for (int i = 0; i < mainUnits.Count; i++)
                if (mainUnits[i].IsAlive())
                    return true;

            return false;
        }

        private void SimulateSideTurn(
            List<BattleStep> steps,
            ITeamSimulationState attacker,
            ITeamSimulationState defender,
            int currentTurn,
            ISeededRandomService seededRandomService)
        {
            _logger.LogDebug($"[Story][Battle] side turn side = {attacker.BattleSide}, turn = {currentTurn}");

            _battlePerkSimulator.BeginSideTurn();

            SimulateStatuses(steps, attacker, defender, currentTurn, seededRandomService);
            EmitPhaseTrailingWait(steps, currentTurn, BattlePhaseType.StatusTrigger, _statusesCooldown, "statuses", attacker.BattleSide);

            if (_battlePerkSimulator.ShouldAbortSideTurn)
            {
                _logger.LogDebug($"[Story][Battle] side turn abort after statuses side = {attacker.BattleSide} turn = {currentTurn}");

                return;
            }

            _battlePerkSimulator.Simulate(steps, attacker, defender, currentTurn, seededRandomService);
            EmitPhaseTrailingWait(steps, currentTurn, BattlePhaseType.PerkTrigger, _perksCooldown, "perks", attacker.BattleSide);

            if (_battlePerkSimulator.ShouldAbortSideTurn)
            {
                _logger.LogDebug($"[Story][Battle] side turn abort after perks side = {attacker.BattleSide} turn = {currentTurn}");

                return;
            }

            _battleSummonSimulator.Simulate(steps, attacker, defender, currentTurn, seededRandomService);
            EmitPhaseTrailingWait(steps, currentTurn, BattlePhaseType.SummonAttack, _summonsCooldown, "summons", attacker.BattleSide);

            if (_battlePerkSimulator.ShouldAbortSideTurn)
            {
                _logger.LogDebug($"[Story][Battle] side turn abort after summons side = {attacker.BattleSide} turn = {currentTurn}");

                return;
            }

            _battleAttackService.SimulateMainUnits(steps, attacker, defender, currentTurn, seededRandomService);
        }

        private void EmitPhaseTrailingWait(
            List<BattleStep> steps,
            int currentTurn,
            BattlePhaseType phase,
            float cooldownSeconds,
            string phaseName,
            BattleSide side)
        {
            if (cooldownSeconds <= 0f)
            {
                _logger.LogDebug($"[Story][Battle] phase trailing wait skip phase = {phaseName} side = {side} turn = {currentTurn} cooldown = {cooldownSeconds}");

                return;
            }

            _battleScriptBuilder.Add(
                steps,
                currentTurn,
                phase,
                new List<BattleCommand>
                {
                    _battleCommandFactory.Wait(cooldownSeconds),
                });

            _logger.LogDebug($"[Story][Battle] phase trailing wait phase = {phaseName} side = {side} turn = {currentTurn} cooldown = {cooldownSeconds}");
        }

        private void EmitInitialStatuses(List<BattleStep> steps, ITeamSimulationState team, int currentTurn)
        {
            var mainUnits = team.MainUnits;

            for (int i = 0; i < mainUnits.Count; i++)
                _battleStatusSimulator.EmitInitialStatuses(mainUnits[i], steps, currentTurn);

            var summons = team.Summons;

            for (int i = 0; i < summons.Count; i++)
                _battleStatusSimulator.EmitInitialStatuses(summons[i], steps, currentTurn);
        }

        private void SimulateStatuses(
            List<BattleStep> steps,
            ITeamSimulationState ownerTeam,
            ITeamSimulationState opponentTeam,
            int currentTurn,
            ISeededRandomService seededRandomService)
        {
            _battleStatusSimulator.SimulateSide(ownerTeam, opponentTeam, steps, currentTurn, seededRandomService);
        }

        private void ApplyTurnStartBonuses(List<BattleStep> steps, ITeamSimulationState team, int currentTurn)
        {
            ApplyTurnStartBonusesForUnits(steps, team.MainUnits, currentTurn);
            ApplyTurnStartBonusesForUnits(steps, team.Summons, currentTurn);
        }

        private void ApplyTurnStartBonusesForUnits(List<BattleStep> steps, IReadOnlyList<IUnitState> units, int currentTurn)
        {
            for (int i = 0; i < units.Count; i++)
            {
                var unit = units[i];
                var commands = new List<BattleCommand>();

                _battleBonusService.OnTurnStart(unit, currentTurn, commands);

                if (commands.Count == 0)
                    continue;

                _battleScriptBuilder.Add(
                    steps,
                    currentTurn,
                    BattlePhaseType.Unknown,
                    unit,
                    commands,
                    unit);

                _logger.LogDebug($"[Story][Battle] turn start bonus presentation unitId = {unit.Id}, turn = {currentTurn}, commands = {commands.Count}");
            }
        }

        private void ApplyBattleEndBonuses(List<BattleStep> steps, ITeamSimulationState team, int currentTurn)
        {
            ApplyBattleEndBonusesForUnits(steps, team.MainUnits, currentTurn);
            ApplyBattleEndBonusesForUnits(steps, team.Summons, currentTurn);
        }

        private void ApplyBattleEndBonusesForUnits(List<BattleStep> steps, IReadOnlyList<IUnitState> units, int currentTurn)
        {
            for (int i = 0; i < units.Count; i++)
            {
                var unit = units[i];

                _battleStatusSimulator.ExpireNonDamageOverTimeStatusesAtBattleEnd(unit, steps, currentTurn);

                var commands = new List<BattleCommand>();

                _battleBonusService.OnBattleEnd(unit, commands);

                if (commands.Count == 0)
                    continue;

                _battleScriptBuilder.Add(
                    steps,
                    currentTurn,
                    BattlePhaseType.Unknown,
                    unit,
                    commands,
                    unit);

                _logger.LogDebug($"[Story][Battle] battle end bonus presentation unitId = {unit.Id}, turn = {currentTurn}, commands = {commands.Count}");
            }
        }
    }
}

