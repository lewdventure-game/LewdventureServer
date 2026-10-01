namespace Server.Battles
{
    internal sealed class SetBonusSkillActionExecutor : ISkillActionExecutor
    {
        private const string BonusParameter = "bonus_id";

        private readonly ICoreLog _coreLog;
        private readonly ISkillArgumentReader _skillArgumentReader;

        public SetBonusSkillActionExecutor(ICoreLog coreLog, ISkillArgumentReader skillArgumentReader)
        {
            _coreLog = coreLog;
            _skillArgumentReader = skillArgumentReader;
        }

        public SkillActionType ActionType => SkillActionType.SetBonus;

        public string TypeKey => "set_bonus";

        public void Execute(SkillActionContext context)
        {
            if (_skillArgumentReader.TryReadInt(context.Component, BonusParameter, context.SkillLevel, out var bonusId) == false || bonusId <= 0)
            {
                _coreLog.Error("[Config]: Skill set_bonus action has no readable bonus_id parameter");

                return;
            }

            var executionContext = context.ExecutionContext;
            var rewards = new List<BattleReward>
            {
                new BattleReward(BattleRewardType.Bonus, bonusId, 1),
            };

            executionContext.BattleRewardService.Apply(
                rewards,
                executionContext.Actor,
                context.Target,
                executionContext.Attacker,
                executionContext.Defender,
                context.Commands,
                executionContext.CurrentTurn);
        }
    }
}
