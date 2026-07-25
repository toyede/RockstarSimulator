using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using static ContextStage.EditorTools.EditorSetupUtility;

namespace ContextStage.EditorTools
{
    /// <summary>
    /// 배경 프레임 애니메이션 셋업.
    ///
    /// <c>Tools/UI/Setup Animated Background</c> 한 번이면:
    /// <list type="number">
    /// <item>씬의 <c>AnimatedBackground</c> 오브젝트에 컴포넌트를 붙이고</item>
    /// <item>스프라이트 폴더를 번호순으로 훑어 프레임 배열을 채우고</item>
    /// <item>SpriteRenderer 를 연결하고 첫 프레임을 꽂아 둔다</item>
    /// </list>
    /// 29장을 손으로 끌어다 넣지 않는다. 프레임이 늘어나면 다시 실행하기만 하면 된다.
    ///
    /// 폴더는 이미 꽂혀 있는 스프라이트의 위치에서 알아내므로,
    /// 아트가 폴더를 옮겨도 첫 프레임만 다시 지정하면 그대로 동작한다.
    /// </summary>
    public static class AnimatedBackgroundSetup
    {
        const string ObjectName = "AnimatedBackground";
        const string DefaultFrameFolder = "Assets/Sprites/UI/Background/Bgs";
        const string DefaultFramePrefix = "BG_";

        /// <summary>파일명 끝의 연속된 숫자. BG_0028 → 28</summary>
        static readonly Regex TrailingNumber = new Regex(@"(\d+)\s*$", RegexOptions.Compiled);

        [MenuItem("Tools/UI/Setup Animated Background", false, 30)]
        public static void Setup()
        {
            GameObject target = ResolveTarget();
            if (target == null)
            {
                Debug.LogError(
                    $"[AnimatedBackground] 씬에서 '{ObjectName}' 오브젝트를 찾지 못했습니다. " +
                    "Title 씬을 연 뒤 다시 실행하거나, 대상 오브젝트를 선택하고 실행하세요.");
                return;
            }

            var view = EnsureComponent<AnimatedBackground>(target);
            var spriteRenderer = target.GetComponent<SpriteRenderer>();

            string folder = ResolveFrameFolder(view, spriteRenderer);
            List<Sprite> frames = LoadFrames(folder, DefaultFramePrefix);
            if (frames.Count == 0)
            {
                Debug.LogError(
                    $"[AnimatedBackground] '{folder}' 에서 '{DefaultFramePrefix}0000' 형식의 " +
                    "스프라이트를 찾지 못했습니다. 텍스처 타입이 Sprite 인지 확인하세요.");
                return;
            }

            Undo.RecordObject(view, "Setup Animated Background");
            var serialized = new SerializedObject(view);

            SerializedProperty framesProperty = serialized.FindProperty("clip.frames");
            if (framesProperty == null)
            {
                Debug.LogError("[AnimatedBackground] 'clip.frames' 필드를 찾지 못했습니다.", view);
                return;
            }

            framesProperty.arraySize = frames.Count;
            for (int i = 0; i < frames.Count; i++)
                framesProperty.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];

            if (spriteRenderer != null)
                SetObjectReference(serialized, "spriteRenderer", spriteRenderer);

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(view);

            // 에디터에서도 배경이 보이도록 첫 프레임을 꽂아 둔다 (Play 전 확인용)
            if (spriteRenderer != null && spriteRenderer.sprite != frames[0])
            {
                Undo.RecordObject(spriteRenderer, "Set Background First Frame");
                spriteRenderer.sprite = frames[0];
                EditorUtility.SetDirty(spriteRenderer);
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Selection.activeGameObject = target;

            SerializedProperty fps = serialized.FindProperty("clip.fps");
            float loopSeconds = fps != null && fps.floatValue > 0f
                ? frames.Count / fps.floatValue
                : 0f;
            Debug.Log(
                $"[AnimatedBackground] 프레임 {frames.Count}장 연결 완료 " +
                $"({frames[0].name} ~ {frames[frames.Count - 1].name}). " +
                $"한 바퀴 {loopSeconds:0.00}초. 속도는 clip 의 fps 로 조절하세요. " +
                "(씬을 Ctrl+S 로 저장할 것)",
                view);
        }

        /// <summary>선택한 오브젝트를 우선하고, 없으면 이름으로 씬에서 찾는다.</summary>
        static GameObject ResolveTarget()
        {
            GameObject selected = Selection.activeGameObject;
            if (selected != null &&
                (selected.name == ObjectName ||
                 selected.GetComponent<AnimatedBackground>() != null))
                return selected;

            GameObject found = GameObject.Find(ObjectName);
            if (found != null) return found;

            AnimatedBackground existing = Object.FindFirstObjectByType<AnimatedBackground>(
                FindObjectsInactive.Include);
            return existing != null ? existing.gameObject : null;
        }

        /// <summary>
        /// 이미 꽂혀 있는 스프라이트가 있으면 그 폴더를 쓴다.
        /// 아트가 폴더를 옮겨도 이 메뉴가 따라간다.
        /// </summary>
        static string ResolveFrameFolder(AnimatedBackground view, SpriteRenderer spriteRenderer)
        {
            Sprite hint = null;
            if (view != null && view.Clip != null && view.Clip.FrameCount > 0)
                hint = view.Clip.GetFrameAt(0);
            if (hint == null && spriteRenderer != null) hint = spriteRenderer.sprite;

            if (hint != null)
            {
                string path = AssetDatabase.GetAssetPath(hint);
                if (!string.IsNullOrEmpty(path))
                {
                    string folder = Path.GetDirectoryName(path)?.Replace('\\', '/');
                    if (!string.IsNullOrEmpty(folder)) return folder;
                }
            }

            return DefaultFrameFolder;
        }

        /// <summary>
        /// 폴더의 스프라이트를 파일명 끝 숫자 순으로 정렬해 돌려준다.
        /// (BG_2 와 BG_10 처럼 자리수가 다른 이름이 섞여도 순서가 맞는다)
        /// </summary>
        static List<Sprite> LoadFrames(string folder, string prefix)
        {
            var frames = new List<Sprite>(32);
            if (!AssetDatabase.IsValidFolder(folder)) return frames;

            string[] guids = AssetDatabase.FindAssets("t:Sprite", new[] { folder });
            var ordered = new List<(int order, string name, Sprite sprite)>(guids.Length);

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                string fileName = Path.GetFileNameWithoutExtension(path);
                if (!string.IsNullOrEmpty(prefix) &&
                    !fileName.StartsWith(prefix, System.StringComparison.Ordinal))
                    continue;

                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite == null) continue;

                Match match = TrailingNumber.Match(fileName);
                int order = match.Success && int.TryParse(match.Groups[1].Value, out int parsed)
                    ? parsed
                    : int.MaxValue;
                ordered.Add((order, fileName, sprite));
            }

            ordered.Sort((a, b) =>
            {
                int byOrder = a.order.CompareTo(b.order);
                return byOrder != 0
                    ? byOrder
                    : string.CompareOrdinal(a.name, b.name); // 숫자가 없는 파일은 이름순
            });

            for (int i = 0; i < ordered.Count; i++) frames.Add(ordered[i].sprite);
            return frames;
        }
    }
}
