#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using ContextStage;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ContextStageEditor
{
    public static class TourPrototypeSetup
    {
        const string TourSettingsFolder = "Assets/Settings/Tour";
        const string TitleScenePath = "Assets/Scenes/Title.unity";
        const string MainScenePath = "Assets/Scenes/Main.unity";
        const string HubScenePath = "Assets/Scenes/TourHub.unity";

        /// <summary>
        /// Title 씬 런처의 스테이지 목록을 기존 Stage01~05 에셋 5개로 되돌린다 (에셋 값은 건드리지 않음).
        /// Stage04 아트가 오기 전까지 임시로 5노드를 유지할 때 쓴다.
        /// </summary>
        [MenuItem("Tools/Tour/Restore Five Nodes", false, 1)]
        public static void RestoreFiveNodes()
        {
            string originalScenePath = SceneManager.GetActiveScene().path;
            string[] names = { "Stage01", "Stage02", "Stage03", "Stage04", "Stage05Boss" };
            var stages = new List<StageDefinition>(names.Length);
            foreach (string name in names)
            {
                var stage = AssetDatabase.LoadAssetAtPath<StageDefinition>($"{TourSettingsFolder}/{name}.asset");
                if (stage == null)
                {
                    Debug.LogError($"[TourPrototypeSetup] {name}.asset 이 없습니다. Setup Prototype Loop 를 먼저 실행하세요.");
                    return;
                }
                stages.Add(stage);
            }

            ConfigureTitleScene(stages);
            AssetDatabase.SaveAssets();

            if (!string.IsNullOrEmpty(originalScenePath) && System.IO.File.Exists(originalScenePath))
                EditorSceneManager.OpenScene(originalScenePath, OpenSceneMode.Single);
            Debug.Log("[TourPrototypeSetup] Title 런처 스테이지 5개로 복구했습니다.");
        }

        [MenuItem("Tools/Tour/Setup Prototype Loop", false, 0)]
        public static void Setup()
        {
            string originalScenePath = SceneManager.GetActiveScene().path;
            EnsureFolder(TourSettingsFolder);

            StageDefinition[] stages =
            {
                CreateOrUpdateStage("Stage01", "stage_01", "Alley Busking", RunNodeType.Performance, false, 5000, 120f, "intro_stage_01", "reward_basic"),
                CreateOrUpdateStage("Stage02", "stage_02", "Basement Live Hall", RunNodeType.Performance, false, 6500, 120f, "intro_stage_02", "reward_basic"),
                CreateOrUpdateStage("Stage03", "stage_03", "Rock Festival", RunNodeType.Performance, false, 8000, 120f, "intro_stage_03", "reward_advanced"),
                CreateOrUpdateStage("Stage04", "stage_04", "Arena Headliner", RunNodeType.Performance, false, 11000, 120f, "intro_stage_04", "reward_advanced"),
                CreateOrUpdateStage("Stage05Boss", "stage_05_boss", "World Stadium Rival Battle", RunNodeType.ElitePerformance, true, 12000, 120f, "intro_stage_05_boss", "")
            };

            ConfigureHubScene();
            ConfigureTitleScene(stages);
            ConfigureMainScene();
            AddHubToBuildSettings();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (!string.IsNullOrEmpty(originalScenePath) && System.IO.File.Exists(originalScenePath))
                EditorSceneManager.OpenScene(originalScenePath, OpenSceneMode.Single);
            else
                EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);

            Debug.Log("[TourPrototypeSetup] 전체 임시 투어 루프 설정을 완료했습니다.");
        }

        static StageDefinition CreateOrUpdateStage(
            string assetName,
            string stageId,
            string displayName,
            RunNodeType nodeType,
            bool isBoss,
            int targetScore,
            float duration,
            string preDialogueId,
            string rewardTableId)
        {
            string path = $"{TourSettingsFolder}/{assetName}.asset";
            StageDefinition stage = AssetDatabase.LoadAssetAtPath<StageDefinition>(path);
            if (stage == null)
            {
                stage = ScriptableObject.CreateInstance<StageDefinition>();
                AssetDatabase.CreateAsset(stage, path);
            }

            var serialized = new SerializedObject(stage);
            serialized.FindProperty("stageId").stringValue = stageId;
            serialized.FindProperty("displayName").stringValue = displayName;
            serialized.FindProperty("nodeType").enumValueIndex = (int)nodeType;
            serialized.FindProperty("isBoss").boolValue = isBoss;
            serialized.FindProperty("targetScore").intValue = targetScore;
            serialized.FindProperty("duration").floatValue = duration;
            serialized.FindProperty("preDialogueId").stringValue = preDialogueId;
            serialized.FindProperty("rewardTableId").stringValue = rewardTableId;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(stage);
            return stage;
        }

        static void ConfigureHubScene()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(HubScenePath) == null)
            {
                Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EnsureHubCamera();
                var root = new GameObject("[TourHub]");
                root.AddComponent<TourPrototypeUI>();
                EditorSceneManager.SaveScene(scene, HubScenePath);
                return;
            }

            Scene existingScene = EditorSceneManager.OpenScene(HubScenePath, OpenSceneMode.Single);
            EnsureHubCamera();
            TourPrototypeUI ui = Object.FindFirstObjectByType<TourPrototypeUI>(FindObjectsInactive.Include);
            if (ui == null)
            {
                var root = new GameObject("[TourHub]");
                root.AddComponent<TourPrototypeUI>();
            }
            EditorSceneManager.SaveScene(existingScene);
        }

        static void EnsureHubCamera()
        {
            if (Object.FindFirstObjectByType<Camera>(FindObjectsInactive.Include) != null) return;

            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(0x16, 0x12, 0x1C, 0xFF);
            camera.orthographic = true;
            cameraObject.AddComponent<AudioListener>();
        }

        static void ConfigureTitleScene(IReadOnlyList<StageDefinition> stages)
        {
            Scene scene = EditorSceneManager.OpenScene(TitleScenePath, OpenSceneMode.Single);
            GameObject startButtonObject = GameObject.Find("Canvas/Buttons/StartButton");
            if (startButtonObject == null)
                throw new MissingReferenceException("Title 씬의 StartButton을 찾지 못했습니다.");

            TourTitleLauncher launcher = startButtonObject.GetComponent<TourTitleLauncher>();
            if (launcher == null) launcher = Undo.AddComponent<TourTitleLauncher>(startButtonObject);
            launcher.Configure(stages, "TourHub");

            Button button = startButtonObject.GetComponent<Button>();
            for (int i = button.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
            {
                string method = button.onClick.GetPersistentMethodName(i);
                if (method == "StartGameNextScene" || method == nameof(TourTitleLauncher.StartTour))
                    UnityEventTools.RemovePersistentListener(button.onClick, i);
            }
            UnityEventTools.AddPersistentListener(button.onClick, launcher.StartTour);

            EditorUtility.SetDirty(button);
            EditorUtility.SetDirty(launcher);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        static void ConfigureMainScene()
        {
            Scene scene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
            TourPerformanceBridge bridge = Object.FindFirstObjectByType<TourPerformanceBridge>(FindObjectsInactive.Include);
            if (bridge == null)
            {
                var root = new GameObject("[TourPerformance]");
                bridge = root.AddComponent<TourPerformanceBridge>();
            }

            EditorUtility.SetDirty(bridge);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        static void AddHubToBuildSettings()
        {
            List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.All(entry => entry.path != HubScenePath))
                scenes.Add(new EditorBuildSettingsScene(HubScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        static void EnsureFolder(string path)
        {
            string[] parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
#endif
