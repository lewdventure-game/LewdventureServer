using Newtonsoft.Json;

namespace Tests.Golden.Infrastructure
{
    internal sealed class SheetRowsBuilder
    {
        public string Build(IList<IList<object>>? values)
        {
            var rowsData = new List<Dictionary<string, object>>();

            if (values == null || values.Count < 2)
                return JsonConvert.SerializeObject(rowsData);

            var headers = new List<string>(values[0].Count);

            for (int headerIndex = 0; headerIndex < values[0].Count; headerIndex++)
            {
                var header = values[0][headerIndex];
                var headerText = header == null ? string.Empty : header.ToString();

                headers.Add(headerText == null ? string.Empty : headerText.Trim());
            }

            for (int i = 1; i < values.Count; i++)
            {
                var row = values[i];
                var rowDict = new Dictionary<string, object>();
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

                    rowDict[header] = cellValue ?? string.Empty;

                    if (header.Equals("is_off", StringComparison.OrdinalIgnoreCase) && cellValue is bool isOff && isOff)
                        isActive = false;
                }

                if (isActive)
                    rowsData.Add(rowDict);
            }

            return JsonConvert.SerializeObject(rowsData);
        }
    }
}
