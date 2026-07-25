using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using static ContextStage.EditorTools.EditorSetupUtility;

namespace ContextStage.EditorTools
{
    /// <summary>
    /// 장식용 배경 군중 + 결과 화면 너구리 셋업.
    ///
    /// <list type="bullet">
    /// <item><c>Tools/Art/Setup Decorative Crowd</c> — 배경 뒤쪽 장식 군중을 씬에 만든다</item>
    /// <item><c>Tools/UI/Setup Rank Raccoon</c> — 결과 팝업의 랭크 알파벳 오른쪽에 너구리를 넣는다</item>
    /// </list>
    ///
    /// 이미 있는 것은 덮어쓰지 않는다. 여러 번 실행해도 안전하다.
    /// </summary>
    public static class DecorativeCrowdSetup
    {
        const string CrowdRootName = "[DecorativeCrowd]";
        const string PeopleFolder = "Assets/Sprites/UI/Anonymous";
        const string RaccoonClearPath = "Assets/Sprites/UI/raccoon_clear (2).png";
        const string RaccoonGameOverPath = "Assets/Sprites/UI/raccoon_gameover.png";
        const string ScoreCanvasPrefabPath = "Assets/Prefabs/UI/ScoreCanvas.prefab";
        const string RaccoonObjectName = "RankRaccoon";

        // ---------------- 1) 장식용 배경 군중 ----------------

