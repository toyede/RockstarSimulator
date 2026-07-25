using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using static ContextStage.EditorTools.EditorSetupUtility;

namespace ContextStage.EditorTools
{
    /// <summary>
    /// 주인공(Raccoon)·밴드(Friends) 스프라이트 애니메이션 셋업.
    ///
    ///   Tools/Art/Fix Character Sprite Import    임포트 설정 교정 (먼저 실행할 것)
    ///   Tools/Art/Setup Raccoon And Friends      씬 배치 + 프레임 자동 수집 + 연결
    ///
    /// 임포트 교정이 필요한 이유: 지금 Friends 는 spriteMode = Multiple 로 들어와
    /// 이미지 한 장이 9개 쓰레기 조각으로 잘려 있다. 애니메이션 프레임으로 쓰려면
    /// "이미지 1장 = 스프라이트 1개"(Single) 여야 한다.
    /// </summary>
    public static class CharacterAnimationSetup
    {
        /// <summary>폴더별 임포트 설정. 새 캐릭터가 늘어나면 이 표에만 추가한다.</summary>
        static readonly (string folder, float pixelsPerUnit)[] ImportFolders =
        {
            // 834x1112px → PPU 222 이면 화면(10유닛) 대비 5유닛 높이로 들어온다
            ("Assets/Sprites/Raccoon", 222f),
            // 1280x720px → PPU 72 이면 16:9 화면을 꽉 채운다
            ("Assets/Sprites/Friends", 72f),
        };

        const float DefaultFps = 12f;

        // ---------------- 1) 임포트 교정 ----------------

        [MenuItem("Tools/Art/Fix Character Sprite Import", false, 0)]
        public static void FixSpriteImport()
        {
            int fixedCount = 0;

            foreach (var (folder, ppu) in ImportFolders)
            {
                if (!AssetDatabase.IsValidFolder(folder))
                {
                    Debug.LogWarning($"[Character] 폴더를 찾지 못했습니다: {folder}");
                    continue;
                }

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

                    // 핵심: Multiple 로 잘린 조각들을 버리고 이미지 전체를 한 장으로 되돌린다
                    if (importer.spriteImportMode != SpriteImportMode.Single)
                    {
                        importer.spriteImportMode = SpriteImportMode.Single;
                        changed = true;
                    }

                    if (!Mathf.Approximately(importer.spritePixelsPerUnit, ppu))
                    {
                        importer.spritePixelsPerUnit = ppu;
                        changed = true;
                    }

                    var settings = new TextureImporterSettings();
                    importer.ReadTextureSettings(settings);
                    if (settings.spriteAlignment != (int)SpriteAlignment.Center)
                    {
                        settings.spriteAlignment = (int)SpriteAlignment.Center;
                        importer.SetTextureSettings(settings);
                        changed = true;
                    }

                    if (importer.mipmapEnabled)
                    {
                        importer.mipmapEnabled = false;
                        changed = true;
                    }

                    // 픽셀아트다 — Bilinear 로 두면 도트가 뭉개진다
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

                    if (!importer.alphaIsTransparency)
                    {
                        importer.alphaIsTransparency = true;
                        changed = true;
                    }

                    if (!changed) continue;

                    importer.SaveAndReimport();
                    fixedCount++;
                }
            }

            AssetDatabase.Refresh();
            Debug.Log($"[Character] 스프라이트 임포트 교정 완료 — {fixedCount}장 수정 " +
                      "(spriteMode=Single, pivot=Center, PPU 조정). " +
                      "이어서 Tools/Art/Setup Raccoon And Friends 를 실행하세요.");
        }

        // ---------------- 2) 씬 셋업 ----------------

        [MenuItem("Tools/Art/Setup Raccoon And Friends", false, 1)]
        public static void SetupScene()
        {
            var root = GameObject.Find("[Characters]");
            if (root == null)
            {
                root = new GameObject("[Characters]");
                Undo.RegisterCreatedObjectUndo(root, "Create Characters");
            }

            // ---- Friends: 무한 루프 (무대 위, 너구리 뒤) ----
            // 투명 배경 픽셀아트 밴드 3인. 관객(baseSortingOrder 5 + 워닝/팝업 ~38)보다 앞,
            // 너구리보다는 뒤에 둔다.
            var friends = SetupCharacter(
                root.transform,
                "Friends",
                sortingOrder: 45,
                localPosition: new Vector3(0f, 0f, 0f),
                clips: new[]
                {
                    ("Idle", "Assets/Sprites/Friends", true),
                },
                defaultClip: "Idle");

            // ---- Raccoon: 3상태 (뒷모습, 카메라에 가장 가까움) ----
            // PPU 222 → 3.76 x 5.01 유닛. y = -1.5 면 발이 -4.0, 왕관이 +1.0 에 온다.
            var raccoon = SetupCharacter(
                root.transform,
                "Raccoon",
                sortingOrder: 50,
                localPosition: new Vector3(0f, -1.5f, 0f),
                clips: new[]
                {
                    ("Idle",       "Assets/Sprites/Raccoon/Idle",       true),   // 루프
                    ("Stroke",     "Assets/Sprites/Raccoon/Stroke",     false),  // 1회 → Idle 복귀
                    ("GuitarSolo", "Assets/Sprites/Raccoon/GuitarSolo", false),  // 1회 → Idle 복귀
                },
                defaultClip: "Idle");

            if (raccoon != null && raccoon.GetComponent<ContextStage.RaccoonAnimator>() == null)
                Undo.AddComponent<ContextStage.RaccoonAnimator>(raccoon.gameObject);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Selection.activeGameObject = raccoon != null ? raccoon.gameObject : root;
            Debug.Log("[Character] 셋업 완료. Raccoon 인스펙터 ⋮ → Debug/Play Stroke · Play Guitar Solo 로 확인하세요.\n" +
                      "위치·크기는 Scene 뷰에서 직접 맞추고, 반드시 씬을 Ctrl+S 로 저장할 것!");
        }

