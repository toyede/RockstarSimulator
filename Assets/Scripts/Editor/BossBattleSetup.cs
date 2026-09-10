using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using static ContextStage.EditorTools.EditorSetupUtility;

namespace ContextStage.EditorTools
{
    /// <summary>
    /// 보스전 셋업.
    ///
    ///   Tools/Tour/Setup Boss Battle
    ///     1. Settings/Tour/RuleConfigs/BossBattle.asset (없을 때만)
    ///     2. Stage05Boss.asset venueRuleIds = special_audience_requests, boss_battle
    ///     3. Main 씬 [StageRuntime]/Rule_BossBattle 에 BossBattleRule + BossBattleUI + RivalStagePlaceholder
    ///        + BossArenaLayout/BossCameraDirector/BossStagePresentation(3화면 연출) 배치, 디렉터 룰 목록에 추가
    ///   실행 후 Ctrl+S. (Tools/Tour/Setup Stage Runtime 을 먼저 실행해 둘 것)
    /// </summary>
    public static class BossBattleSetup
    {
        const string ConfigPath = "Assets/Settings/Tour/RuleConfigs/BossBattle.asset";
        const string BossStagePath = "Assets/Settings/Tour/Stage05Boss.asset";
        const string MainScenePath = "Assets/Scenes/Main.unity";
        const string RuntimeRootName = "[StageRuntime]";
        const string RuleObjectName = "Rule_BossBattle";
        const string RuleId = "boss_battle";

        [MenuItem("Tools/Tour/Setup Boss Battle", false, 12)]
        public static void Setup()
        {
            EnsureFolder("Assets/Settings/Tour/RuleConfigs");

            var config = AssetDatabase.LoadAssetAtPath<BossBattleConfig>(ConfigPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<BossBattleConfig>();
                AssetDatabase.CreateAsset(config, ConfigPath);
            }

            ConfigureBossStage();
            AssetDatabase.SaveAssets();

            ConfigureMainScene(config);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Boss] 보스전 셋업 완료. Main 씬을 Ctrl+S 로 저장할 것!", config);
        }

        static void ConfigureBossStage()
        {
            var stage = AssetDatabase.LoadAssetAtPath<StageDefinition>(BossStagePath);
            if (stage == null)
            {
                Debug.LogWarning($"[Boss] {BossStagePath} 가 없습니다. Tools/Tour/Setup Prototype Loop 를 먼저 실행하세요.");
                return;
            }

            var serialized = new SerializedObject(stage);
            SerializedProperty rules = serialized.FindProperty("venueRuleIds");
            rules.arraySize = 3;
            rules.GetArrayElementAtIndex(0).stringValue = "special_audience_requests";
            rules.GetArrayElementAtIndex(1).stringValue = RuleId;
            rules.GetArrayElementAtIndex(2).stringValue = "stadium_contested_fan";
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(stage);
        }

        static void ConfigureMainScene(BossBattleConfig config)
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != MainScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                scene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
            }

            GameObject root = GameObject.Find(RuntimeRootName);
            if (root == null)
            {
                Debug.LogError("[Boss] Main 씬에 [StageRuntime] 이 없습니다. Tools/Tour/Setup Stage Runtime 을 먼저 실행하세요.");
                return;
            }

            Transform child = root.transform.Find(RuleObjectName);
            GameObject ruleObject;
            if (child != null) ruleObject = child.gameObject;
            else
            {
                ruleObject = new GameObject(RuleObjectName);
                Undo.RegisterCreatedObjectUndo(ruleObject, "Create Rule_BossBattle");
                ruleObject.transform.SetParent(root.transform, false);
            }

            var rule = EnsureComponent<BossBattleRule>(ruleObject);
            rule.EditorSetRuleId(RuleId);
            rule.EditorConfigure(config);
            EditorUtility.SetDirty(rule);

            var ui = EnsureComponent<BossBattleUI>(ruleObject);
            ui.EditorConfigure(ProjectFontTool.TmpFont, config.RivalName, config.PatternsToClear);
            EditorUtility.SetDirty(ui);

            var placeholder = EnsureComponent<RivalStagePlaceholder>(ruleObject);
            placeholder.EditorSetFanPool(config.RivalFanPool);
            EditorUtility.SetDirty(placeholder);

            // 3화면 연출: 배치 → 카메라 → 연출 순으로 (RequireComponent 의존)
            EnsureComponent<BossArenaLayout>(ruleObject);
            EnsureComponent<BossCameraDirector>(ruleObject);
            var presentation = EnsureComponent<BossStagePresentation>(ruleObject);
            presentation.EditorConfigure(config);
            EditorUtility.SetDirty(presentation);

            var director = root.GetComponent<StageRuntimeDirector>();
            if (director != null)
            {
                director.EditorAddRule(rule);
                EditorUtility.SetDirty(director);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = ruleObject;
        }
    }
}
