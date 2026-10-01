namespace Server.Battles
{
    internal sealed class SkillTriggerRegistry : ISkillTriggerRegistry
    {
        private readonly Dictionary<SkillTriggerType, ISkillTriggerEvaluator> _evaluators = new();
        private readonly List<string> _typeKeys = new();

        public SkillTriggerRegistry(IReadOnlyList<ISkillTriggerEvaluator> evaluators)
        {
            for (int i = 0; i < evaluators.Count; i++)
            {
                var evaluator = evaluators[i];

                _evaluators[evaluator.TriggerType] = evaluator;
                _typeKeys.Add(evaluator.TypeKey);
            }
        }

        public IReadOnlyList<string> TypeKeys => _typeKeys;

        public bool TryResolve(SkillTriggerType triggerType, out ISkillTriggerEvaluator evaluator)
        {
            return _evaluators.TryGetValue(triggerType, out evaluator!);
        }
    }
}
