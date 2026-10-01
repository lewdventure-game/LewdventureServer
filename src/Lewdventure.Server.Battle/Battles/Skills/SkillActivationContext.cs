namespace Server.Battles
{
    internal sealed class SkillActivationContext
    {
        private readonly IUnitState _owner;
        private readonly ITeamSimulationState _ownerTeam;
        private readonly ITeamSimulationState _opponentTeam;
        private readonly SkillRuntimeState _runtimeState;
        private readonly int _skillLevel;
        private readonly int _currentTurn;
        private readonly bool _isInstantCheck;

        public SkillActivationContext(
            IUnitState owner,
            ITeamSimulationState ownerTeam,
            ITeamSimulationState opponentTeam,
            SkillRuntimeState runtimeState,
            int skillLevel,
            int currentTurn,
            bool isInstantCheck)
        {
            _owner = owner;
            _ownerTeam = ownerTeam;
            _opponentTeam = opponentTeam;
            _runtimeState = runtimeState;
            _skillLevel = skillLevel;
            _currentTurn = currentTurn;
            _isInstantCheck = isInstantCheck;
        }

        public IUnitState Owner => _owner;

        public ITeamSimulationState OwnerTeam => _ownerTeam;

        public ITeamSimulationState OpponentTeam => _opponentTeam;

        public SkillRuntimeState RuntimeState => _runtimeState;

        public int SkillLevel => _skillLevel;

        public int CurrentTurn => _currentTurn;

        public bool IsInstantCheck => _isInstantCheck;
    }
}
