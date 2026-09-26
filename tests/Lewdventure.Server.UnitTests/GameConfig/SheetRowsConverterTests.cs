using Newtonsoft.Json.Linq;
using Server.GameConfigs;

namespace Tests.Unit.GameConfig
{
    [TestFixture]
    public sealed class SheetRowsConverterTests
    {
        private readonly SheetRowsConverter _converter = new();

        [Test]
        public void ToRowsJson_MapsHeadersSkipsDisabledRows()
        {
            var values = new List<IReadOnlyList<object?>>
            {
                new List<object?> { "is_off", "id", "title", string.Empty },
                new List<object?> { "FALSE", "1", "first", "ignored" },
                new List<object?> { "TRUE", "2", "disabled", "ignored" },
                new List<object?> { "false", "3" },
            };

            var rows = JArray.Parse(_converter.ToRowsJson(values));

            Assert.That(rows, Has.Count.EqualTo(2));
            Assert.That((string?)rows[0]["id"], Is.EqualTo("1"));
            Assert.That((bool?)rows[0]["is_off"], Is.False);
            Assert.That(rows[0]["ignored"], Is.Null);
            Assert.That((string?)rows[1]["id"], Is.EqualTo("3"));
            Assert.That(rows[1]["title"], Is.Null);
        }

        [Test]
        public void ToRowsJson_EmptyOrHeaderOnly_ReturnsEmptyArray()
        {
            Assert.That(_converter.ToRowsJson(null), Is.EqualTo("[]"));
            Assert.That(_converter.ToRowsJson(new List<IReadOnlyList<object?>>()), Is.EqualTo("[]"));
            Assert.That(_converter.ToRowsJson(new List<IReadOnlyList<object?>> { new List<object?> { "id" } }), Is.EqualTo("[]"));
        }
    }
}
