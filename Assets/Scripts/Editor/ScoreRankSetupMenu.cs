using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using static ContextStage.EditorTools.EditorSetupUtility;

namespace ContextStage.EditorTools
{
    /// <summary>
    /// 우측 상단 랭크 아이콘 + 목표 달성 게이지 셋업.
    ///
    ///   Tools/UI/Setup Score Rank HUD
    ///
    /// 기존 ScoreUI("SCORE 4200 / 5000") 옆에 랭크와 세로 막대를 붙인다.
    /// UI 스프라이트가 Multiple 모드로 들어와 Image 에 꽂히지 않으므로 Single 로 교정도 함께 한다.
    /// </summary>
    public static class ScoreRankSetupMenu
    {
        const string GaugeFolder = "Assets/Sprites/UI/Gauge";
        const string RankFolder = "Assets/Sprites/UI/Rank";
        const string GaugeBackName = "score_bar_back";
        const string GaugeFillName = "score_bar_guage";

        /// <summary>랭크 순서 = ScoreRankUI 의 rankTiers 순서와 반드시 일치해야 한다.</summary>
        static readonly string[] RankOrder = { "F", "D", "C", "B", "A", "S" };

        [MenuItem("Tools/UI/Setup Score Rank HUD", false, 0)]
        public static void Setup()
        {
            FixUiSpriteImport();

            // 기존 점수 텍스트를 기준점으로 삼는다 (같은 HUD 에 붙이기 위함)
            ScoreUI scoreUI = Object.FindFirstObjectByType<ScoreUI>(FindObjectsInactive.Include);
            Transform host = ResolveHost(scoreUI);
            if (host == null)
            {
                Debug.LogError("[ScoreRank] UI Canvas 를 찾지 못했습니다. 먼저 점수 HUD 를 만드세요.");
                return;
            }

            var root = host.Find("ScoreRankHUD");
            if (root == null)
            {
                var go = new GameObject("ScoreRankHUD", typeof(RectTransform));
                Undo.RegisterCreatedObjectUndo(go, "Create ScoreRankHUD");
                go.transform.SetParent(host, false);
                go.layer = LayerMask.NameToLayer("UI");
                root = go.transform;

                var rect = (RectTransform)root;
                rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(1f, 1f);
                rect.anchoredPosition = new Vector2(-40f, -30f);
                rect.sizeDelta = new Vector2(150f, 120f);
            }

            // ---- 랭크 아이콘 (48x48 원본 → 96 로 키워 배치) ----
            Image rankImage = EnsureImage(root, "RankIcon", new Vector2(1f, 1f), new Vector2(-70f, 0f), new Vector2(96f, 96f));
            rankImage.preserveAspect = true;
            rankImage.sprite = LoadSprite(RankFolder, "F");

            // ---- 게이지 (20x54 원본 → 세로 막대. 뒤판 + 채움) ----
            Image gaugeBack = EnsureImage(root, "GaugeBack", new Vector2(1f, 1f), new Vector2(-8f, 0f), new Vector2(28f, 100f));
            gaugeBack.sprite = LoadSprite(GaugeFolder, GaugeBackName);
            gaugeBack.type = Image.Type.Simple;

            Image gaugeFill = EnsureImage(gaugeBack.rectTransform, "GaugeFill", new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, stretch: true);
            gaugeFill.sprite = LoadSprite(GaugeFolder, GaugeFillName);
            gaugeFill.type = Image.Type.Filled;                       // 아래에서 위로 차오른다
            gaugeFill.fillMethod = Image.FillMethod.Vertical;
            gaugeFill.fillOrigin = (int)Image.OriginVertical.Bottom;
            gaugeFill.fillAmount = 0f;

            // ---- 컴포넌트 ----
            var ui = EnsureComponent<ScoreRankUI>(root.gameObject);
            var serialized = new SerializedObject(ui);
            SetObjectReference(serialized, "rankImage", rankImage);
            SetObjectReference(serialized, "gaugeFill", gaugeFill);
            AssignRankIcons(serialized);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Selection.activeGameObject = root.gameObject;
            Debug.Log("[ScoreRank] 셋업 완료 — 랭크 아이콘 + 목표 게이지 배치. " +
                      $"목표 점수는 PerformanceTimerConfig.targetScore({PerformanceTimer.TargetScore}) 를 따른다. " +
                      "씬을 Ctrl+S 로 저장할 것!");
        }

        /// <summary>rankTiers 배열의 icon 을 F→S 순서로 채운다. minRatio 는 컴포넌트 기본값을 유지한다.</summary>
        static void AssignRankIcons(SerializedObject serialized)
        {
            SerializedProperty tiers = serialized.FindProperty("rankTiers");
            if (tiers == null) return;

            for (int i = 0; i < tiers.arraySize && i < RankOrder.Length; i++)
            {
                SerializedProperty element = tiers.GetArrayElementAtIndex(i);
                SerializedProperty label = element.FindPropertyRelative("label");
                string wanted = label != null && !string.IsNullOrEmpty(label.stringValue)
                    ? label.stringValue
                    : RankOrder[i];

                Sprite icon = LoadSprite(RankFolder, wanted);
                if (icon == null) continue;
                element.FindPropertyRelative("icon").objectReferenceValue = icon;
            }
        }

