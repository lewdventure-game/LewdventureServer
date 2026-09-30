using System;
using Server.Services;

namespace Server.Battles
{
    internal sealed class BattleSimulatorService : IBattleSimulatorService
    {
        private readonly IBattleTeamQuery _battleTeamQuery;
        private readonly ICoreLog _coreLog;
        private readonly IBattleAttackService _battleAttackService;
        private readonly IBattleBonusService _battleBonusService;
        private readonly IBattleCommandFactory _battleCommandFactory;
        private readonly IBattlePerkSimulator _battlePerkSimulator;
        private readonly IBattleScriptBuilder _battleScriptBuilder;
        private readonly IBattleStatusSimulator _battleStatusSimulator;
        private readonly IReadOnlyList<IBattleTurnPhase> _turnPhases;
        private readonly IBattleSummonSimulator _battleSummonSimulator;
        private readonly IConfigDistributor _configDistributor;
        private readonly ISeededRandomFactory _seededRandomFactory;
        private readonly IUnitStateBuilder _unitStateBuilder;

        public BattleSimulatorService(
            IBattleTeamQuery battleTeamQuery,
            ICoreLog coreLog,
            IBattleAttackService battleAttackService,
            IBattleBonusService battleBonusService,
            IBattleCommandFactory battleCommandFactory,
            IBattlePerkSimulator battlePerkSimulator,
            IBattleScriptBuilder battleScriptBuilder,
            IBattleStatusSimulator battleStatusSimulator,
            IReadOnlyList<IBattleTurnPhase> turnPhases,
            IBattleSummonSimulator battleSummonSimulator,
            IConfigDistributor configDistributor,
            ISeededRandomFactory seededRandomFactory,
            IUnitStateBuilder unitStateBuilder)
        {
            _battleTeamQuery = battleTeamQuery;
            _coreLog = coreLog;
            _battleAttackService = battleAttackService;
            _battleBonusService = battleBonusService;
            _battleCommandFactory = battleCommandFactory;
            _battlePerkSimulator = battlePerkSimulator;
            _battleScriptBuilder = battleScriptBuilder;
            _battleStatusSimulator = battleStatusSimulator;
            _turnPhases = turnPhases;
            _battleSummonSimulator = battleSummonSimulator;
            _configDistributor = configDistributor;
            _seededRandomFactory = seededRandomFactory;
            _unitStateBuilder = unitStateBuilder;
        }

        public IBattleScriptResponse Simulate(IBattleSimulationData data)
        {
            var seededRandomService = _seededRandomFactory.Create();

            _coreLog.Debug($"[Story][Battle]: Generated seed = {seededRandomService.Seed}");

            return RunSimulation(data, seededRandomService);
        }

        public IBattleScriptResponse Replay(IBattleReplayData data)
        {
            var seededRandomService = _seededRandomFactory.Create(data.Seed);

            _coreLog.Debug($"[Story][Battle]: Replay seed = {seededRandomService.Seed}");

            return RunSimulation(data, seededRandomService);
        }

