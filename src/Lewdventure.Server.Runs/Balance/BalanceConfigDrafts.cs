using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Server.GameConfigs;

namespace Server.Balance
{
    internal sealed class BalanceConfigDrafts
    {
        public const int MaxEdits = 200;

        private const string DraftSource = "balance-draft";
        private const string DraftSuffix = "+draft";
        private const char RowIndexPrefix = '#';
        private const char ConditionSeparator = ';';
        private const char KeyValueSeparator = '=';

        private readonly GameConfigSetBuilder _gameConfigSetBuilder;

        public BalanceConfigDrafts(GameConfigSetBuilder gameConfigSetBuilder)
        {
            _gameConfigSetBuilder = gameConfigSetBuilder;
        }

        public BalanceConfigDraft Apply(GameConfigSet configSet, IReadOnlyList<BalanceConfigEdit> edits)
        {
            var errors = new List<string>();

            if (edits.Count == 0)
                return new BalanceConfigDraft(configSet.Version, configSet.Distributor, errors);

            if (MaxEdits < edits.Count)
            {
                errors.Add($"Draft has {edits.Count} edits, the limit is {MaxEdits}.");

                return new BalanceConfigDraft(configSet.Version, null, errors);
            }

            var snapshot = configSet.Snapshot;
            var rowsByDomain = new Dictionary<string, JArray>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < edits.Count; i++)
                ApplyEdit(snapshot, edits[i], i + 1, rowsByDomain, errors);

            if (0 < errors.Count)
                return new BalanceConfigDraft(configSet.Version, null, errors);

            var version = configSet.Version + DraftSuffix;
            var draft = new GameConfigSnapshot(version, snapshot.CreatedAt, DraftSource, CreateDomains(snapshot, rowsByDomain));
            var buildResult = _gameConfigSetBuilder.Build(draft, DraftSource);

            if (buildResult.Succeeded == false)
            {
                errors.AddRange(buildResult.Errors);

                return new BalanceConfigDraft(version, null, errors);
            }

            return new BalanceConfigDraft(version, buildResult.ConfigSet!.Distributor, errors);
        }

        private void ApplyEdit(GameConfigSnapshot snapshot, BalanceConfigEdit edit, int number, Dictionary<string, JArray> rowsByDomain, List<string> errors)
        {
            var label = $"Edit {number} ({edit.Sheet} / {edit.Row} / {edit.Column})";
            var sheet = (edit.Sheet ?? string.Empty).Trim();
            var column = (edit.Column ?? string.Empty).Trim();

            if (sheet.Length == 0 || column.Length == 0)
            {
                errors.Add($"{label}: sheet and column are required.");

                return;
            }

            var domain = FindDomain(snapshot, sheet);

            if (domain == null)
            {
                errors.Add($"{label}: sheet {sheet} is not in the snapshot.");

                return;
            }

            if (rowsByDomain.TryGetValue(domain.Domain, out var rows) == false)
            {
                rows = JArray.Parse(domain.RowsJson);
                rowsByDomain[domain.Domain] = rows;
            }

            var errorCount = errors.Count;
            var matched = FindRows(rows, (edit.Row ?? string.Empty).Trim(), label, errors);

            if (matched.Count == 0)
            {
                if (errors.Count == errorCount)
                    errors.Add($"{label}: no row matches {edit.Row}.");

                return;
            }

            for (int i = 0; i < matched.Count; i++)
            {
                if (matched[i].TryGetValue(column, out var current) == false)
                {
                    errors.Add($"{label}: column {column} is not in sheet {domain.Domain}.");

                    return;
                }

                if (TryCreateValue(current, (edit.Value ?? string.Empty).Trim(), out var value) == false)
                {
                    errors.Add($"{label}: value {edit.Value} does not fit column {column}.");

                    return;
                }

                matched[i][column] = value;
            }
        }

        private bool TryCreateValue(JToken current, string raw, out JToken value)
        {
            value = new JValue(raw);

            if (current.Type == JTokenType.Boolean)
            {
                if (bool.TryParse(raw, out var flag) == false)
                    return false;

                value = new JValue(flag);
            }

            return true;
        }

        private ConfigSnapshotDomain? FindDomain(GameConfigSnapshot snapshot, string sheet)
        {
            for (int i = 0; i < snapshot.Domains.Count; i++)
            {
                if (string.Equals(snapshot.Domains[i].Domain, sheet, StringComparison.OrdinalIgnoreCase))
                    return snapshot.Domains[i];
            }

            return null;
        }

        private List<JObject> FindRows(JArray rows, string selector, string label, List<string> errors)
        {
            var matched = new List<JObject>();

            if (selector.Length == 0)
            {
                errors.Add($"{label}: row is required, for example id=10101 or #3.");

                return matched;
            }

            if (selector[0] == RowIndexPrefix)
            {
                if (int.TryParse(selector.Substring(1), out var index) == false || index < 1 || rows.Count < index)
                {
                    errors.Add($"{label}: row {selector} is out of range 1..{rows.Count}.");

                    return matched;
                }

                if (rows[index - 1] is JObject indexed)
                    matched.Add(indexed);

                return matched;
            }

            var conditions = ParseConditions(selector, label, errors);

            if (conditions.Count == 0)
                return matched;

            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i] is JObject row && Matches(row, conditions))
                    matched.Add(row);
            }

            return matched;
        }

        private List<KeyValuePair<string, string>> ParseConditions(string selector, string label, List<string> errors)
        {
            var conditions = new List<KeyValuePair<string, string>>();
            var parts = selector.Split(ConditionSeparator, StringSplitOptions.RemoveEmptyEntries);

            for (int i = 0; i < parts.Length; i++)
            {
                var separator = parts[i].IndexOf(KeyValueSeparator);

                if (separator <= 0)
                {
                    errors.Add($"{label}: row condition {parts[i].Trim()} must look like column=value.");

                    return new List<KeyValuePair<string, string>>();
                }

                conditions.Add(new KeyValuePair<string, string>(parts[i].Substring(0, separator).Trim(), parts[i].Substring(separator + 1).Trim()));
            }

            return conditions;
        }

        private bool Matches(JObject row, List<KeyValuePair<string, string>> conditions)
        {
            for (int i = 0; i < conditions.Count; i++)
            {
                if (row.TryGetValue(conditions[i].Key, out var token) == false)
                    return false;

                if (string.Equals(token.ToString(), conditions[i].Value, StringComparison.Ordinal) == false)
                    return false;
            }

            return true;
        }

        private List<ConfigSnapshotDomain> CreateDomains(GameConfigSnapshot snapshot, Dictionary<string, JArray> rowsByDomain)
        {
            var domains = new List<ConfigSnapshotDomain>(snapshot.Domains.Count);

            for (int i = 0; i < snapshot.Domains.Count; i++)
            {
                var domain = snapshot.Domains[i];

                if (rowsByDomain.TryGetValue(domain.Domain, out var rows) == false)
                {
                    domains.Add(domain);

                    continue;
                }

                domains.Add(new ConfigSnapshotDomain(domain.Domain, domain.SpreadsheetId, domain.Range, rows.ToString(Formatting.None, Array.Empty<JsonConverter>()), domain.SourceRows));
            }

            return domains;
        }
    }
}
