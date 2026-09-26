namespace Server.GameConfigs
{
    internal sealed class SheetRowsResult
    {
        public SheetRowsResult(string rowsJson, IReadOnlyList<int> sourceRows)
        {
            RowsJson = rowsJson;
            SourceRows = sourceRows;
        }

        public string RowsJson { get; }

        public IReadOnlyList<int> SourceRows { get; }
    }
}
