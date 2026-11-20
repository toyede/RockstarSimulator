using System.IO;
using UnityEditor;
using UnityEngine;

namespace ContextStage.EditorTools
{
    internal static class PlaytestMetricsSetup
    {
        const string MenuPath = "Tools/Playtest/Remote Metrics in Release Builds";
        const string AssetFolder = "Assets/Resources/Playtest";
        const string MarkerPath = AssetFolder + "/EnableRemoteMetrics.txt";

        [MenuItem(MenuPath, true)]
        static bool ValidateToggle()
        {
            Menu.SetChecked(MenuPath, AssetDatabase.LoadAssetAtPath<TextAsset>(MarkerPath) != null);
            return !EditorApplication.isPlayingOrWillChangePlaymode && !BuildPipeline.isBuildingPlayer;
        }

        [MenuItem(MenuPath)]
        static void Toggle()
        {
            if (AssetDatabase.LoadAssetAtPath<TextAsset>(MarkerPath) != null)
            {
                if (AssetDatabase.MoveAssetToTrash(MarkerPath))
                    Debug.Log("[Playtest] Remote metrics disabled for future builds.");
                else
                    Debug.LogError("[Playtest] Could not remove the remote metrics marker.");
                return;
            }

            if (!EditorUtility.DisplayDialog(
                    "Enable remote playtest metrics?",
                    "Enable only after the collection endpoint is ready. This applies to all subsequent non-development builds until disabled. Players must explicitly opt in from Options. Editor and Development Build do not send metrics.",
                    "Enable for test builds", "Cancel"))
                return;

            EditorSetupUtility.EnsureFolder(AssetFolder);
            if (!File.Exists(MarkerPath))
            {
                using var stream = new FileStream(MarkerPath, FileMode.CreateNew, FileAccess.Write);
                using var writer = new StreamWriter(stream);
                writer.WriteLine("Remote playtest metrics enabled for this build.");
            }
            AssetDatabase.ImportAsset(MarkerPath, ImportAssetOptions.ForceSynchronousImport);

            if (AssetDatabase.LoadAssetAtPath<TextAsset>(MarkerPath) != null)
                Debug.Log("[Playtest] Remote metrics enabled for future non-development builds. Disable before a normal public build.");
            else
                Debug.LogError("[Playtest] The remote metrics marker could not be imported.");
        }
    }
}
