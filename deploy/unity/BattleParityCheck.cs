using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Server.Battles;
using Server.Logging;
using UnityEngine;

namespace Cheats.BattleParity
{
    internal sealed class BattleParityRunner
    {
        private const string KitFolderName = "parity-kit";
        private const string ReportFileName = "parity-report.txt";

        private readonly ICoreLog _coreLog;
        private readonly StringBuilder _report = new();

        public BattleParityRunner(ICoreLog coreLog)
        {
            _coreLog = coreLog;
        }

        public bool Run()
        {
            var succeeded = Check();
            var reportPath = Path.Combine(Application.persistentDataPath, ReportFileName);

            try
            {
                File.WriteAllText(reportPath, _report.ToString());
                Debug.Log($"[Parity] report saved: {reportPath}");
            }
            catch (Exception exception)
            {
                Debug.LogError($"[Parity] report not saved: {exception.Message}");
            }

            return succeeded;
        }

        private bool Check()
        {
            var kitDirectory = Path.Combine(Application.streamingAssetsPath, KitFolderName);

            Line($"platform = {Application.platform}, unity = {Application.unityVersion}, il2cpp = {IsIl2Cpp()}");

            if (Directory.Exists(kitDirectory) == false)
            {
                Line($"FAIL kit not found: {kitDirectory}", true);

                return false;
            }

            var expectedVersion = File.ReadAllText(Path.Combine(kitDirectory, "config-version.txt")).Trim();
            var bundleJson = File.ReadAllText(Path.Combine(kitDirectory, "ConfigBundle.json"));
            var rollTrace = new BattleRollTrace();
            var result = new BattleCoreFactory().CreateFromBundle(bundleJson, _coreLog, rollTrace);

            if (result.Succeeded == false)
            {
                Line($"FAIL configs rejected: {string.Join("; ", result.Errors)}", true);

                return false;
            }

            if (string.Equals(result.ConfigVersion, expectedVersion, StringComparison.Ordinal) == false)
            {
                Line($"FAIL config version: client = {result.ConfigVersion}, kit = {expectedVersion}", true);

                return false;
            }

            Line($"config version ok: {result.ConfigVersion}");

            var serializerSettings = new BattleJsonSettingsFactory().Create();
            var caseNames = File.ReadAllLines(Path.Combine(kitDirectory, "cases.txt"));
            var checkedCount = 0;
            var failed = 0;

            for (int i = 0; i < caseNames.Length; i++)
            {
                var caseName = caseNames[i].Trim();

                if (caseName.Length == 0)
                    continue;

                checkedCount += 1;

                if (CheckCase(result.BattleCore, rollTrace, serializerSettings, kitDirectory, caseName) == false)
                    failed += 1;
            }

            if (failed == 0)
            {
                Line($"PASS all cases match, count = {checkedCount}");

                return true;
            }

            Line($"FAIL cases failed = {failed} of {checkedCount}", true);

            return false;
        }

        private bool CheckCase(
            IBattleCore battleCore,
            BattleRollTrace rollTrace,
            JsonSerializerSettings serializerSettings,
            string kitDirectory,
            string caseName)
        {
            var requestJson = File.ReadAllText(Path.Combine(kitDirectory, caseName + ".request.json"));
            var expectedDigest = File.ReadAllText(Path.Combine(kitDirectory, caseName + ".digest.txt")).Trim();
            var request = JsonConvert.DeserializeObject<BattleReplayData>(requestJson, serializerSettings);

            rollTrace.Clear();

            var script = battleCore.Replay(request);
            var digest = battleCore.ComputeDigest(script);

            if (string.Equals(digest, expectedDigest, StringComparison.Ordinal))
            {
                Line($"ok {caseName}: steps = {script.Steps.Count}, rolls = {rollTrace.Lines.Count}, digest = {digest}");
                ReportTextDifference(serializerSettings, script, kitDirectory, caseName, false);

                return true;
            }

            Line($"FAIL {caseName}: digest = {digest}, kit = {expectedDigest}, steps = {script.Steps.Count}, rolls = {rollTrace.Lines.Count}", true);
            ReportTrace(rollTrace, kitDirectory, caseName);
            ReportTextDifference(serializerSettings, script, kitDirectory, caseName, true);

            return false;
        }

