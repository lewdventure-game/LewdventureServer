using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Editors.BattleParity
{
    internal sealed class BattleParityBuild
    {
        private const string ScenePath = "Assets/Scenes/ParityCheckTemp.unity";
        private const string OutputDirectory = "out/parity-player";
        private const string ExecutableName = "Lewdventure.exe";

        [MenuItem("Lewdventure/Собрать плеер для паритета (IL2CPP)")]
        private static void BuildFromMenu()
        {
            new BattleParityBuild().Build();
        }

        public static void BuildWindowsIl2Cpp()
        {
            new BattleParityBuild().Build();
        }

        public void Build()
        {
            var previousBackend = PlayerSettings.GetScriptingBackend(NamedBuildTarget.Standalone);
            var scenePath = Path.GetFullPath(ScenePath);
            var outputPath = Path.Combine(Path.GetFullPath(OutputDirectory), ExecutableName);

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            Directory.CreateDirectory(Path.GetDirectoryName(scenePath)!);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            EditorSceneManager.SaveScene(scene, ScenePath);
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.IL2CPP);

            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = outputPath,
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.None,
            };

            var report = BuildPipeline.BuildPlayer(options);

            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, previousBackend);
            AssetDatabase.DeleteAsset(ScenePath);
            AssetDatabase.SaveAssets();

            Debug.Log($"[ParityBuild] result = {report.summary.result}, errors = {report.summary.totalErrors}, output = {outputPath}");
        }
    }
}