        [MenuItem("Tools/Art/Setup Decorative Crowd", false, 2)]
        public static void SetupDecorativeCrowd()
        {
            DecorativeCrowd existing = Object.FindFirstObjectByType<DecorativeCrowd>(
                FindObjectsInactive.Include);
            if (existing != null)
            {
                Debug.Log(
                    "[DecorativeCrowd] 이미 있습니다. 설정을 덮어쓰지 않았습니다.",
                    existing);
                Selection.activeGameObject = existing.gameObject;
                return;
            }

            var root = GameObject.Find(CrowdRootName);
            if (root == null)
            {
                root = new GameObject(CrowdRootName);
                Undo.RegisterCreatedObjectUndo(root, "Create Decorative Crowd");
            }

            var crowd = EnsureComponent<DecorativeCrowd>(root);

            List<Sprite> people = LoadPeopleSprites();
            if (people.Count == 0)
            {
                Debug.LogError(
                    $"[DecorativeCrowd] '{PeopleFolder}' 에서 people 스프라이트를 찾지 못했습니다.");
                return;
            }

            var serialized = new SerializedObject(crowd);
            SerializedProperty variants = serialized.FindProperty("variants");
            variants.arraySize = people.Count;
            for (int i = 0; i < people.Count; i++)
                variants.GetArrayElementAtIndex(i).objectReferenceValue = people[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(crowd);

            PlaceBehindStage(root.transform);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Selection.activeGameObject = root;
            Debug.Log(
                $"[DecorativeCrowd] 스프라이트 {people.Count}종 연결 완료. " +
                "위치·폭은 spreadWidth 와 오브젝트 Transform 으로, " +
                "앞뒤 관계는 sortingOrder 로 맞추세요. (씬을 Ctrl+S 로 저장할 것)",
                crowd);
        }

        /// <summary>
        /// 이미 씬에 있는 장식 군중의 배치 값을 권장값으로 되돌린다.
        ///
        /// 셋업 메뉴는 이미 있는 컴포넌트를 건드리지 않으므로, 코드의 기본값을 바꿔도
        /// 씬에 저장된 예전 값이 그대로 남는다. (조명에서 겪은 것과 같은 상황)
        /// 스프라이트 연결과 Transform 위치는 건드리지 않는다.
        /// </summary>
        [MenuItem("Tools/Art/Fix Decorative Crowd Layout", false, 3)]
        public static void FixDecorativeCrowdLayout()
        {
            DecorativeCrowd crowd = Object.FindFirstObjectByType<DecorativeCrowd>(
                FindObjectsInactive.Include);
            if (crowd == null)
            {
                Debug.LogWarning(
                    "[DecorativeCrowd] 씬에 없습니다. " +
                    "먼저 Tools/Art/Setup Decorative Crowd 를 실행하세요.");
                return;
            }

            Undo.RecordObject(crowd, "Fix Decorative Crowd Layout");
            var serialized = new SerializedObject(crowd);

            SetFloat(serialized, "baseScale", 2.2f);      // 기존 대비 약 2.2배
            SetInt(serialized, "rows", 3);
            SetFloat(serialized, "rowSpacing", 0.55f);
            SetFloat(serialized, "rowScaleFalloff", 0.92f);
            SetFloat(serialized, "rowStagger", 0.5f);
            SetFloat(serialized, "verticalJitter", 0.18f);
            SetFloat(serialized, "spreadWidth", 16f);
            SetInt(serialized, "sortingOrder", 2);
            SetInt(serialized, "sortingOrderSpan", 3);

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(crowd);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Selection.activeGameObject = crowd.gameObject;

            Debug.Log(
                "[DecorativeCrowd] 배치를 권장값으로 맞췄습니다 — " +
                "크기 2.2배 / 3줄 겹침 / 정렬 2. " +
                "전체 위치는 오브젝트 Transform 으로 옮기세요. (씬을 Ctrl+S 로 저장할 것)",
                crowd);
        }

        static void SetFloat(SerializedObject serialized, string field, float value)
        {
            SerializedProperty property = serialized.FindProperty(field);
            if (property != null) property.floatValue = value;
            else Debug.LogWarning($"[DecorativeCrowd] '{field}' 필드를 찾지 못했습니다.");
        }

        static void SetInt(SerializedObject serialized, string field, int value)
        {
            SerializedProperty property = serialized.FindProperty(field);
            if (property != null) property.intValue = value;
            else Debug.LogWarning($"[DecorativeCrowd] '{field}' 필드를 찾지 못했습니다.");
        }

        static List<Sprite> LoadPeopleSprites()
        {
            var sprites = new List<Sprite>();
            if (!AssetDatabase.IsValidFolder(PeopleFolder)) return sprites;

            string[] guids = AssetDatabase.FindAssets("t:Sprite", new[] { PeopleFolder });
            var paths = new List<string>(guids.Length);
            for (int i = 0; i < guids.Length; i++)
                paths.Add(AssetDatabase.GUIDToAssetPath(guids[i]));
            paths.Sort(string.CompareOrdinal); // people1 → people2 순서 고정

            for (int i = 0; i < paths.Count; i++)
            {
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(paths[i]);
                if (sprite != null) sprites.Add(sprite);
            }
            return sprites;
        }

        /// <summary>배경 앞·무대 뒤 정도의 자리에 놓는다. 정확한 위치는 Scene 뷰에서 맞춘다.</summary>
        static void PlaceBehindStage(Transform target)
        {
            Camera camera = Camera.main;
            float y = camera != null ? camera.transform.position.y + 1.2f : 1.2f;
            target.position = new Vector3(0f, y, 0f);
        }

        // ---------------- 2) 결과 화면 랭크 너구리 ----------------

        [MenuItem("Tools/UI/Setup Rank Raccoon", false, 32)]
        public static void SetupRankRaccoon()
        {
            List<Sprite> clearFrames = LoadFrames(RaccoonClearPath);
            List<Sprite> gameOverFrames = LoadFrames(RaccoonGameOverPath);
            if (clearFrames.Count == 0 || gameOverFrames.Count == 0)
            {
                Debug.LogError(
                    "[RankRaccoon] 너구리 스프라이트를 찾지 못했습니다.\n" +
                    $"  {RaccoonClearPath} → {clearFrames.Count}프레임\n" +
                    $"  {RaccoonGameOverPath} → {gameOverFrames.Count}프레임");
                return;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(ScoreCanvasPrefabPath);
            if (root == null)
            {
                Debug.LogError($"[RankRaccoon] 프리팹을 찾지 못했습니다: {ScoreCanvasPrefabPath}");
                return;
            }

            try
            {
                var popup = root.GetComponentInChildren<GameOverPopup>(true);
                if (popup == null)
                {
                    Debug.LogError("[RankRaccoon] 프리팹에서 GameOverPopup 을 찾지 못했습니다.");
                    return;
                }

                if (popup.GetComponentInChildren<RankRaccoonAnimator>(true) != null)
                {
                    Debug.Log("[RankRaccoon] 이미 있습니다. 설정을 덮어쓰지 않았습니다.");
                    return;
                }

                // 랭크 알파벳 Image 를 찾아 그 오른쪽에 붙인다
                var popupSerialized = new SerializedObject(popup);
                var rankImage =
                    popupSerialized.FindProperty("rankImage")?.objectReferenceValue as Image;
                if (rankImage == null)
                {
                    Debug.LogError(
                        "[RankRaccoon] GameOverPopup 의 rankImage 가 비어 있어 " +
                        "붙일 자리를 찾지 못했습니다. 먼저 랭크 이미지를 연결하세요.");
                    return;
                }

                Image raccoon = CreateRaccoonImage(rankImage);
                var animator = raccoon.gameObject.AddComponent<RankRaccoonAnimator>();

                var serialized = new SerializedObject(animator);
                SetObjectReference(serialized, "targetImage", raccoon);
                FillClip(serialized, "clearClip", clearFrames, 4f);
                FillClip(serialized, "gameOverClip", gameOverFrames, 3f);
                serialized.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, ScoreCanvasPrefabPath);
                Debug.Log(
                    $"[RankRaccoon] 랭크 오른쪽에 너구리를 넣었습니다 " +
                    $"(클리어 {clearFrames.Count}프레임 / 게임오버 {gameOverFrames.Count}프레임). " +
                    "속도는 각 클립의 fps 로 조절합니다.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>랭크 이미지와 같은 부모·같은 세로 위치에, 폭만큼 오른쪽으로 밀어 만든다.</summary>
        static Image CreateRaccoonImage(Image rankImage)
        {
            RectTransform rankRect = rankImage.rectTransform;

            var go = new GameObject(RaccoonObjectName, typeof(RectTransform), typeof(Image));
            go.layer = rankImage.gameObject.layer;
            go.transform.SetParent(rankRect.parent, false);

            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rankRect.anchorMin;
            rect.anchorMax = rankRect.anchorMax;
            rect.pivot = rankRect.pivot;
            rect.sizeDelta = rankRect.sizeDelta;
            rect.localScale = Vector3.one;

            // 랭크 알파벳 오른쪽에 한 칸 띄워 세운다
            float gap = rankRect.sizeDelta.x * 0.25f;
            rect.anchoredPosition = rankRect.anchoredPosition +
                                    new Vector2(rankRect.sizeDelta.x + gap, 0f);

            Image image = go.GetComponent<Image>();
            image.raycastTarget = false;
            // 두 프레임의 트리밍 크기가 달라서 비율을 유지해야 크기가 튀지 않는다
            image.preserveAspect = true;
            return image;
        }

        static void FillClip(
            SerializedObject serialized,
            string clipField,
            List<Sprite> frames,
            float fps)
        {
            SerializedProperty clip = serialized.FindProperty(clipField);
            if (clip == null)
            {
                Debug.LogWarning($"[RankRaccoon] '{clipField}' 필드를 찾지 못했습니다.");
                return;
            }

            clip.FindPropertyRelative("fps").floatValue = fps;
            clip.FindPropertyRelative("loop").boolValue = true;

            SerializedProperty array = clip.FindPropertyRelative("frames");
            array.arraySize = frames.Count;
            for (int i = 0; i < frames.Count; i++)
                array.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
        }

        /// <summary>
        /// 스프라이트 시트(Multiple)로 잘린 프레임을 이름 순으로 꺼낸다.
        /// 두 너구리 png 는 <c>_0</c>, <c>_1</c> 두 조각으로 들어와 있어 메인 에셋이 Texture2D 다.
        /// </summary>
        static List<Sprite> LoadFrames(string assetPath)
        {
            var frames = new List<Sprite>();

            Object[] all = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            for (int i = 0; i < all.Length; i++)
                if (all[i] is Sprite sprite) frames.Add(sprite);

            if (frames.Count == 0)
            {
                var single = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
                if (single != null) frames.Add(single);
            }

            frames.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            return frames;
        }
    }
}
