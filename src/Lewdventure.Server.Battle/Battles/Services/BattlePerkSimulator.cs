using System;
using Microsoft.Extensions.Logging;
using Server.Perks;
using Server.Services;

namespace Server.Battles
{
    internal sealed class BattlePerkSimulator : IBattlePerkSimulator
    {
        private IUnitState _actingUnit;

        private readonly List<int> _abortedUnitIds = new();
        private readonly List<int> _abortedUnitSlots = new();

        private readonly ILogger<BattlePerkSimulator> _logger;
        private readonly IBattleBonusService _battleBonusService;
        private readonly IBattleCommandFactory _battleCommandFactory;
        private readonly IBattleRewardService _battleRewardService;
        private readonly IBattleScriptBuilder _battleScriptBuilder;
        private readonly IConfigDistributor _configDistributor;
        private readonly List<PerkQueueEntry> _queueBuffer = new();

        public bool ShouldSkipRemainingActions(IUnitState unit)
        {
            for (int i = 0; i < _abortedUnitIds.Count; i++)
            {
                if (_abortedUnitIds[i] != unit.Id)
                    continue;

                if (_abortedUnitSlots[i] != unit.SlotIndex)
                    continue;

                return true;
            }

            return false;
        }

        public BattlePerkSimulator(
            ILogger<BattlePerkSimulator> logger,
            IBattleBonusService battleBonusService,
            IBattleCommandFactory battleCommandFactory,
            IBattleRewardService battleRewardService,
            IBattleScriptBuilder battleScriptBuilder,
            IConfigDistributor configDistributor)
        {
            _logger = logger;
            _battleBonusService = battleBonusService;
            _battleCommandFactory = battleCommandFactory;
            _battleRewardService = battleRewardService;
            _battleScriptBuilder = battleScriptBuilder;
            _configDistributor = configDistributor;
        }

        public void BeginBattleTurn()
        {
            ClearAbortedUnits();

            _actingUnit = null;
        }

