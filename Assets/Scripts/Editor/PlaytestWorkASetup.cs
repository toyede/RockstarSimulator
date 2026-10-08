using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace ContextStage.EditorTools
{
    /// <summary>기존 씬 배치를 재생성하지 않고 작업 A의 대상 설정만 갱신한다.</summary>
    public static class PlaytestWorkASetup
    {
        [MenuItem("Tools/Playtest/Apply Work A To Main")]
        public static void Apply()
        {
            if (EditorApplication.isPlaying || UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "Main")
                throw new InvalidOperationException("Main 씬의 Edit Mode에서 실행하세요.");

            foreach (var boss in UnityEngine.Object.FindObjectsByType<BossStagePresentation>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var data = Edit(boss);
                data.FindProperty("hideTimerHud").boolValue = false;
                data.ApplyModifiedProperties();
            }
            foreach (var ui in UnityEngine.Object.FindObjectsByType<BossBattleUI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var data = Edit(ui);
                data.FindProperty("patternPanelPosition").vector2Value = new Vector2(24f, -205f);
                data.FindProperty("patternPanelSize").vector2Value = new Vector2(520f, 164f);
                data.ApplyModifiedProperties();
            }

            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Font/DungGeunMo SDF.asset");
            foreach (var view in UnityEngine.Object.FindObjectsByType<ResultNewspaperView>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                foreach (TMP_Text text in view.GetComponentsInChildren<TMP_Text>(true))
                {
                    Undo.RecordObject(text, "Work A result typography");
                    Undo.RecordObject(text.rectTransform, "Work A result text bounds");
                    if (font != null) text.font = font;
                    text.textWrappingMode = TextWrappingModes.Normal;
                    text.enableAutoSizing = true;
                    text.fontSizeMax = text.fontSize;
                    text.fontSizeMin = Mathf.Max(20f, text.fontSize - 4f);
                    if (text.name == "Title")
                    {
                        text.fontSize = text.fontSizeMax = 42f;
                        text.fontSizeMin = 36f;
                        text.rectTransform.sizeDelta = new Vector2(1125f, 62f);
                    }
                    if (text.name == "ArticleBody")
                    {
                        text.rectTransform.sizeDelta = new Vector2(800f, 54f);
                        text.rectTransform.anchoredPosition = new Vector2(-250f, -374f);
                    }
                    EditorUtility.SetDirty(text);
                }

            foreach (var controller in UnityEngine.Object.FindObjectsByType<StageLightController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var data = Edit(controller);
                var left = data.FindProperty("leftStageLight").objectReferenceValue as Light2D;
                var right = data.FindProperty("rightStageLight").objectReferenceValue as Light2D;
                if (left == null || right == null) continue;
                var profiles = data.FindProperty("stageGeometry");
                // Stage 1과 StageSet의 Transform은 전혀 수정하지 않는다.
                string[] ids = { "stage_02", "stage_03", "stage_04", "stage_05_boss" };
                Vector2[] angles = { new Vector2(28, 38), new Vector2(42, 60), new Vector2(32, 48), new Vector2(40, 58) };
                float[] spread = { 0.91f, 1.02f, 1.04f, 1.04f };
                float[] radii = { 11f, 13f, 14f, 15f };
                profiles.arraySize = ids.Length;
                for (int i = 0; i < ids.Length; i++)
                {
                    var entry = profiles.GetArrayElementAtIndex(i);
                    entry.FindPropertyRelative("stageId").stringValue = ids[i];
                    Vector3 lp = left.transform.localPosition, rp = right.transform.localPosition;
                    lp.x *= spread[i]; rp.x *= spread[i];
                    lp.y += i == 0 ? 0f : 0.2f; rp.y += i == 0 ? 0f : 0.2f;
                    entry.FindPropertyRelative("leftPosition").vector3Value = lp;
                    entry.FindPropertyRelative("rightPosition").vector3Value = rp;
                    entry.FindPropertyRelative("leftRotation").vector3Value = left.transform.localEulerAngles;
                    entry.FindPropertyRelative("rightRotation").vector3Value = right.transform.localEulerAngles;
                    entry.FindPropertyRelative("angleRange").vector2Value = angles[i];
                    entry.FindPropertyRelative("radius").floatValue = radii[i];
                    entry.FindPropertyRelative("rightRadius").floatValue = radii[i];
                    entry.FindPropertyRelative("sweep").floatValue = i == 0 ? 6f : 12f;
                    entry.FindPropertyRelative("speed").floatValue = i == 0 ? 0.12f : 0.22f;
                }
                data.ApplyModifiedProperties();
            }
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log("[Work A] 타이머·미션·결과 폰트·Stage 2~5 조명 설정 완료. Stage 1 배치 보존.");
        }

        static SerializedObject Edit(UnityEngine.Object target)
        {
            Undo.RecordObject(target, "Playtest Work A");
            return new SerializedObject(target);
        }

        public static string Audit()
        {
            var sets = UnityEngine.Object.FindObjectsByType<StageSet>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            return string.Join("\n", sets.Select(s => s.StageId + ": " + string.Join(", ",
                s.GetComponentsInChildren<SpriteRenderer>(true).Select(r => r.name + "=" + (r.sprite != null ? r.sprite.name : "MISSING")))));
        }
    }
}
