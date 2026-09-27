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
        private readonly BattleTurnState _turnState;
        private readonly IBattleCommandFactory _battleCommandFactory;
        private readonly IBattleDamageMath _battleDamageMath;
        private readonly IBattlePerkSimulator _battlePerkSimulator;
        private readonly IBattleScriptBuilder _battleScriptBuilder;
        private readonly IBattleRewardService _battleRewardService;
        private readonly IConfigDistributor _configDistributor;
        private readonly ICoreLog _coreLog;

        public List<BattleStep> Steps => _steps;

        public IUnitState Owner => _owner;

        public ITeamSimulationState Attacker => _attacker;

        public ITeamSimulationState Defender => _defender;

        public int CurrentTurn => _currentTurn;

        public ISeededRandomService SeededRandomService => _seededRandomService;

        public IBattleBonusService BattleBonusService => _battleBonusService;

        public IBattleCommandFactory BattleCommandFactory => _battleCommandFactory;

        public IBattleDamageMath BattleDamageMath => _battleDamageMath;

        public IBattleScriptBuilder BattleScriptBuilder => _battleScriptBuilder;

        public IBattleRewardService BattleRewardService => _battleRewardService;

        public IConfigDistributor ConfigDistributor => _configDistributor;

        public ICoreLog CoreLog => _coreLog;

        public PerkExecutionContext(
            List<BattleStep> steps,
            IUnitState owner,
            ITeamSimulationState attacker,
            ITeamSimulationState defender,
            int currentTurn,
            ISeededRandomService seededRandomService,
            IBattleBonusService battleBonusService,
            BattleTurnState turnState,
            IBattleCommandFactory battleCommandFactory,
            IBattleDamageMath battleDamageMath,
            IBattlePerkSimulator battlePerkSimulator,
            IBattleScriptBuilder battleScriptBuilder,
            IBattleRewardService battleRewardService,
            IConfigDistributor configDistributor,
            ICoreLog coreLog)
        {
            _steps = steps;
            _owner = owner;
            _attacker = attacker;
            _defender = defender;
            _currentTurn = currentTurn;
            _seededRandomService = seededRandomService;
            _battleBonusService = battleBonusService;
            _turnState = turnState;
            _battleCommandFactory = battleCommandFactory;
            _battleDamageMath = battleDamageMath;
            _battlePerkSimulator = battlePerkSimulator;
            _battleScriptBuilder = battleScriptBuilder;
            _battleRewardService = battleRewardService;
            _configDistributor = configDistributor;
            _coreLog = coreLog;
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
                _seededRandomService,
                _turnState);
        }

        public void EmitDeath(IUnitState unit)
        {
            if (_battlePerkSimulator.TryResurrectOnDeath(unit, _steps, _currentTurn, _turnState))
                return;

            _coreLog.Debug($"[Story][Battle]: Death, unitId = {unit.Id}, turn = {_currentTurn}");

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