        // ---------------- UI 스프라이트 임포트 교정 ----------------

        /// <summary>
        /// 게이지·랭크 스프라이트가 Multiple 모드로 들어와 있으면 Image 에 꽂을 수 없다
        /// (메인 Sprite 에셋이 없어 LoadAssetAtPath&lt;Sprite&gt; 가 null 을 돌려준다).
        /// 단일 이미지들이므로 Single 로 되돌리고 픽셀 필터를 적용한다.
        /// </summary>
        [MenuItem("Tools/UI/Fix UI Sprite Import", false, 20)]
        public static void FixUiSpriteImport()
        {
            int fixedCount = 0;
            foreach (string folder in new[] { GaugeFolder, RankFolder })
            {
                if (!AssetDatabase.IsValidFolder(folder)) continue;

                foreach (string path in FindPngPaths(folder))
                {
                    var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (importer == null) continue;

                    bool changed = false;
                    if (importer.textureType != TextureImporterType.Sprite)
                    {
                        importer.textureType = TextureImporterType.Sprite;
                        changed = true;
                    }
                    if (importer.spriteImportMode != SpriteImportMode.Single)
                    {
                        importer.spriteImportMode = SpriteImportMode.Single;
                        changed = true;
                    }
                    if (importer.filterMode != FilterMode.Point)
                    {
                        importer.filterMode = FilterMode.Point;
                        changed = true;
                    }
                    if (importer.textureCompression != TextureImporterCompression.Uncompressed)
                    {
                        importer.textureCompression = TextureImporterCompression.Uncompressed;
                        changed = true;
                    }
                    if (importer.mipmapEnabled)
                    {
                        importer.mipmapEnabled = false;
                        changed = true;
                    }

                    if (!changed) continue;
                    importer.SaveAndReimport();
                    fixedCount++;
                }
            }

            if (fixedCount > 0)
            {
                AssetDatabase.Refresh();
                Debug.Log($"[ScoreRank] UI 스프라이트 임포트 교정 — {fixedCount}장 (Single / Point).");
            }
        }

        // ---------------- 헬퍼 ----------------

        static Transform ResolveHost(ScoreUI scoreUI)
        {
            if (scoreUI != null)
            {
                Transform parent = scoreUI.transform.parent;
                if (parent != null) return parent;

                var canvas = scoreUI.GetComponentInParent<Canvas>(true);
                if (canvas != null) return canvas.transform;
            }

            var canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var c in canvases)
                if (c.name == "HypeCanvas") return c.transform;
            return canvases.Length > 0 ? canvases[0].transform : null;
        }

        static Image EnsureImage(
            Transform parent, string name,
            Vector2 anchor, Vector2 anchoredPosition, Vector2 size, bool stretch = false)
        {
            Transform existing = parent.Find(name);
            GameObject go;
            if (existing != null)
            {
                go = existing.gameObject;
            }
            else
            {
                go = new GameObject(name, typeof(RectTransform));
                Undo.RegisterCreatedObjectUndo(go, "Create " + name);
                go.transform.SetParent(parent, false);
                go.layer = LayerMask.NameToLayer("UI");
            }

            var rect = (RectTransform)go.transform;
            if (stretch)
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
            }
            else
            {
                rect.anchorMin = rect.anchorMax = anchor;
                rect.pivot = new Vector2(1f, 1f);
                rect.anchoredPosition = anchoredPosition;
                rect.sizeDelta = size;
            }

            var image = EnsureComponent<Image>(go);
            image.raycastTarget = false;
            return image;
        }

        /// <summary>Single 모드면 메인 에셋, Multiple 이면 첫 서브 스프라이트를 돌려준다.</summary>
        static Sprite LoadSprite(string folder, string fileNameWithoutExtension)
        {
            string path = FindPngPaths(folder)
                .FirstOrDefault(p => System.IO.Path.GetFileNameWithoutExtension(p) == fileNameWithoutExtension);
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogWarning($"[ScoreRank] '{fileNameWithoutExtension}.png' 를 {folder} 에서 찾지 못했습니다.");
                return null;
            }

            var main = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (main != null) return main;

            return AssetDatabase.LoadAllAssetRepresentationsAtPath(path).OfType<Sprite>().FirstOrDefault();
        }

        static IEnumerable<string> FindPngPaths(string folder)
        {
            if (!AssetDatabase.IsValidFolder(folder)) return System.Array.Empty<string>();

            return AssetDatabase.FindAssets("t:Texture2D", new[] { folder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => p.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase))
                .Distinct();
        }
    }
}
