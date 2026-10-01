namespace Server.Battles
{
    internal interface ISkillTriggerRegistry
    {
        public IReadOnlyList<string> TypeKeys { get; }

        public bool TryResolve(SkillTriggerType triggerType, out ISkillTriggerEvaluator evaluator);
    }
}
