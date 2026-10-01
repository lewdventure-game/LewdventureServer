namespace Server.Battles
{
    internal sealed class SetStatusSkillActionExecutor : ISkillActionExecutor
    {
        private const string StatusParameter = "status_id";

        private readonly ICoreLog _coreLog;
        private readonly ISkillArgumentReader _skillArgumentReader;

        public SetStatusSkillActionExecutor(ICoreLog coreLog, ISkillArgumentReader skillArgumentReader)
        {
            _coreLog = coreLog;
            _skillArgumentReader = skillArgumentReader;
        }

        public SkillActionType ActionType => SkillActionType.SetStatus;

        public string TypeKey => "set_status";

        public void Execute(SkillActionContext context)
        {
            if (_skillArgumentReader.TryReadInt(context.Component, StatusParameter, context.SkillLevel, out var statusId) == false || statusId <= 0)
            {
                _coreLog.Error("[Config]: Skill set_status action has no readable status_id parameter");

                return;
            }

            var executionContext = context.ExecutionContext;
            var rewards = new List<BattleReward>
            {
                new BattleReward(BattleRewardType.Status, statusId, 1),
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
