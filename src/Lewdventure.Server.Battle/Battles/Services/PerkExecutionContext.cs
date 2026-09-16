using Microsoft.Extensions.Logging;
using Server.Services;

namespace Server.Battles
{
    internal sealed class PerkExecutionContext : IPerkExecutionContext
    {
        private readonly List<BattleStep> _steps;
        private readonly IUnitState _owner;
        private readonly ITeamSimulationState _attacker;
        private readonly ITeamSimulationState _defender;
        private readonly int _currentTurn;
        private readonly ISeededRandomService _seededRandomService;
        private readonly IBattleBonusService _battleBonusService;
        private readonly IBattleCommandFactory _battleCommandFactory;
        private readonly IBattlePerkSimulator _battlePerkSimulator;
        private readonly IBattleScriptBuilder _battleScriptBuilder;
        private readonly IBattleRewardService _battleRewardService;
        private readonly IConfigDistributor _configDistributor;
        private readonly ILogger _logger;

        public List<BattleStep> Steps => _steps;

        public IUnitState Owner => _owner;

        public ITeamSimulationState Attacker => _attacker;

        public ITeamSimulationState Defender => _defender;

        public int CurrentTurn => _currentTurn;

        public ISeededRandomService SeededRandomService => _seededRandomService;

        public IBattleBonusService BattleBonusService => _battleBonusService;

        public IBattleCommandFactory BattleCommandFactory => _battleCommandFactory;

        public IBattleScriptBuilder BattleScriptBuilder => _battleScriptBuilder;

        public IBattleRewardService BattleRewardService => _battleRewardService;

        public IConfigDistributor ConfigDistributor => _configDistributor;

        public ILogger Logger => _logger;

        public PerkExecutionContext(
            List<BattleStep> steps,
            IUnitState owner,
            ITeamSimulationState attacker,
            ITeamSimulationState defender,
            int currentTurn,
            ISeededRandomService seededRandomService,
            IBattleBonusService battleBonusService,
            IBattleCommandFactory battleCommandFactory,
            IBattlePerkSimulator battlePerkSimulator,
            IBattleScriptBuilder battleScriptBuilder,
            IBattleRewardService battleRewardService,
            IConfigDistributor configDistributor,
            ILogger logger)
        {
            _steps = steps;
            _owner = owner;
            _attacker = attacker;
            _defender = defender;
            _currentTurn = currentTurn;
            _seededRandomService = seededRandomService;
            _battleBonusService = battleBonusService;
            _battleCommandFactory = battleCommandFactory;
            _battlePerkSimulator = battlePerkSimulator;
            _battleScriptBuilder = battleScriptBuilder;
            _battleRewardService = battleRewardService;
            _configDistributor = configDistributor;
            _logger = logger;
        }

        public int FindDefenderTargetIndex()
        {
            var mainUnits = _defender.MainUnits;
            var selectedIndex = -1;
            var selectedSlot = int.MaxValue;

            for (int i = 0; i < mainUnits.Count; i++)
            {
                var unit = mainUnits[i];

                if (unit.IsAlive() == false)
                    continue;

                if (selectedSlot <= unit.SlotIndex)
                    continue;

                selectedIndex = i;
                selectedSlot = unit.SlotIndex;
            }

            return selectedIndex;
        }

        public void NotifyAnyDamage()
        {
            _battlePerkSimulator.NotifyAnyDamage(
                _owner,
                _attacker,
                _defender,
                _steps,
                _currentTurn,
                _seededRandomService);
        }

        public void EmitDeath(IUnitState unit)
        {
            if (_battlePerkSimulator.TryResurrectOnDeath(unit, _steps, _currentTurn))
                return;

            _logger.LogDebug($"[Story][Battle] death unitId = {unit.Id}, turn = {_currentTurn}");

            _battleScriptBuilder.Add(
                _steps,
                _currentTurn,
                BattlePhaseType.Death,
                unit,
                new List<BattleCommand>
                {
                    _battleCommandFactory.KillUnit(unit.Id, unit.SlotIndex),
                    _battleCommandFactory.SetHp(unit.Id, unit.SlotIndex, 0f),
                });
        }
    }
}