        private void ReportTextDifference(
            JsonSerializerSettings serializerSettings,
            IBattleScriptResponse script,
            string kitDirectory,
            string caseName,
            bool isFailure)
        {
            var expectedPath = Path.Combine(kitDirectory, caseName + ".expected.json");

            if (File.Exists(expectedPath) == false)
                return;

            var expectedJson = File.ReadAllText(expectedPath);
            var actualJson = JsonConvert.SerializeObject(script, serializerSettings);

            if (string.Equals(actualJson, expectedJson, StringComparison.Ordinal))
                return;

            var difference = FindFirstDifference(expectedJson, actualJson);
            var message = $"   json text differs at char {difference}\n   kit:    {Fragment(expectedJson, difference)}\n   client: {Fragment(actualJson, difference)}";

            if (isFailure)
                Line(message, true);
            else
                Line(message + "\n   digest matches: разница только в печати float, биты одинаковые");
        }

        private void ReportTrace(BattleRollTrace rollTrace, string kitDirectory, string caseName)
        {
            var tracePath = Path.Combine(kitDirectory, caseName + ".rolls.txt");

            if (File.Exists(tracePath) == false)
                return;

            var expectedLines = File.ReadAllLines(tracePath);
            var actualLines = rollTrace.Lines;

            for (int i = 0; i < expectedLines.Length; i++)
            {
                var expectedLine = expectedLines[i].Trim();

                if (expectedLine.Length == 0)
                    continue;

                if (actualLines.Count <= i)
                {
                    Line($"   roll trace ends early at line {i}: kit {expectedLine}", true);

                    return;
                }

                if (string.Equals(expectedLine, actualLines[i], StringComparison.Ordinal))
                    continue;

                Line($"   first roll mismatch at line {i}: kit {expectedLine}, client {actualLines[i]}", true);

                return;
            }

            if (expectedLines.Length < actualLines.Count)
                Line($"   extra rolls after line {expectedLines.Length}: {actualLines[expectedLines.Length]}", true);
            else
                Line("   roll trace matches: расхождение не в случайности, а в сборке скрипта", true);
        }

        private bool IsIl2Cpp()
        {
#if ENABLE_IL2CPP
            return true;
#else
            return false;
#endif
        }

        private string Fragment(string value, int index)
        {
            var start = 40 < index ? index - 40 : 0;
            var length = value.Length - start;

            if (80 < length)
                length = 80;

            return value.Substring(start, length);
        }

        private int FindFirstDifference(string expected, string actual)
        {
            var length = expected.Length < actual.Length ? expected.Length : actual.Length;

            for (int i = 0; i < length; i++)
            {
                if (expected[i] != actual[i])
                    return i;
            }

            return length;
        }

        private void Line(string message)
        {
            Line(message, false);
        }

        private void Line(string message, bool isError)
        {
            _report.AppendLine(message);

            if (isError)
                Debug.LogError("[Parity] " + message);
            else
                Debug.Log("[Parity] " + message);
        }
    }

#if UNITY_EDITOR
    internal sealed class BattleParityCheckMenu
    {
        [UnityEditor.MenuItem("Lewdventure/Проверить паритет боя")]
        private static void Run()
        {
            new BattleParityRunner(new SilentCoreLog()).Run();
        }
    }
#endif

    internal sealed class BattleParityStartupHook
    {
        private const string CommandLineFlag = "-parity";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void RunWhenRequested()
        {
            var arguments = Environment.GetCommandLineArgs();
            var requested = false;

            for (int i = 0; i < arguments.Length; i++)
            {
                if (string.Equals(arguments[i], CommandLineFlag, StringComparison.OrdinalIgnoreCase) == false)
                    continue;

                requested = true;

                break;
            }

            if (requested == false)
                return;

            var succeeded = new BattleParityRunner(new SilentCoreLog()).Run();

            Application.Quit(succeeded ? 0 : 1);
        }
    }

    internal sealed class UnityCoreLog : ICoreLog
    {
        public void Debug(string message)
        {
            UnityEngine.Debug.Log(message);
        }

        public void Information(string message)
        {
            UnityEngine.Debug.Log(message);
        }

        public void Warning(string message)
        {
            UnityEngine.Debug.LogWarning(message);
        }

        public void Warning(Exception exception, string message)
        {
            UnityEngine.Debug.LogWarning($"{message} {exception.Message}");
        }

        public void Error(string message)
        {
            UnityEngine.Debug.LogError(message);
        }

        public void Error(Exception exception, string message)
        {
            UnityEngine.Debug.LogError($"{message} {exception.Message}");
        }
    }
}
