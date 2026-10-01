namespace Server.Battles
{
    internal sealed class AllyHealthLowerTriggerEvaluator : ISkillTriggerEvaluator
    {
        private const string HealthParameter = "health";
        private const string InstantParameter = "is_instant_activation";

        private readonly ICoreLog _coreLog;
        private readonly ISkillArgumentReader _skillArgumentReader;

        public AllyHealthLowerTriggerEvaluator(ICoreLog coreLog, ISkillArgumentReader skillArgumentReader)
        {
            _coreLog = coreLog;
            _skillArgumentReader = skillArgumentReader;
        }

        public SkillTriggerType TriggerType => SkillTriggerType.AllyHealthLower;

        public string TypeKey => "ally_health_lower";

        public bool CanActivate(SkillComponent component, SkillActivationContext context)
        {
            if (_skillArgumentReader.TryReadFloat(component, HealthParameter, context.SkillLevel, out var threshold) == false)
            {
                _coreLog.Error($"[Config]: Skill ally_health_lower has no readable health parameter, unitId = {context.Owner.Id}");

                return false;
            }

            if (context.IsInstantCheck && AllowsInstantActivation(component, context.SkillLevel) == false)
                return false;

            if (IsAnyAllyBelowThreshold(context, threshold) == false)
            {
                context.RuntimeState.IsHealthThresholdArmed = true;

                return false;
            }

            return context.RuntimeState.IsHealthThresholdArmed;
        }

        public void OnActivated(SkillComponent component, SkillActivationContext context)
        {
            context.RuntimeState.IsHealthThresholdArmed = false;
        }

        public bool AllowsInstantActivation(SkillComponent component, int skillLevel)
        {
            return _skillArgumentReader.ReadFlag(component, InstantParameter, skillLevel);
        }

        private bool IsAnyAllyBelowThreshold(SkillActivationContext context, float threshold)
        {
            var mainUnits = context.OwnerTeam.MainUnits;

            for (int i = 0; i < mainUnits.Count; i++)
            {
                var unit = mainUnits[i];
                var characteristics = unit.CharacteristicState;

                if (characteristics.MaxHealth <= 0f || unit.IsAlive() == false)
                    continue;

                if (characteristics.Health / characteristics.MaxHealth <= threshold)
                    return true;
            }

            return false;
        }
    }
}
