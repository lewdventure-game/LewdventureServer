namespace Tests.Unit.Battle
{
    [TestFixture]
    public sealed class BattleNumericPolicyTests
    {
        private readonly string[] _forbiddenApis =
        {
            "MathF.Pow",
            "MathF.Sqrt",
            "MathF.Exp",
            "MathF.Log",
            "MathF.Sin",
            "MathF.Cos",
            "MathF.Tan",
            "MathF.Atan",
            "MathF.FusedMultiplyAdd",
            "Math.Pow",
            "Math.Sqrt",
            "Math.Exp",
            "Math.Log",
            "Math.Sin",
            "Math.Cos",
            "Math.FusedMultiplyAdd",
            "Random.Shared",
            "DateTime.Now",
            "DateTime.UtcNow",
            "Environment.TickCount",
            "Guid.NewGuid",
        };

        private readonly string[] _doubleAllowList =
        {
            "SeededRandomService.cs",
        };

        private readonly Dictionary<string, string[]> _apiAllowList = new(StringComparer.Ordinal)
        {
            ["SeededRandomService.cs"] = new[] { "Environment.TickCount" },
        };

        [Test]
        public void BattleSources_DoNotUsePlatformDependentMath()
        {
            var violations = new List<string>();
            var files = Directory.GetFiles(FindBattleDirectory(), "*.cs", SearchOption.AllDirectories);

            for (int i = 0; i < files.Length; i++)
            {
                if (IsGenerated(files[i]))
                    continue;

                var text = File.ReadAllText(files[i]);

                var name = Path.GetFileName(files[i]);

                for (int j = 0; j < _forbiddenApis.Length; j++)
                {
                    if (text.Contains(_forbiddenApis[j], StringComparison.Ordinal) == false)
                        continue;

                    if (IsApiAllowed(name, _forbiddenApis[j]))
                        continue;

                    violations.Add($"{name}: {_forbiddenApis[j]}");
                }
            }

            Assert.That(violations, Is.Empty, "бой должен считаться одинаково на сервере и в Unity: " + string.Join("; ", violations));
        }

        [Test]
        public void BattleSources_DoNotMixDoubleIntoFloatMath()
        {
            var violations = new List<string>();
            var files = Directory.GetFiles(FindBattleDirectory(), "*.cs", SearchOption.AllDirectories);

            for (int i = 0; i < files.Length; i++)
            {
                var name = Path.GetFileName(files[i]);

                if (IsGenerated(files[i]) || IsAllowed(name))
                    continue;

                var lines = File.ReadAllLines(files[i]);

                for (int j = 0; j < lines.Length; j++)
                {
                    var line = lines[j];

                    if (line.Contains(" double ", StringComparison.Ordinal) || line.Contains("(double)", StringComparison.Ordinal))
                        violations.Add($"{name}:{j + 1}");
                }
            }

            Assert.That(violations, Is.Empty, "в боевом коде не должно быть double: " + string.Join("; ", violations));
        }

        private bool IsApiAllowed(string fileName, string api)
        {
            if (_apiAllowList.TryGetValue(fileName, out var allowed) == false)
                return false;

            for (int i = 0; i < allowed.Length; i++)
            {
                if (string.Equals(allowed[i], api, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private bool IsAllowed(string fileName)
        {
            for (int i = 0; i < _doubleAllowList.Length; i++)
            {
                if (string.Equals(_doubleAllowList[i], fileName, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private bool IsGenerated(string path)
        {
            return path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                || path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal);
        }

        private string FindBattleDirectory()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);

            while (directory != null)
            {
                var candidate = Path.Combine(directory.FullName, "src", "Lewdventure.Server.Battle");

                if (Directory.Exists(candidate))
                    return candidate;

                directory = directory.Parent;
            }

            throw new InvalidOperationException("src/Lewdventure.Server.Battle not found");
        }
    }
}