        private IBattleScriptResponse RunSimulation(IBattleSimulationData data, ISeededRandomService seededRandomService)
        {
            var seed = seededRandomService.Seed;
            var steps = new List<BattleStep>();

            var teamA = data.TeamA;
            var teamB = data.TeamB;
            var maxTurns = CalculateMaxTurns(data.StoryLevelId);

            _coreLog.Information($"[Story][Battle]: Simulate start storyLevelId = {data.StoryLevelId}, maxTurns = {maxTurns}, seed = {seed}");

            var stateA = BuildTeamState(teamA, BattleSide.Attacking, data.StoryLevelId, data.StageId);
            var stateB = BuildTeamState(teamB, BattleSide.Defending, data.StoryLevelId, data.StageId);

            _battleSummonSimulator.EmitInitialSpawns(steps, stateA, 0);
            _battleSummonSimulator.EmitInitialSpawns(steps, stateB, 0);

            EmitInitialStatuses(steps, stateA, 0);
            EmitInitialStatuses(steps, stateB, 0);

            var currentTurn = 0;
            var turnState = new BattleTurnState();

            while (currentTurn < maxTurns && _battleTeamQuery.HasAliveMainUnits(stateA) && _battleTeamQuery.HasAliveMainUnits(stateB))
            {
                turnState.BeginSideTurn();

                ApplyTurnStartBonuses(steps, stateA, currentTurn);
                ApplyTurnStartBonuses(steps, stateB, currentTurn);

                SimulateSideTurn(steps, stateA, stateB, currentTurn, seededRandomService, turnState);

                if (_battleTeamQuery.HasAliveMainUnits(stateB) == false)
                    break;

                SimulateSideTurn(steps, stateB, stateA, currentTurn, seededRandomService, turnState);

                if (_battleTeamQuery.HasAliveMainUnits(stateA) == false)
                    break;

                ++currentTurn;
            }

            ApplyBattleEndBonuses(steps, stateA, currentTurn);
            ApplyBattleEndBonuses(steps, stateB, currentTurn);

            var outcomeType = ResolveOutcomeType(stateA, stateB, currentTurn, maxTurns);

            if (outcomeType == OutcomeType.Timeout)
                EmitTurnLimitDeath(steps, stateA, currentTurn);

            var maxTurnFromSteps = GetMaxTurnFromSteps(steps);

            LogLivingSummonsLeftInScript(stateA);
            LogLivingSummonsLeftInScript(stateB);

            _coreLog.Information($"[Story][Battle]: OutcomeType = {outcomeType}, currentTurn = {currentTurn}, maxTurns = {maxTurns}, maxTurnFromSteps = {maxTurnFromSteps}");

            _battleScriptBuilder.Add(
                steps,
                maxTurnFromSteps,
                BattlePhaseType.Unknown,
                [
                    _battleCommandFactory.SetBattleResult(outcomeType),
                ]);

            var response = new BattleScriptResponse
            {
                ProtocolVersion = 1,
                Seed = seed,
                OutcomeType = outcomeType,
                Steps = steps,
                PerkUsages = CollectPerkUsages(stateA),
            };

            return response;
        }

        private void EmitTurnLimitDeath(List<BattleStep> steps, ITeamSimulationState state, int currentTurn)
        {
            var mainUnits = state.MainUnits;

            for (int i = 0; i < mainUnits.Count; i++)
            {
                var unit = mainUnits[i];

                if (unit.IsAlive() == false)
                    continue;

                unit.CharacteristicState.Health = 0f;

                var commands = new List<BattleCommand>
                {
                    _battleCommandFactory.SetHp(unit.Id, unit.SlotIndex, 0f),
                    _battleCommandFactory.KillUnit(unit.Id, unit.SlotIndex),
                };

                _battleScriptBuilder.Add(
                    steps,
                    currentTurn,
                    BattlePhaseType.Death,
                    unit,
                    commands);

                _coreLog.Information($"[Story][Battle]: Turn limit death, unitId = {unit.Id}, slot = {unit.SlotIndex}, turn = {currentTurn}");
            }
        }

