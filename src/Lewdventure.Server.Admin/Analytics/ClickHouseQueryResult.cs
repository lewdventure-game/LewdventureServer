using System.Text.Json;

namespace Server.Admin.Analytics
{
    internal sealed class ClickHouseQueryResult
    {
        public bool IsSuccess { get; set; }

        public string Error { get; set; } = string.Empty;

        public List<Dictionary<string, JsonElement>> Rows { get; } = new();

        public string ReadString(int row, string column)
        {
            if (Rows[row].TryGetValue(column, out var value) == false)
                return string.Empty;

            return value.ValueKind == JsonValueKind.String ? value.GetString()! : value.GetRawText();
        }

        public double ReadDouble(int row, string column)
        {
            if (Rows[row].TryGetValue(column, out var value) == false)
                return 0d;

            if (value.ValueKind == JsonValueKind.Number)
                return value.GetDouble();

            if (value.ValueKind == JsonValueKind.String && double.TryParse(value.GetString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsed))
                return parsed;

            return 0d;
        }
    }
}
