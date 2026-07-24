using System.IO;
using UnityEditor;
using UnityEngine;

namespace GameJamKit.EditorTools
{
    /// <summary>
    /// 킷 배포/셋업용 에디터 메뉴. Tools/GameJamKit 아래에 모여 있다.
    /// </summary>
    public static class GameJamKitMenu
    {
        const string KitRoot = "Assets/GameJamKit";
        const string ResourcesPath = KitRoot + "/Resources";

        // ---------------- .unitypackage 내보내기 ----------------

        [MenuItem("Tools/GameJamKit/Export .unitypackage", false, 0)]
        public static void ExportPackage()
        {
            if (!AssetDatabase.IsValidFolder(KitRoot))
            {
                EditorUtility.DisplayDialog("GameJamKit", $"{KitRoot} 폴더를 찾을 수 없습니다.", "확인");
                return;
            }

            string defaultName = $"GameJamKit_{System.DateTime.Now:yyyyMMdd}.unitypackage";
            string path = EditorUtility.SaveFilePanel("GameJamKit 내보내기", "", defaultName, "unitypackage");
            if (string.IsNullOrEmpty(path)) return;

            AssetDatabase.ExportPackage(
                KitRoot,
                path,
                ExportPackageOptions.Recurse | ExportPackageOptions.IncludeDependencies);

            Debug.Log($"[GameJamKit] 내보내기 완료: {path}");
            EditorUtility.RevealInFinder(path);
        }

        // ---------------- 기본 에셋 생성 ----------------

        [MenuItem("Tools/GameJamKit/Create Default Assets", false, 20)]
        public static void CreateDefaultAssets()
        {
            EnsureFolder(ResourcesPath);

            CreateIfMissing<SoundLibrary>(ResourcesPath + "/SoundLibrary.asset");
            var palette = CreateIfMissing<ColorPalette>(ResourcesPath + "/ColorPalette.asset");
            if (palette != null) FillDefaultPalette(palette);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[GameJamKit] 기본 에셋을 {ResourcesPath} 에 준비했습니다.");
        }

        static T CreateIfMissing<T>(string path) where T : ScriptableObject
        {
            if (AssetDatabase.LoadAssetAtPath<T>(path) != null) return null; // 이미 있으면 건드리지 않는다

            var asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        static void FillDefaultPalette(ColorPalette palette)
        {
            var so = new SerializedObject(palette);
            var list = so.FindProperty("entries");

            (string key, string hex)[] defaults =
            {
                ("primary",   "#4CC9F0"),
                ("secondary", "#F72585"),
                ("accent",    "#FFD166"),
                ("bg",        "#1B1B2F"),
                ("text",      "#F2F2F2"),
                ("enemy",     "#EF476F"),
                ("player",    "#06D6A0"),
            };

            list.arraySize = defaults.Length;
            for (int i = 0; i < defaults.Length; i++)
            {
                var element = list.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("key").stringValue = defaults[i].key;
                element.FindPropertyRelative("hex").stringValue = defaults[i].hex;
                element.FindPropertyRelative("color").colorValue = Palette.FromHex(defaults[i].hex);
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------------- 씬 셋업 ----------------

        [MenuItem("Tools/GameJamKit/Create Managers In Scene", false, 21)]
        public static void CreateManagers()
        {
            var root = GameObject.Find("[Managers]") ?? new GameObject("[Managers]");

            AddChildManager<GameManager>(root, "GameManager");
            AddChildManager<AudioManager>(root, "AudioManager");
            AddChildManager<PoolManager>(root, "PoolManager");
            AddChildManager<UIManager>(root, "UIManager");
            AddChildManager<CameraShake>(root, "CameraShake");

            Selection.activeGameObject = root;
            Debug.Log("[GameJamKit] 매니저 오브젝트를 생성했습니다. " +
                      "(ScreenFader/TimerRunner 는 런타임에 자동 생성되므로 배치할 필요 없습니다)");
        }

        static void AddChildManager<T>(GameObject root, string name) where T : MonoBehaviour
        {
            if (root.GetComponentInChildren<T>(true) != null) return;

            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            go.AddComponent<T>();
            Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        }

        // ---------------- 유틸 ----------------

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            var parent = Path.GetDirectoryName(path).Replace('\\', '/');
            var leaf = Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        [MenuItem("Tools/GameJamKit/Clear Saved Data (PlayerPrefs)", false, 40)]
        public static void ClearSavedData()
        {
            if (!EditorUtility.DisplayDialog("GameJamKit",
                    "PlayerPrefs 전체를 삭제합니다. 계속할까요?", "삭제", "취소")) return;

            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
            Debug.Log("[GameJamKit] PlayerPrefs 를 삭제했습니다.");
        }
    }
}
