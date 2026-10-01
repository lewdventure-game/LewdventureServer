namespace Server.Battles
{
    internal sealed class EnergyNeededTriggerEvaluator : ISkillTriggerEvaluator
    {
        private const string EnergyParameter = "energy";

        private readonly ICoreLog _coreLog;
        private readonly ISkillArgumentReader _skillArgumentReader;

        public EnergyNeededTriggerEvaluator(ICoreLog coreLog, ISkillArgumentReader skillArgumentReader)
        {
            _coreLog = coreLog;
            _skillArgumentReader = skillArgumentReader;
        }

        public SkillTriggerType TriggerType => SkillTriggerType.EnergyNeeded;

        public string TypeKey => "energy_needed";

        public bool CanActivate(SkillComponent component, SkillActivationContext context)
        {
            if (context.IsInstantCheck)
                return false;

            if (TryReadRequiredEnergy(component, context.SkillLevel, out var requiredEnergy) == false)
                return false;

            var characteristics = context.Owner.CharacteristicState;

            if (characteristics.Energy < requiredEnergy)
            {
                _coreLog.Debug($"[Story][Battle]: Skill energy not ready, unitId = {context.Owner.Id}, energy = {characteristics.Energy}, required = {requiredEnergy}");

                return false;
            }

            return true;
        }

        public void OnActivated(SkillComponent component, SkillActivationContext context)
        {
            context.Owner.CharacteristicState.Energy = 0f;
        }

        public bool TryReadRequiredEnergy(SkillComponent component, int skillLevel, out float requiredEnergy)
        {
            if (_skillArgumentReader.TryReadFloat(component, EnergyParameter, skillLevel, out requiredEnergy) == false)
            {
                _coreLog.Error("[Config]: Skill energy_needed has no readable energy parameter");

                return false;
            }

            return 0f < requiredEnergy;
        }
    }
}
