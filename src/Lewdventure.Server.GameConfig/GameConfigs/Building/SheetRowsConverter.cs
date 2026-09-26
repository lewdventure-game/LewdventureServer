using Newtonsoft.Json;

namespace Server.GameConfigs
{
    internal sealed class SheetRowsConverter
    {
        private const string IsOffHeader = "is_off";

        private readonly ConfigRangeReader _configRangeReader;

        public SheetRowsConverter(ConfigRangeReader configRangeReader)
        {
            _configRangeReader = configRangeReader;
        }

        public SheetRowsResult Convert(IReadOnlyList<IReadOnlyList<object?>>? values, string range)
        {
            var rowsData = new List<Dictionary<string, object>>();
            var sourceRows = new List<int>();

            if (values == null || values.Count < 2)
                return new SheetRowsResult(JsonConvert.SerializeObject(rowsData), sourceRows);

            var startRow = _configRangeReader.ResolveStartRow(range);
            var headerRow = values[0];
            var headers = new List<string>(headerRow.Count);

            for (int i = 0; i < headerRow.Count; i++)
            {
                var header = headerRow[i];
                var headerText = header == null ? string.Empty : header.ToString();

                headers.Add(headerText == null ? string.Empty : headerText.Trim());
            }

            for (int i = 1; i < values.Count; i++)
            {
                var row = values[i];
                var rowData = new Dictionary<string, object>();
                var isActive = true;

                for (int j = 0; j < headers.Count && j < row.Count; j++)
                {
                    var header = headers[j];

                    if (string.IsNullOrEmpty(header))
                        continue;

                    var cellValue = row[j];

                    if (cellValue is string stringValue
                        && (stringValue.Equals("TRUE", StringComparison.OrdinalIgnoreCase)
                            || stringValue.Equals("FALSE", StringComparison.OrdinalIgnoreCase)))
                    {
                        cellValue = bool.Parse(stringValue);
                    }

                    rowData[header] = cellValue ?? string.Empty;

                    if (header.Equals(IsOffHeader, StringComparison.OrdinalIgnoreCase) && cellValue is bool isOff && isOff)
                        isActive = false;
                }

                if (isActive == false)
                    continue;

                rowsData.Add(rowData);
                sourceRows.Add(startRow + i);
            }

            return new SheetRowsResult(JsonConvert.SerializeObject(rowsData), sourceRows);
        }
    }
}
