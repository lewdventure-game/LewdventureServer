using System.Globalization;

namespace Server.Battles
{
    internal sealed class SkillArgumentReader : ISkillArgumentReader
    {
        private readonly ICoreLog _coreLog;

        public SkillArgumentReader(ICoreLog coreLog)
        {
            _coreLog = coreLog;
        }

        public bool TryReadFloat(SkillComponent component, string parameterName, int skillLevel, out float value)
        {
            value = 0f;

            if (TryReadArgument(component, parameterName, skillLevel, out var argument) == false)
                return false;

            if (float.TryParse(argument, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                return true;

            _coreLog.Error($"[Config]: Skill parameter {parameterName} of {component.Name} is not a number, raw = {argument}");

            return false;
        }

        public bool TryReadInt(SkillComponent component, string parameterName, int skillLevel, out int value)
        {
            value = 0;

            if (TryReadArgument(component, parameterName, skillLevel, out var argument) == false)
                return false;

            if (int.TryParse(argument, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
                return true;

            if (float.TryParse(argument, NumberStyles.Float, CultureInfo.InvariantCulture, out var asFloat))
            {
                value = (int)asFloat;

                return true;
            }

            _coreLog.Error($"[Config]: Skill parameter {parameterName} of {component.Name} is not an integer, raw = {argument}");

            return false;
        }

        public bool ReadFlag(SkillComponent component, string parameterName, int skillLevel)
        {
            if (TryReadInt(component, parameterName, skillLevel, out var value) == false)
                return false;

            return value == 1;
        }

        public bool TryReadArgument(SkillComponent component, string parameterName, int skillLevel, out string argument)
        {
            argument = string.Empty;

            if (component.TryGetParameter(parameterName, out var parameter) == false)
                return false;

            var arguments = parameter.Arguments;

            if (arguments.Count == 0)
                return false;

            var index = skillLevel;

            if (index < 0)
                index = 0;

            if (arguments.Count <= index)
                index = arguments.Count - 1;

            argument = arguments[index];

            return true;
        }
    }
}
