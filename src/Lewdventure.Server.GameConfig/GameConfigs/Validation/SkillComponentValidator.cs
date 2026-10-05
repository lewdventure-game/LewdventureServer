using System.Globalization;
using Newtonsoft.Json.Linq;
using Server.Skills;

namespace Server.GameConfigs
{
    internal sealed class SkillComponentValidator
    {
        private const string IdProperty = "id";
        private const string TriggersProperty = "triggers";
        private const string ActionsProperty = "actions";
        private const char ComponentSeparator = ';';
        private const char ParameterSeparator = ',';
        private const char NameSeparator = ':';
        private const char ComponentOpen = '[';
        private const char ComponentClose = ']';
        private const char ArgumentsOpen = '{';
        private const char ArgumentsClose = '}';

        private readonly SkillComponentRegistry _skillComponentRegistry;

        public SkillComponentValidator(SkillComponentRegistry skillComponentRegistry)
        {
            _skillComponentRegistry = skillComponentRegistry;
        }

        public void Validate(ConfigSnapshotDomain domain, List<string> errors, List<string> warnings)
        {
            JArray rows;

            try
            {
                rows = JArray.Parse(domain.RowsJson);
            }
            catch (Newtonsoft.Json.JsonException)
            {
                return;
            }

            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i] is JObject row == false)
                    continue;

                var id = ReadText(row, IdProperty);
                var triggers = ReadText(row, TriggersProperty);
                var actions = ReadText(row, ActionsProperty);

                if (triggers.Length == 0 && actions.Length == 0)
                    continue;

                if (triggers.Length == 0)
                    errors.Add($"Лист Skills, скилл {id}: заполнены actions, но пустые triggers - скилл никогда не активируется.");

                if (actions.Length == 0)
                    errors.Add($"Лист Skills, скилл {id}: заполнены triggers, но пустые actions - скилл ничего не делает.");

