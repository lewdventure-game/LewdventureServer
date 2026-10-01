namespace Server.Battles
{
    internal interface ISkillTargetResolver
    {
        public void Resolve(
            SkillTargetType targetType,
            IUnitState owner,
            ITeamSimulationState ownerTeam,
            ITeamSimulationState opponentTeam,
            List<IUnitState> targets);
    }
}
