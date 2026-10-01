namespace Server.Battles
{
    internal sealed class SkillDefinitionParser : ISkillDefinitionParser
    {
        private const char ComponentSeparator = ';';
        private const char ParameterSeparator = ',';
        private const char NameSeparator = ':';
        private const char ComponentOpen = '[';
        private const char ComponentClose = ']';
        private const char ArgumentsOpen = '{';
        private const char ArgumentsClose = '}';

        private readonly ICoreLog _coreLog;

        public SkillDefinitionParser(ICoreLog coreLog)
        {
            _coreLog = coreLog;
        }

        public bool TryParse(Server.Skills.ISkillMapper mapper, out SkillDefinition definition)
        {
            definition = null!;

            if (string.IsNullOrWhiteSpace(mapper.Triggers) && string.IsNullOrWhiteSpace(mapper.Actions))
                return false;

            var triggers = new List<SkillTriggerDefinition>();
            var actions = new List<SkillActionDefinition>();
            var triggerComponents = ParseComponents(mapper.Triggers, mapper.Id, "triggers");

            for (int i = 0; i < triggerComponents.Count; i++)
            {
                var component = triggerComponents[i];
                var triggerType = ResolveTriggerType(component.Name);

                if (triggerType == SkillTriggerType.Unknown)
                {
                    _coreLog.Error($"[Config]: Skill {mapper.Id} has unknown trigger {component.Name}");

                    continue;
                }

                triggers.Add(new SkillTriggerDefinition(triggerType, component));
            }

            var actionComponents = ParseComponents(mapper.Actions, mapper.Id, "actions");

            for (int i = 0; i < actionComponents.Count; i++)
            {
                var component = actionComponents[i];
                var actionType = ResolveActionType(component.Name);

                if (actionType == SkillActionType.Unknown)
                {
                    _coreLog.Error($"[Config]: Skill {mapper.Id} has unknown action {component.Name}");

                    continue;
                }

                actions.Add(new SkillActionDefinition(actionType, component));
            }

            if (triggers.Count == 0)
            {
                _coreLog.Error($"[Config]: Skill {mapper.Id} has no readable triggers, raw = {mapper.Triggers}");

                return false;
            }

            if (actions.Count == 0)
            {
                _coreLog.Error($"[Config]: Skill {mapper.Id} has no readable actions, raw = {mapper.Actions}");

                return false;
            }

            definition = new SkillDefinition(mapper.Id, triggers, actions);

            return true;
        }

        private List<SkillComponent> ParseComponents(string raw, int skillId, string columnName)
        {
            var components = new List<SkillComponent>();

            if (string.IsNullOrWhiteSpace(raw))
                return components;

            var parts = SplitTopLevel(raw, ComponentSeparator);

            for (int i = 0; i < parts.Count; i++)
            {
                var part = parts[i].Trim();

                if (part.Length == 0)
                    continue;

                var nameSeparator = part.IndexOf(NameSeparator);

                if (nameSeparator <= 0)
                {
                    _coreLog.Error($"[Config]: Skill {skillId} {columnName} component without name, raw = {part}");

                    continue;
                }

                var name = part.Substring(0, nameSeparator).Trim();
                var body = Unwrap(part.Substring(nameSeparator + 1).Trim(), ComponentOpen, ComponentClose);
                var parameters = ParseParameters(body, skillId, name);

                components.Add(new SkillComponent(name, parameters));
            }

            return components;
        }

        private List<SkillParameter> ParseParameters(string raw, int skillId, string componentName)
        {
            var parameters = new List<SkillParameter>();

            if (string.IsNullOrWhiteSpace(raw))
                return parameters;

            var parts = SplitTopLevel(raw, ParameterSeparator);

            for (int i = 0; i < parts.Count; i++)
            {
                var part = parts[i].Trim();

                if (part.Length == 0)
                    continue;

                var nameSeparator = part.IndexOf(NameSeparator);

                if (nameSeparator <= 0)
                {
                    _coreLog.Error($"[Config]: Skill {skillId} component {componentName} has a parameter without name, raw = {part}");

                    continue;
                }

                var name = part.Substring(0, nameSeparator).Trim();
                var body = Unwrap(part.Substring(nameSeparator + 1).Trim(), ArgumentsOpen, ArgumentsClose);

                parameters.Add(new SkillParameter(name, ParseArguments(body)));
            }

            return parameters;
        }

        private List<string> ParseArguments(string raw)
        {
            var arguments = new List<string>();

            if (string.IsNullOrWhiteSpace(raw))
                return arguments;

            var start = 0;

            for (int i = 0; i <= raw.Length; i++)
            {
                if (i < raw.Length && raw[i] != ParameterSeparator && raw[i] != NameSeparator)
                    continue;

                var argument = raw.Substring(start, i - start).Trim();

                if (argument.Length != 0)
                    arguments.Add(argument);

                start = i + 1;
            }

            return arguments;
        }

        private List<string> SplitTopLevel(string input, char separator)
        {
            var result = new List<string>();
            var depth = 0;
            var start = 0;

            for (int i = 0; i < input.Length; i++)
            {
                var character = input[i];

                if (character == ComponentOpen || character == ArgumentsOpen)
                {
                    depth += 1;

                    continue;
                }

                if (character == ComponentClose || character == ArgumentsClose)
                {
                    if (0 < depth)
                        depth -= 1;

                    continue;
                }

                if (character != separator || 0 < depth)
                    continue;

                result.Add(input.Substring(start, i - start));
                start = i + 1;
            }

            result.Add(input.Substring(start));

            return result;
        }

        private string Unwrap(string value, char open, char close)
        {
            var trimmed = value.Trim();

            if (trimmed.Length < 2)
                return trimmed;

            if (trimmed[0] != open)
                return trimmed;

            var end = trimmed.LastIndexOf(close);

            if (end <= 0)
                return trimmed.Substring(1);

            return trimmed.Substring(1, end - 1);
        }

        private SkillTriggerType ResolveTriggerType(string name)
        {
            if (string.Equals(name, "energy_needed", StringComparison.OrdinalIgnoreCase))
                return SkillTriggerType.EnergyNeeded;

            if (string.Equals(name, "ally_health_lower", StringComparison.OrdinalIgnoreCase))
                return SkillTriggerType.AllyHealthLower;

            if (string.Equals(name, "on_cooldown", StringComparison.OrdinalIgnoreCase))
                return SkillTriggerType.OnCooldown;

            return SkillTriggerType.Unknown;
        }

        private SkillActionType ResolveActionType(string name)
        {
            if (string.Equals(name, "damage", StringComparison.OrdinalIgnoreCase))
                return SkillActionType.Damage;

            if (string.Equals(name, "set_status", StringComparison.OrdinalIgnoreCase))
                return SkillActionType.SetStatus;

            if (string.Equals(name, "set_bonus", StringComparison.OrdinalIgnoreCase))
                return SkillActionType.SetBonus;

            return SkillActionType.Unknown;
        }
    }
}