                ValidateColumn(id, TriggersProperty, triggers, true, errors, warnings);
                ValidateColumn(id, ActionsProperty, actions, false, errors, warnings);
                ValidatePassiveBonus(id, triggers, actions, warnings);
            }
        }

        private void ValidatePassiveBonus(string id, string triggers, string actions, List<string> warnings)
        {
            var compactTriggers = RemoveWhitespace(triggers);

            if (compactTriggers.Contains("as_bonus_logic:[", StringComparison.Ordinal) == false)
                return;

            var compactActions = RemoveWhitespace(actions);

            if (compactActions.Contains("damage:[", StringComparison.Ordinal) || compactActions.Contains("set_status:[", StringComparison.Ordinal))
                warnings.Add($"Лист Skills, скилл {id}: с условием as_bonus_logic работает только set_bonus, остальные действия игнорируются.");
        }

        private string RemoveWhitespace(string value)
        {
            var builder = new System.Text.StringBuilder(value.Length);

            for (int i = 0; i < value.Length; i++)
            {
                if (char.IsWhiteSpace(value[i]) == false)
                    builder.Append(value[i]);
            }

            return builder.ToString();
        }

        private void ValidateColumn(string id, string column, string raw, bool isTrigger, List<string> errors, List<string> warnings)
        {
            if (raw.Length == 0)
                return;

            var components = SplitTopLevel(raw, ComponentSeparator);

            for (int i = 0; i < components.Count; i++)
            {
                var component = components[i].Trim();

                if (component.Length == 0)
                    continue;

                var nameSeparator = component.IndexOf(NameSeparator);

                if (nameSeparator <= 0)
                {
                    errors.Add($"Лист Skills, скилл {id}, колонка {column}: не разобрать запись {component}, ожидался вид имя:[параметр:{{значение}}].");

                    continue;
                }

                var name = component.Substring(0, nameSeparator).Trim();
                var body = Unwrap(component.Substring(nameSeparator + 1).Trim(), ComponentOpen, ComponentClose);
                SkillComponentDefinition definition;

                if (isTrigger)
                {
                    if (_skillComponentRegistry.TryGetTrigger(name, out definition) == false)
                    {
                        errors.Add($"Лист Skills, скилл {id}: неизвестное условие активации {name}, сервер такой скилл не активирует.");

                        continue;
                    }
                }
                else if (_skillComponentRegistry.TryGetAction(name, out definition) == false)
                {
                    errors.Add($"Лист Skills, скилл {id}: неизвестное целевое действие {name}, сервер такое действие не выполнит.");

                    continue;
                }

                ValidateParameters(id, column, name, body, definition, errors, warnings);
            }
        }

        private void ValidateParameters(string id, string column, string componentName, string body, SkillComponentDefinition definition, List<string> errors, List<string> warnings)
        {
            if (body.Trim().Length == 0)
            {
                if (0 < definition.ParameterNames.Count)
                    errors.Add($"Лист Skills, скилл {id}: у {componentName} не заданы параметры.");

                return;
            }

            var parameters = SplitTopLevel(body, ParameterSeparator);

            for (int i = 0; i < parameters.Count; i++)
            {
                var parameter = parameters[i].Trim();

                if (parameter.Length == 0)
                    continue;

                var nameSeparator = parameter.IndexOf(NameSeparator);

                if (nameSeparator <= 0)
                {
                    errors.Add($"Лист Skills, скилл {id}, колонка {column}: не разобрать параметр {parameter} у {componentName}.");

                    continue;
                }

                var parameterName = parameter.Substring(0, nameSeparator).Trim();
                var arguments = parameter.Substring(nameSeparator + 1).Trim();

                if (definition.HasParameter(parameterName) == false)
                    warnings.Add($"Лист Skills, скилл {id}: параметр {parameterName} у {componentName} сервером не используется, проверьте опечатку.");

                if (arguments.Length < 2 || arguments[0] != ArgumentsOpen || arguments[arguments.Length - 1] != ArgumentsClose)
                {
                    warnings.Add($"Лист Skills, скилл {id}: значения параметра {parameterName} у {componentName} не обёрнуты в фигурные скобки.");

                    continue;
                }

                var inner = Unwrap(arguments, ArgumentsOpen, ArgumentsClose);

                if (0 <= inner.IndexOf(ParameterSeparator))
                    warnings.Add($"Лист Skills, скилл {id}: в значениях параметра {parameterName} у {componentName} стоит запятая, разделитель третьего порядка - двоеточие.");

                ValidateArguments(id, componentName, parameterName, inner, errors);
            }
        }

        private void ValidateArguments(string id, string componentName, string parameterName, string inner, List<string> errors)
        {
            var arguments = SplitArguments(inner);

            if (arguments.Count == 0)
            {
                errors.Add($"Лист Skills, скилл {id}: у параметра {parameterName} в {componentName} нет значений.");

                return;
            }

            for (int i = 0; i < arguments.Count; i++)
            {
                var argument = arguments[i];

                if (float.TryParse(argument, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
                    continue;

                errors.Add($"Лист Skills, скилл {id}: значение {argument} параметра {parameterName} в {componentName} не число. Значения по уровням разделяются двоеточием, дробная часть - точкой.");
            }
        }

        private List<string> SplitArguments(string inner)
        {
            var arguments = new List<string>();
            var start = 0;

            for (int i = 0; i <= inner.Length; i++)
            {
                if (i < inner.Length && inner[i] != ParameterSeparator && inner[i] != NameSeparator)
                    continue;

                var argument = inner.Substring(start, i - start).Trim();

                if (argument.Length != 0)
                    arguments.Add(argument);

                start = i + 1;
            }

            return arguments;
        }

        private string ReadText(JObject row, string property)
        {
            var token = row[property];

            if (token == null || token.Type == JTokenType.Null)
                return string.Empty;

            return token.ToString().Trim();
        }

        private string Unwrap(string value, char open, char close)
        {
            if (value.Length < 2)
                return value;

            if (value[0] != open || value[value.Length - 1] != close)
                return value;

            return value.Substring(1, value.Length - 2);
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
    }
}
