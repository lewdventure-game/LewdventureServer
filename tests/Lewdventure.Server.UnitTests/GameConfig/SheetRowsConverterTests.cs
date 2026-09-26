using Newtonsoft.Json.Linq;
using Server.GameConfigs;

namespace Tests.Unit.GameConfig
{
    [TestFixture]
    public sealed class SheetRowsConverterTests
    {
        private readonly SheetRowsConverter _converter = new(new ConfigRangeReader());

        [Test]
        public void Convert_MapsHeadersSkipsDisabledRows()
        {
            var values = new List<IReadOnlyList<object?>>
            {
                new List<object?> { "is_off", "id", "title", string.Empty },
                new List<object?> { "FALSE", "1", "first", "ignored" },
                new List<object?> { "TRUE", "2", "disabled", "ignored" },
                new List<object?> { "false", "3" },
            };

            var rows = JArray.Parse(_converter.Convert(values, "A1:D4").RowsJson);

            Assert.That(rows, Has.Count.EqualTo(2));
            Assert.That((string?)rows[0]["id"], Is.EqualTo("1"));
            Assert.That((bool?)rows[0]["is_off"], Is.False);
            Assert.That(rows[0]["ignored"], Is.Null);
            Assert.That((string?)rows[1]["id"], Is.EqualTo("3"));
            Assert.That(rows[1]["title"], Is.Null);
        }

        [Test]
        public void Convert_KeepsSheetRowNumbers()
        {
            var values = new List<IReadOnlyList<object?>>
            {
                new List<object?> { "is_off", "id" },
                new List<object?> { "FALSE", "1" },
                new List<object?> { "TRUE", "2" },
                new List<object?> { "FALSE", "3" },
            };

            var result = _converter.Convert(values, "A1:B4");

            Assert.That(result.SourceRows, Is.EqualTo(new[] { 2, 4 }));
        }

        [Test]
        public void Convert_RangeWithOffset_ShiftsRowNumbers()
        {
            var values = new List<IReadOnlyList<object?>>
            {
                new List<object?> { "id" },
                new List<object?> { "1" },
            };

            var result = _converter.Convert(values, "Constants!A10:B11");

            Assert.That(result.SourceRows, Is.EqualTo(new[] { 11 }));
        }

        [Test]
        public void Convert_EmptyOrHeaderOnly_ReturnsEmptyArray()
        {
            Assert.That(_converter.Convert(null, "A1:D1").RowsJson, Is.EqualTo("[]"));
            Assert.That(_converter.Convert(new List<IReadOnlyList<object?>>(), "A1:D1").RowsJson, Is.EqualTo("[]"));
            Assert.That(_converter.Convert(new List<IReadOnlyList<object?>> { new List<object?> { "id" } }, "A1:D1").RowsJson, Is.EqualTo("[]"));
        }
    }
}
