using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ContextStage.EditorTools
{
    /// <summary>
    /// 프로젝트 전역 폰트를 DungGeunMo 로 일괄 적용하는 에디터 툴.
    ///
    /// 레거시 UnityEngine.UI.Text 는 .ttf(Font) 를 바로 쓰고,
    /// TMP_Text 는 .ttf 로 만든 TMP Font Asset 이 필요하므로 없으면 자동 생성한다.
    ///
    ///   Tools/Project/Apply DungGeunMo Font To Scene   현재 씬의 모든 텍스트에 적용
    ///   Tools/Project/Apply DungGeunMo Font To Prefabs  카드·UI 프리팹에 적용
    /// </summary>
    public static class ProjectFontTool
    {
        const string FontPath = "Assets/Font/DungGeunMo.ttf";
        const string TmpFontFolder = "Assets/Font";
        const string TmpFontPath = "Assets/Font/DungGeunMo SDF.asset";

        // ---------------- 공용 접근자 (셋업 메뉴들이 쓴다) ----------------

        /// <summary>레거시 Text 용 폰트. 없으면 내장 폰트로 폴백.</summary>
        public static Font LegacyFont
        {
            get
            {
                var font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
                return font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }
        }

        /// <summary>TMP 용 폰트 에셋. 없으면 생성 시도, 실패하면 TMP 기본 폰트.</summary>
        public static TMP_FontAsset TmpFont
        {
            get
            {
                var asset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TmpFontPath);
                if (asset == null) asset = CreateTmpFontAsset();
                else RepairTmpFontAsset(asset);
                return asset != null ? asset : TMP_Settings.defaultFontAsset;
            }
        }

        // ---------------- 메뉴 ----------------

        [MenuItem("Tools/Project/Apply DungGeunMo Font To Scene", false, 0)]
        public static void ApplyToScene()
        {
            Font legacy = LegacyFont;
            TMP_FontAsset tmp = TmpFont;

            int legacyCount = 0, tmpCount = 0;

            foreach (var text in Object.FindObjectsByType<Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Undo.RecordObject(text, "Apply Font");
                text.font = legacy;
                EditorUtility.SetDirty(text);
                legacyCount++;
            }

            if (tmp != null)
            {
                foreach (var text in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    Undo.RecordObject(text, "Apply Font");
                    text.font = tmp;
                    EditorUtility.SetDirty(text);
                    tmpCount++;
                }
            }

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log($"[Font] 씬 적용 완료 — 레거시 Text {legacyCount}개, TMP {tmpCount}개. (씬을 Ctrl+S 로 저장할 것)");
        }

        [MenuItem("Tools/Project/Apply DungGeunMo Font To Prefabs", false, 1)]
        public static void ApplyToPrefabs()
        {
            Font legacy = LegacyFont;
            TMP_FontAsset tmp = TmpFont;

            // 카드·오디언스·UI 프리팹만 훑는다 (TMP 패키지 프리팹은 건드리지 않는다)
            string[] folders = { "Assets/Card_Prefab", "Assets/Prefabs" };
            var guids = AssetDatabase.FindAssets("t:Prefab", folders);
            int changed = 0;

            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                bool touched = false;

                foreach (var text in root.GetComponentsInChildren<Text>(true))
                {
                    if (text.font == legacy) continue;
                    text.font = legacy;
                    touched = true;
                }
                if (tmp != null)
                {
                    foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
                    {
                        if (text.font == tmp) continue;
                        text.font = tmp;
                        touched = true;
                    }
                }

                if (touched)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    changed++;
                }
                PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[Font] 프리팹 적용 완료 — {changed}개 프리팹 변경.");
        }

        [MenuItem("Tools/Project/Create DungGeunMo TMP Font Asset", false, 20)]
        public static void CreateTmpFontAssetMenu()
        {
            var asset = CreateTmpFontAsset();
            if (asset != null)
            {
                Selection.activeObject = asset;
                Debug.Log($"[Font] TMP 폰트 에셋 준비 완료: {TmpFontPath}");
            }
        }

        // ---------------- TMP 폰트 생성 ----------------

        static TMP_FontAsset CreateTmpFontAsset()
        {
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TmpFontPath);
            if (existing != null)
            {
                RepairTmpFontAsset(existing);
                return existing;
            }

            var sourceFont = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
            if (sourceFont == null)
            {
                Debug.LogWarning($"[Font] {FontPath} 를 찾지 못했습니다.");
                return null;
            }

            // Dynamic 모드로 생성하면 필요한 글리프를 런타임에 아틀라스로 굽는다.
            // 한글은 글자 수가 많아 Static 으로 전부 굽기 어려우므로 Dynamic 이 안전하다.
            var asset = TMP_FontAsset.CreateFontAsset(sourceFont);
            if (asset == null)
            {
                Debug.LogError("[Font] TMP 폰트 에셋 생성에 실패했습니다. " +
                               "Window/TextMeshPro/Font Asset Creator 로 수동 생성하세요.");
                return null;
            }

            asset.name = "DungGeunMo SDF";
            AssetDatabase.CreateAsset(asset, TmpFontPath);

            // CreateFontAsset 이 만든 아틀라스 텍스처·머티리얼을 에셋에 하위로 붙인다
            if (asset.atlasTextures != null)
            {
                foreach (var tex in asset.atlasTextures)
                {
                    if (tex != null && !AssetDatabase.Contains(tex))
                    {
                        tex.name = "DungGeunMo Atlas";
                        AssetDatabase.AddObjectToAsset(tex, asset);
                    }
                }
            }
            if (asset.material != null && !AssetDatabase.Contains(asset.material))
            {
                asset.material.name = "DungGeunMo Material";
                AssetDatabase.AddObjectToAsset(asset.material, asset);
            }

            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(TmpFontPath);
            RepairTmpFontAsset(asset);
            return asset;
        }

        /// <summary>
        /// Dynamic TMP 폰트가 런타임에 한글 글리프를 추가할 수 있도록
        /// 소스 TTF 참조와 Multi Atlas 설정을 복구한다.
        /// </summary>
        static void RepairTmpFontAsset(TMP_FontAsset asset)
        {
            if (asset == null) return;

            Font sourceFont = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
            if (sourceFont == null)
            {
                Debug.LogWarning($"[Font] {FontPath} 를 찾지 못했습니다.");
                return;
            }

            var serialized = new SerializedObject(asset);
            SerializedProperty source = serialized.FindProperty("m_SourceFontFile");
            SerializedProperty sourceGuid = serialized.FindProperty("m_SourceFontFileGUID");
            SerializedProperty population = serialized.FindProperty("m_AtlasPopulationMode");
            SerializedProperty multiAtlas = serialized.FindProperty("m_IsMultiAtlasTexturesEnabled");

            if (source != null) source.objectReferenceValue = sourceFont;
            if (sourceGuid != null)
                sourceGuid.stringValue = AssetDatabase.AssetPathToGUID(FontPath);
            if (population != null)
                population.enumValueIndex = (int)AtlasPopulationMode.Dynamic;
            if (multiAtlas != null) multiAtlas.boolValue = true;

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
        }
    }
}