        static ContextStage.SpriteSheetAnimator SetupCharacter(
            Transform parent,
            string name,
            int sortingOrder,
            Vector3 localPosition,
            (string clipName, string folder, bool loop)[] clips,
            string defaultClip)
        {
            var existing = parent.Find(name);
            GameObject go;
            if (existing != null)
            {
                go = existing.gameObject;
            }
            else
            {
                go = new GameObject(name);
                Undo.RegisterCreatedObjectUndo(go, "Create " + name);
                go.transform.SetParent(parent, false);
                go.transform.localPosition = localPosition;
            }

            var renderer = EnsureComponent<SpriteRenderer>(go);
            renderer.sortingOrder = sortingOrder;

            var animator = EnsureComponent<ContextStage.SpriteSheetAnimator>(go);

            // 프레임 수집 + 클립 작성
            var serialized = new SerializedObject(animator);
            var clipsProp = serialized.FindProperty("clips");
            if (clipsProp == null)
            {
                Debug.LogWarning($"[Character] {name}: SpriteSheetAnimator 의 clips 필드를 찾지 못했습니다.");
                return animator;
            }

            clipsProp.arraySize = clips.Length;
            for (int i = 0; i < clips.Length; i++)
            {
                var (clipName, folder, loop) = clips[i];
                var frames = LoadFrames(folder);
                if (frames.Count == 0)
                    Debug.LogWarning($"[Character] {name}/{clipName}: '{folder}' 에서 스프라이트를 찾지 못했습니다. " +
                                     "Fix Character Sprite Import 를 먼저 실행했는지 확인하세요.");

                var element = clipsProp.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("clipName").stringValue = clipName;
                element.FindPropertyRelative("fps").floatValue = DefaultFps;
                element.FindPropertyRelative("loop").boolValue = loop;
                element.FindPropertyRelative("pingPong").boolValue = false;

                var framesProp = element.FindPropertyRelative("frames");
                framesProp.arraySize = frames.Count;
                for (int f = 0; f < frames.Count; f++)
                    framesProp.GetArrayElementAtIndex(f).objectReferenceValue = frames[f];
            }

            serialized.FindProperty("defaultClip").stringValue = defaultClip;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            // 첫 프레임을 미리 꽂아 에디터에서도 보이게 한다
            var firstFrames = LoadFrames(clips[0].folder);
            if (firstFrames.Count > 0) renderer.sprite = firstFrames[0];

            int total = clips.Sum(c => LoadFrames(c.folder).Count);
            Debug.Log($"[Character] {name}: 클립 {clips.Length}개 / 프레임 {total}장 연결 (fps {DefaultFps})");
            return animator;
        }

        // ---------------- 헬퍼 ----------------

        /// <summary>폴더의 PNG 를 파일명 끝 숫자 순서로 정렬해 Sprite 로 읽는다.</summary>
        static List<Sprite> LoadFrames(string folder)
        {
            var result = new List<Sprite>();
            foreach (string path in FindPngPaths(folder).OrderBy(TrailingNumber).ThenBy(p => p))
            {
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite != null) result.Add(sprite);
            }
            return result;
        }

        /// <summary>해당 폴더(하위 폴더 제외)의 PNG 경로.</summary>
        static IEnumerable<string> FindPngPaths(string folder)
        {
            if (!AssetDatabase.IsValidFolder(folder)) return System.Array.Empty<string>();

            return AssetDatabase.FindAssets("t:Texture2D", new[] { folder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => p.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase))
                .Distinct();
        }

        static int TrailingNumber(string path)
        {
            string name = System.IO.Path.GetFileNameWithoutExtension(path);
            int i = name.Length;
            while (i > 0 && char.IsDigit(name[i - 1])) i--;
            return i < name.Length && int.TryParse(name.Substring(i), out int n) ? n : 0;
        }
    }
}