        public void BeginSideTurn(BattleSide actingSide)
        {
            ClearAbortedUnits();

            _actingUnit = null;
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void SetActingUnit(IUnitState unit)
        {
            _actingUnit = unit;
        }

        public void Simulate(
            List<BattleStep> steps,
            ITeamSimulationState attacker,
            ITeamSimulationState defender,
            int currentTurn,
            ISeededRandomService seededRandomService)
        {
            _queueBuffer.Clear();

            CollectPerks(attacker.MainUnits);
            CollectPerks(attacker.Summons);
            SortQueueByTriggerOrder();

            if (_queueBuffer.Count == 0)
            {
                _logger.LogDebug($"[Story][Battle]: Phase wait skippedEmpty phase = perks, side = {attacker.BattleSide}, turn = {currentTurn}, queueSize = 0, emittedWaits = 0");

                return;
            }

            var cooldown = GetPerksCooldown();
            var emittedWaits = 0;

            _logger.LogDebug($"[Story][Battle]: Perk phase, side = {attacker.BattleSide}, turn = {currentTurn}, queue = {_queueBuffer.Count}");

            for (int i = 0; i < _queueBuffer.Count; i++)
            {
                var entry = _queueBuffer[i];
                var owner = entry.Owner;
                var perk = entry.Perk;

                SetActingUnit(owner);

                if (ShouldSkipRemainingActions(owner))
                {
                    _logger.LogDebug($"[Story][Battle]: Perk queue skip aborted unit, perkId = {perk.Id}, ownerId = {owner.Id}, turn = {currentTurn}");

                    continue;
                }

                if (owner.IsAlive() == false && perk.PerkType != PerkType.Resurrection)
                    continue;

                if (perk.CanTrigger(currentTurn) == false)
                    continue;

                _logger.LogDebug($"[Story][Battle]: Perk queue, perkId = {perk.Id}, type = {perk.PerkType}, order = {perk.TriggerOrder}, ownerId = {owner.Id}, side = {attacker.BattleSide}");

                var stepCountBefore = steps.Count;
                var context = CreateContext(steps, owner, attacker, defender, currentTurn, seededRandomService);
                perk.Trigger(context);

                if (steps.Count == stepCountBefore)
                {
                    _logger.LogDebug($"[Story][Battle]: Perk queue no-op skip cooldown, perkId = {perk.Id}, ownerId = {owner.Id}");

                    continue;
                }

                var waitCommands = new List<BattleCommand>
                {
                    _battleCommandFactory.Wait(cooldown),
                };

                _battleScriptBuilder.Add(
                    steps,
                    currentTurn,
                    BattlePhaseType.PerkTrigger,
                    owner,
                    waitCommands);

                ++emittedWaits;

                _logger.LogDebug($"[Story][Battle]: Phase wait phase = perks, perkId = {perk.Id}, ownerId = {owner.Id}, wait = {cooldown}");
            }

            _logger.LogDebug($"[Story][Battle]: Phase wait phase = perks, side = {attacker.BattleSide}, turn = {currentTurn}, queueSize = {_queueBuffer.Count}, emittedWaits = {emittedWaits}");
        }

        public void NotifyAction(
            BattlePerkActionType actionType,
            IUnitState actor,
            ITeamSimulationState actorTeam,
            ITeamSimulationState opponentTeam,
            List<BattleStep> steps,
            int currentTurn,
            ISeededRandomService seededRandomService)
        {
            var perks = actor.Perks;

            for (int i = 0; i < perks.Count; i++)
            {
                var context = CreateContext(steps, actor, actorTeam, opponentTeam, currentTurn, seededRandomService);
                perks[i].NotifyAction(actionType, context);
            }
        }

        public void NotifyAnyDamage(
            IUnitState damageDealer,
            ITeamSimulationState dealerTeam,
            ITeamSimulationState opponentTeam,
            List<BattleStep> steps,
            int currentTurn,
            ISeededRandomService seededRandomService)
        {
            _logger.LogDebug($"[Story][Battle]: Any_damage notify, dealerId = {damageDealer.Id}, slot = {damageDealer.SlotIndex}, turn = {currentTurn}");

            NotifyAction(
                BattlePerkActionType.AnyDamage,
                damageDealer,
                dealerTeam,
                opponentTeam,
                steps,
                currentTurn,
                seededRandomService);

            var mainUnits = dealerTeam.MainUnits;

            for (int i = 0; i < mainUnits.Count; i++)
            {
                var mainUnit = mainUnits[i];

                if (mainUnit.Id == damageDealer.Id && mainUnit.SlotIndex == damageDealer.SlotIndex)
                    continue;

                if (mainUnit.IsAlive() == false)
                    continue;

                NotifyAction(
                    BattlePerkActionType.AnyDamage,
                    mainUnit,
                    dealerTeam,
                    opponentTeam,
                    steps,
                    currentTurn,
                    seededRandomService);
            }
        }

        public bool TryResurrectOnDeath(IUnitState unit, List<BattleStep> steps, int currentTurn)
        {
            if (0 < unit.CharacteristicState.Health)
                return false;

            var perks = unit.Perks;

            for (int i = 0; i < perks.Count; i++)
            {
                var perk = perks[i];

                if (perk.TryResurrectOnDeath(
                        unit,
                        steps,
                        currentTurn,
                        _battleCommandFactory,
                        _battleScriptBuilder) == false)
                    continue;

                AbortUnit(unit);

                if (_actingUnit != null)
                    AbortUnit(_actingUnit);

                _logger.LogInformation($"[Story][Battle]: Resurrected on death, unitId = {unit.Id}, perkId = {perk.Id}, turn = {currentTurn}");
                _logger.LogInformation($"[Story][Battle]: Abort remaining unit actions after resurrection, unitId = {unit.Id}, actingUnitId = {GetActingUnitId()}, turn = {currentTurn}");

                return true;
            }

            return false;
        }

        private void AbortUnit(IUnitState unit)
        {
            if (ShouldSkipRemainingActions(unit))
                return;

            _abortedUnitIds.Add(unit.Id);
            _abortedUnitSlots.Add(unit.SlotIndex);

            _logger.LogDebug($"[Story][Battle]: Abort unit remaining actions, unitId = {unit.Id}, slot = {unit.SlotIndex}");
        }

        private int GetActingUnitId()
        {
            if (_actingUnit == null)
                return -1;

            return _actingUnit.Id;
        }

        private void ClearAbortedUnits()
        {
            _abortedUnitIds.Clear();
            _abortedUnitSlots.Clear();
        }

        private void CollectPerks(IReadOnlyList<IUnitState> units)
        {
            for (int i = 0; i < units.Count; i++)
            {
                var unit = units[i];
                var perks = unit.Perks;

                for (int j = 0; j < perks.Count; j++)
                    _queueBuffer.Add(new PerkQueueEntry(unit, perks[j]));
            }
        }

        private void SortQueueByTriggerOrder()
        {
            var count = _queueBuffer.Count;

            for (int i = 0; i < count; i++)
            {
                for (int j = 0; j < count - 1 - i; j++)
                {
                    if (_queueBuffer[j].Perk.TriggerOrder <= _queueBuffer[j + 1].Perk.TriggerOrder)
                        continue;

                    (_queueBuffer[j + 1], _queueBuffer[j]) = (_queueBuffer[j], _queueBuffer[j + 1]);
                }
            }
        }

        private PerkExecutionContext CreateContext(
            List<BattleStep> steps,
            IUnitState owner,
            ITeamSimulationState attacker,
            ITeamSimulationState defender,
            int currentTurn,
            ISeededRandomService seededRandomService)
        {
            return new PerkExecutionContext(
                steps,
                owner,
                attacker,
                defender,
                currentTurn,
                seededRandomService,
                _battleBonusService,
                _battleCommandFactory,
                this,
                _battleScriptBuilder,
                _battleRewardService,
                _configDistributor,
                _logger);
        }

        private float GetPerksCooldown()
        {
            if (_configDistributor.Constants.TryGet(ConstantKeys.PerksCooldownKey, out var constant) == false)
            {
                _logger.LogError($"[Error][Story][Battle]: Constant missing key = {ConstantKeys.PerksCooldownKey}");

                throw new InvalidOperationException($"[Error][Story][Battle]: Constant missing key = {ConstantKeys.PerksCooldownKey}");
            }

            return float.Parse(constant.ConstantValue, System.Globalization.CultureInfo.InvariantCulture);
        }

        private readonly struct PerkQueueEntry
        {
            private readonly IUnitState _owner;
            private readonly IPerk _perk;

            internal IUnitState Owner => _owner;

            internal IPerk Perk => _perk;

            internal PerkQueueEntry(IUnitState owner, IPerk perk)
            {
                _owner = owner;
                _perk = perk;
            }
        }
    }
}