        private List<PerkUsage> CollectPerkUsages(ITeamSimulationState state)
        {
            var usages = new List<PerkUsage>();
            var mainUnits = state.MainUnits;

            for (int i = 0; i < mainUnits.Count; i++)
            {
                var perks = mainUnits[i].Perks;

                for (int perkIndex = 0; perkIndex < perks.Count; perkIndex++)
                {
                    var perk = perks[perkIndex];

                    if (perk.RemainingUses < 0)
                        continue;

                    usages.Add(new PerkUsage
                    {
                        PerkId = perk.Id,
                        UsedCount = perk.UsedCount,
                        RemainingUses = perk.RemainingUses,
                    });

                    _coreLog.Debug($"[Story][Battle]: Perk usage reported, unitId = {mainUnits[i].Id}, perkId = {perk.Id}, used = {perk.UsedCount}, remaining = {perk.RemainingUses}");
                }
            }

            return usages;
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

        private void LogLivingSummonsLeftInScript(ITeamSimulationState team)
        {
            var summons = team.Summons;

            for (int i = 0; i < summons.Count; i++)
            {
                var summon = summons[i];

                if (summon.IsAlive() == false)
                    continue;

                _coreLog.Information($"[Story][Battle]: Living summons left in script unitId = {summon.Id}, slot = {summon.SlotIndex}, side = {team.BattleSide}");
            }
        }

        private int CalculateMaxTurns(int storyLevelId)
        {
            if (_configDistributor.StoryLevels.TryGet(storyLevelId, out var storyLevel) == false)
            {
                _coreLog.Error($"[Error][Story][Battle]: Story level missing id = {storyLevelId}");

                throw new InvalidOperationException($"[Error][Story][Battle]: Story level missing id = {storyLevelId}");
            }

            var maxTurns = storyLevel.MaxBattleTurns;

            if (maxTurns <= 0)
            {
                _coreLog.Error($"[Error][Story][Battle]: Story level max_battle_turns missing or zero id = {storyLevelId}");

                throw new InvalidOperationException($"[Error][Story][Battle]: Story level max_battle_turns missing or zero id = {storyLevelId}");
            }

            return maxTurns;
        }

        private OutcomeType ResolveOutcomeType(
            ITeamSimulationState stateA,
            ITeamSimulationState stateB,
            int currentTurn,
            int maxTurns)
        {
            var aAlive = _battleTeamQuery.HasAliveMainUnits(stateA);
            var bAlive = _battleTeamQuery.HasAliveMainUnits(stateB);

            if (aAlive && bAlive == false)
                return OutcomeType.TeamAWin;

            if (aAlive == false && bAlive)
                return OutcomeType.TeamBWin;

            return currentTurn < maxTurns
                ? OutcomeType.Draw
                : OutcomeType.Timeout;
        }

        private ITeamSimulationState BuildTeamState(ITeamSnapshot teamSnapshot, BattleSide battleSide, int storyLevelId, int stageId)
        {
            var mainUnits = teamSnapshot.MainUnits;
            var mainUnitsCount = mainUnits.Count;
            var mainUnitStates = new List<IUnitState>(mainUnitsCount);

            for (int i = 0; i < mainUnitsCount; i++)
            {
                var mainUnit = mainUnits[i];

                _coreLog.Debug($"[Story][Battle]: Side = {battleSide}, type = main, id = {mainUnit.Id}, level = {mainUnit.Level}, slot = {mainUnit.SlotIndex}");

                mainUnitStates.Add(_unitStateBuilder.Build(mainUnit, battleSide, false, storyLevelId, stageId));
            }

            var summons = teamSnapshot.Summons;
            var summonsCount = summons.Count;
            var summonStates = new List<IUnitState>(summonsCount);

            for (int i = 0; i < summonsCount; i++)
            {
                var summon = summons[i];

                _coreLog.Debug($"[Story][Battle]: Side = {battleSide}, type = summon, id = {summon.Id}, level = {summon.Level}, slot = {summon.SlotIndex}");

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

                _coreLog.Debug($"[Story][Battle]: Team summons applied on main unitId = {mainUnit.Id}, summonCount = {summonStates.Count}");
            }
        }

        private void SimulateSideTurn(
            List<BattleStep> steps,
            ITeamSimulationState attacker,
            ITeamSimulationState defender,
            int currentTurn,
            ISeededRandomService seededRandomService,
            BattleTurnState turnState)
        {
            _coreLog.Debug($"[Story][Battle]: Side turn side = {attacker.BattleSide}, turn = {currentTurn}");

            turnState.BeginSideTurn();

            for (int i = 0; i < _turnPhases.Count; i++)
            {
                var phase = _turnPhases[i];

                phase.Execute(steps, attacker, defender, currentTurn, seededRandomService, turnState);

                if (phase.StopsSideTurnWhenAttackerDead == false || _battleTeamQuery.HasAliveMainUnits(attacker))
                    continue;

                _coreLog.Debug($"[Story][Battle]: Side turn stop after {phase.Name}, no living mains, side = {attacker.BattleSide}, turn = {currentTurn}");

                return;
            }
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

                _coreLog.Debug($"[Story][Battle]: Turn start bonus presentation unitId = {unit.Id}, turn = {currentTurn}, commands = {commands.Count}");
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

                _coreLog.Debug($"[Story][Battle]: Battle end bonus presentation unitId = {unit.Id}, turn = {currentTurn}, commands = {commands.Count}");
            }
        }
    }
}

