using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Server.GameConfigs;
using Tests.Golden.Infrastructure;

namespace Tests.Golden
{
    [TestFixture]
    [Category("Golden")]
    [Explicit("Ручной экспорт набора для проверки паритета в Unity")]
    public sealed class ParityKitExportTests
    {
        private const ulong Seed = 42;

        private readonly string[] _cases =
        {
            "001-baseline-1v1-vs-tank",
            "019-full-perk-kit",
            "029-three-summons-slot-order",
            "062-long-battle-turn-limit",
        };

        private readonly UTF8Encoding _encoding = new(false);

        [Test]
        public async Task ExportParityKit()
        {
            using var host = new GoldenTestHost();

            var outputDirectory = Path.Combine(host.Paths.RepositoryDirectory, "out", "parity-kit");

            Directory.CreateDirectory(outputDirectory);

            var fileSource = host.Services.GetRequiredService<FileConfigSnapshotSource>();
            var snapshot = await fileSource.LoadAsync(host.Paths.FixturePath, CancellationToken.None);
            var entries = new List<object>(snapshot.Domains.Count);

            for (int i = 0; i < snapshot.Domains.Count; i++)
                entries.Add(new { Name = snapshot.Domains[i].Domain, Content = snapshot.Domains[i].RowsJson });

            File.WriteAllText(
                Path.Combine(outputDirectory, "ConfigBundle.json"),
                JsonConvert.SerializeObject(new { Configs = entries }, Formatting.Indented),
                _encoding);

            var report = new StringBuilder();

            report.AppendLine("# Набор для проверки паритета");
            report.AppendLine();
            report.AppendLine($"Версия конфигов: {snapshot.Version}");
            report.AppendLine();
            report.AppendLine("Шаги:");
            report.AppendLine();
            report.AppendLine("1. Собрать ядро: bash deploy/scripts/build-unity-core.sh, три DLL положить в Assets/Plugins/BattleCore, добавить link.xml.");
            report.AppendLine("2. Создать ядро в клиенте: new BattleCoreFactory().CreateFromBundle(ConfigBundle.json, log) и проверить, что ConfigVersion совпадает с версией выше.");
            report.AppendLine("3. Для каждого кейса прочитать <case>.request.json в BattleReplayData настройками BattleJsonSettingsFactory, вызвать Replay.");
            report.AppendLine("4. Сериализовать ответ теми же настройками и сравнить строку с <case>.expected.json посимвольно.");
            report.AppendLine("5. При расхождении сравнить порядок бросков с <case>.rolls.txt, чтобы найти первый разошедшийся бросок.");
            report.AppendLine("6. Повторить проверку в сборке IL2CPP, а не только в редакторе.");
            report.AppendLine();
            report.AppendLine("| Кейс | Запрос | Ожидаемый ответ | Трасса бросков |");
            report.AppendLine("| --- | --- | --- | --- |");

            for (int i = 0; i < _cases.Length; i++)
            {
                var goldenCase = host.Catalog.Load(_cases[i]);
                var requestBody = host.RequestBuilder.BuildReplayBody(goldenCase.RequestText, Seed);
                var responsePath = goldenCase.GetResponsePath(Seed);
                var rollsSource = Path.Combine(goldenCase.Directory, "seed-42.rolls.txt");
                var requestName = _cases[i] + ".request.json";
                var expectedName = _cases[i] + ".expected.json";
                var rollsName = File.Exists(rollsSource) ? _cases[i] + ".rolls.txt" : string.Empty;

                File.WriteAllText(Path.Combine(outputDirectory, requestName), requestBody, _encoding);
                File.Copy(responsePath, Path.Combine(outputDirectory, expectedName), true);

                if (rollsName.Length != 0)
                    File.Copy(rollsSource, Path.Combine(outputDirectory, rollsName), true);

                report.AppendLine($"| {_cases[i]} | {requestName} | {expectedName} | {(rollsName.Length == 0 ? "нет" : rollsName)} |");
            }

            File.WriteAllText(Path.Combine(outputDirectory, "README.md"), report.ToString(), _encoding);

            TestContext.Out.WriteLine($"[Parity] kit -> {outputDirectory}");

            Assert.That(Directory.GetFiles(outputDirectory).Length, Is.GreaterThan(_cases.Length));
        }
    }
}
