using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ContextStage.EditorTools
{
    /// <summary>
    /// 특별 관객을 Prefab 으로 만드는 에디터 툴.
    ///
    /// 씬/프리팹 YAML 을 직접 손대지 않고 PrefabUtility 로 안전하게 만든다.
    /// 이미 프리팹이 있으면 새로 만들지 않고 <b>빠진 컴포넌트만 채워 업그레이드</b>한다.
    ///
    ///   Tools/Audience/Create Special Audience Prefab   프리팹 생성·업그레이드
    ///   Tools/Audience/Setup Special Audience In Scene  씬에 인스턴스 1개 배치
    /// </summary>
    public static class SpecialAudiencePrefabTool
    {
        const string PrefabFolder = "Assets/Prefabs/Audience";
        const string PrefabPath = PrefabFolder + "/SpecialAudience.prefab";
        const string SpriteSheet = "Assets/Sprites/Crowd/SpecialCrowd/Special_Crowd.png";

        // 캐릭터가 대략 3.7 x 6 유닛(스프라이트 367x599 @100PPU)이라 그보다 넉넉하게 잡는다.
        // 히트 영역: 가로 130% / 세로 120% (지시서 권장 범위)
        static readonly Vector2 HitAreaSize = new Vector2(2.6f, 3.4f);
        static readonly Vector2 HitAreaOffset = new Vector2(0f, 0.4f);

        [MenuItem("Tools/Audience/Create Special Audience Prefab", false, 0)]
        public static void CreatePrefab()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (existing != null)
            {
                UpgradePrefab(existing);
                return;
            }

            EnsureFolder(PrefabFolder);

            // ---- 계층 구성 ----
            var root = new GameObject("SpecialAudience");

            var visualRoot = new GameObject("VisualRoot");
            visualRoot.transform.SetParent(root.transform, false);

            var character = new GameObject("CharacterSprite");
            character.transform.SetParent(visualRoot.transform, false);
            var sr = character.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 10;
            sr.sprite = LoadFirstSprite();

            var hitArea = new GameObject("HitArea");
            hitArea.transform.SetParent(root.transform, false);
            var box = hitArea.AddComponent<BoxCollider2D>();
            box.isTrigger = true;                                 // 물리 이동용이 아니라 드롭 영역 판정용
            box.size = HitAreaSize;
            box.offset = HitAreaOffset;

            // ---- 컴포넌트 ----
            // 군중 사이를 돌아다니는 기존 액터를 그대로 재사용한다 (이벤트 구독형이라 프리팹에서도 동작)
            var actor = character.AddComponent<SpecialAudienceCrowdActor>();
            // 위치는 루트가 옮겨져야 HitArea 가 함께 따라오고,
            // 점프 스쿼시는 시각 오브젝트에만 걸려야 Collider 가 흔들리지 않는다
            SetField(actor, "motionRoot", root.transform);
            SetField(actor, "visualRoot", visualRoot.transform);

            var dropTarget = root.AddComponent<SpecialAudienceDropTarget>();
            SetField(dropTarget, "hitCollider", box);
            SetField(dropTarget, "hoverScaleTarget", visualRoot.transform);
            // followTarget 은 비워둔다 — 액터가 루트를 직접 옮기므로 따라다닐 필요가 없다

            AssignActorSprites(actor);

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);

            AssetDatabase.SaveAssets();
            Selection.activeObject = prefab;
            Debug.Log($"[SpecialAudience] 프리팹 생성 완료: {PrefabPath}\n" +
                      $"HitArea: BoxCollider2D size={HitAreaSize} offset={HitAreaOffset} (인스펙터에서 조절 가능)");
        }

        /// <summary>이미 있는 프리팹에 빠진 컴포넌트만 채운다. 기존 설정은 덮어쓰지 않는다.</summary>
        static void UpgradePrefab(GameObject prefabAsset)
        {
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            bool changed = false;

            var box = root.GetComponentInChildren<BoxCollider2D>(true);
            if (box == null)
            {
                var hitArea = new GameObject("HitArea");
                hitArea.transform.SetParent(root.transform, false);
                box = hitArea.AddComponent<BoxCollider2D>();
                box.isTrigger = true;
                box.size = HitAreaSize;
                box.offset = HitAreaOffset;
                changed = true;
            }

            var dropTarget = root.GetComponent<SpecialAudienceDropTarget>();
            if (dropTarget == null)
            {
                dropTarget = root.AddComponent<SpecialAudienceDropTarget>();
                SetField(dropTarget, "hitCollider", box);

                var actor = root.GetComponentInChildren<SpecialAudienceCrowdActor>(true);
                if (actor != null) SetField(dropTarget, "followTarget", actor.transform);
                changed = true;
            }

            if (changed)
            {
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log($"[SpecialAudience] 기존 프리팹을 업그레이드했습니다: {PrefabPath}");
            }
            else
            {
                Debug.Log($"[SpecialAudience] 프리팹이 이미 최신입니다: {PrefabPath}");
            }

            PrefabUtility.UnloadPrefabContents(root);
            Selection.activeObject = prefabAsset;
        }

        [MenuItem("Tools/Audience/Setup Special Audience In Scene", false, 1)]
        public static void SetupInScene()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                Debug.LogWarning("[SpecialAudience] 프리팹이 없습니다. Create Special Audience Prefab 을 먼저 실행하세요.");
                return;
            }

            // 씬에 이미 있으면 새로 만들지 않는다 (중복 인스턴스 방지)
            var existing = Object.FindFirstObjectByType<SpecialAudienceDropTarget>();
            if (existing != null)
            {
                Debug.Log("[SpecialAudience] 씬에 이미 특별 관객 인스턴스가 있습니다. 그것을 사용합니다.");
                Selection.activeGameObject = existing.gameObject;
                return;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            Undo.RegisterCreatedObjectUndo(instance, "Instantiate SpecialAudience");

            // 군중 밑에 두면 액터가 관객 좌표계를 그대로 쓸 수 있다
            var crowd = Object.FindFirstObjectByType<CrowdSpawner>();
            if (crowd != null) instance.transform.SetParent(crowd.transform, false);

            Selection.activeGameObject = instance;
            Debug.Log("[SpecialAudience] 씬에 배치 완료. 씬을 Ctrl+S 로 저장하세요.");
        }

        // ---------------- 헬퍼 ----------------

        static Sprite LoadFirstSprite()
        {
            var sprites = AssetDatabase.LoadAllAssetRepresentationsAtPath(SpriteSheet).OfType<Sprite>().ToList();
            return sprites.Count > 0 ? sprites[0] : null;
        }

        /// <summary>Special_Crowd 시트의 3장을 Chill/Singalong/Mosh 클립에 넣는다.</summary>
        static void AssignActorSprites(SpecialAudienceCrowdActor actor)
        {
            var sprites = AssetDatabase.LoadAllAssetRepresentationsAtPath(SpriteSheet)
                .OfType<Sprite>()
                .OrderBy(s => s.name)
                .ToList();
            if (sprites.Count == 0) return;

            var so = new SerializedObject(actor);
            string[] fields = { "chillClip", "singalongClip", "moshClip" };
            for (int i = 0; i < fields.Length && i < sprites.Count; i++)
            {
                var frames = so.FindProperty(fields[i])?.FindPropertyRelative("frames");
                if (frames == null) continue;
                frames.arraySize = 1;
                frames.GetArrayElementAtIndex(0).objectReferenceValue = sprites[i];
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetField(Object target, string fieldName, Object value)
        {
            if (target == null) return;

            var so = new SerializedObject(target);
            var prop = so.FindProperty(fieldName);
            if (prop == null)
            {
                Debug.LogWarning($"[SpecialAudience] {target.GetType().Name} 에서 '{fieldName}' 를 찾지 못했습니다.");
                return;
            }
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void EnsureFolder(string path)
        {
            if (string.IsNullOrEmpty(path) || AssetDatabase.IsValidFolder(path)) return;

            var parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
