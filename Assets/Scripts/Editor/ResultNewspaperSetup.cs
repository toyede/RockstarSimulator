using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static ContextStage.EditorTools.EditorSetupUtility;

namespace ContextStage.EditorTools
{
    /// <summary>
    /// 신문 결과창 셋업.
    ///
    ///   Tools/Tour/Setup Result Newspaper
    ///     1. Docs/ArtDrafts/ResultNewspaper_20260910_v1 의 지면 초안 5장을 Assets/Sprites/UI/ResultNewspaper 로 복사 (없을 때만) + 임포트 교정
    ///     2. Resources/Tour/ResultNewspaperCatalog.asset — 스테이지별 스킨, 랭크 아이콘, 너구리 포즈 (비어 있는 것만 채움)
    ///     3. Main 씬 [TourPerformance]/[ResultNewspaper] 캔버스 계층 생성 + ResultNewspaperView 필드 바인딩 + 브리지 연결
    ///   좌표는 1672×941 지면 기준. 실행 후 씬에서 위치를 고쳐도 다시 실행하면 새로 만드니 주의 (기존 계층이 있으면 건너뛴다).
    /// </summary>
    public static class ResultNewspaperSetup
    {
        const string DraftFolder = "Docs/ArtDrafts/ResultNewspaper_20260910_v1";
        const string SpriteFolder = "Assets/Sprites/UI/ResultNewspaper";
        const string CatalogPath = "Assets/Resources/Tour/ResultNewspaperCatalog.asset";
        const string MainScenePath = "Assets/Scenes/Main.unity";
        const string PerformanceRootName = "[TourPerformance]";
        const string ViewRootName = "[ResultNewspaper]";

        static readonly Color Ink = new Color32(0x2E, 0x22, 0x2F, 0xFF);
        static readonly Color Secondary = new Color32(0x62, 0x55, 0x65, 0xFF);
        static readonly Color Paper = new Color32(0xC7, 0xDC, 0xD0, 0xFF);

        // 파일명, stageId, 매체 이름, 강조색
        static readonly (string file, string stageId, string name, Color accent)[] Drafts =
        {
            ("01_alley_daily_draft.png", "stage_01", "골목일보", new Color32(0x67, 0x66, 0x33, 0xFF)),
            ("02_animal_joongang_draft.png", "stage_02", "동물중앙", new Color32(0xB3, 0x38, 0x31, 0xFF)),
            ("03_rolling_hairballs_draft.png", "stage_03", "Rolling Hairballs", new Color32(0xC8, 0x20, 0x2E, 0xFF)),
            ("04_pitchfur_draft.png", "stage_04", "Pitchfur", new Color32(0x8E, 0x44, 0xAD, 0xFF)),
            ("05_animal_times_draft.png", "stage_05_boss", "The Animal Times", new Color32(0xD6, 0x9A, 0x1E, 0xFF)),
        };

        /// <summary>기존 [ResultNewspaper] 계층을 지우고 새로 만든다 (씬에서 고친 위치는 사라진다).</summary>
        [MenuItem("Tools/Tour/Rebuild Result Newspaper", false, 14)]
        public static void Rebuild()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != MainScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
            }
            ResultNewspaperView existing = Object.FindFirstObjectByType<ResultNewspaperView>(FindObjectsInactive.Include);
            if (existing != null) Undo.DestroyObjectImmediate(existing.gameObject);
            Setup();
        }

        [MenuItem("Tools/Tour/Setup Result Newspaper", false, 13)]
        public static void Setup()
        {
            List<Sprite> papers = ImportDrafts();
            ResultNewspaperCatalog catalog = EnsureCatalog(papers);
            AssetDatabase.SaveAssets();
            ConfigureMainScene();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[ResultNewspaper] 셋업 완료. Main 씬을 저장했다.", catalog);
        }

        // ---------------- 1. 아트 ----------------

        static List<Sprite> ImportDrafts()
        {
            EnsureFolder(SpriteFolder);
            var sprites = new List<Sprite>();
            foreach (var draft in Drafts)
            {
                string source = Path.Combine(DraftFolder, draft.file);
                string target = $"{SpriteFolder}/newspaper_{draft.file.Replace("_draft", "")}";
                if (!File.Exists(target))
                {
                    if (!File.Exists(source))
                    {
                        Debug.LogWarning($"[ResultNewspaper] 초안이 없습니다: {source}");
                        sprites.Add(null);
                        continue;
                    }
                    File.Copy(source, target);
                    AssetDatabase.ImportAsset(target);
                }

                var importer = AssetImporter.GetAtPath(target) as TextureImporter;
                if (importer != null)
                {
                    bool changed = false;
                    if (importer.textureType != TextureImporterType.Sprite) { importer.textureType = TextureImporterType.Sprite; changed = true; }
                    if (importer.spriteImportMode != SpriteImportMode.Single) { importer.spriteImportMode = SpriteImportMode.Single; changed = true; }
                    if (importer.filterMode != FilterMode.Point) { importer.filterMode = FilterMode.Point; changed = true; }
                    if (importer.mipmapEnabled) { importer.mipmapEnabled = false; changed = true; }
                    if (importer.textureCompression != TextureImporterCompression.Uncompressed) { importer.textureCompression = TextureImporterCompression.Uncompressed; changed = true; }
                    if (importer.maxTextureSize < 2048) { importer.maxTextureSize = 2048; changed = true; }
                    if (!Mathf.Approximately(importer.spritePixelsPerUnit, 100f)) { importer.spritePixelsPerUnit = 100f; changed = true; }
                    if (changed) importer.SaveAndReimport();
                }
                sprites.Add(AssetDatabase.LoadAssetAtPath<Sprite>(target));
            }
            return sprites;
        }

        // ---------------- 2. 카탈로그 ----------------

        static ResultNewspaperCatalog EnsureCatalog(List<Sprite> papers)
        {
            EnsureFolder("Assets/Resources/Tour");
            var catalog = AssetDatabase.LoadAssetAtPath<ResultNewspaperCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<ResultNewspaperCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            for (int i = 0; i < Drafts.Length; i++)
                catalog.EditorSetSkin(Drafts[i].stageId, i < papers.Count ? papers[i] : null, Drafts[i].name, Drafts[i].accent);

            foreach (string label in new[] { "S", "A", "B", "C", "D", "F" })
                catalog.EditorSetRankIcon(label, AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Sprites/UI/Rank/{label}.png"));

            catalog.EditorSetBandFrames(
                SubSprites("Assets/Sprites/UI/raccoon_clear (2).png"),
                SubSprites("Assets/Sprites/UI/raccoon_gameover.png"));

            EditorUtility.SetDirty(catalog);
            return catalog;
        }

        /// <summary>스프라이트 시트의 프레임을 이름순으로. Single 이면 그 하나.</summary>
        static List<Sprite> SubSprites(string path)
        {
            var frames = new List<Sprite>();
            foreach (Object asset in AssetDatabase.LoadAllAssetRepresentationsAtPath(path))
                if (asset is Sprite sprite) frames.Add(sprite);
            frames.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            if (frames.Count == 0)
            {
                Sprite single = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (single != null) frames.Add(single);
            }
            return frames;
        }

        // ---------------- 3. 씬 ----------------

        static void ConfigureMainScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != MainScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                scene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
            }

            TourPerformanceBridge bridge = Object.FindFirstObjectByType<TourPerformanceBridge>(FindObjectsInactive.Include);
            GameObject performanceRoot;
            if (bridge != null) performanceRoot = bridge.gameObject;
            else
            {
                performanceRoot = new GameObject(PerformanceRootName);
                bridge = performanceRoot.AddComponent<TourPerformanceBridge>();
            }
            EnsureComponent<PerformanceStatsRecorder>(performanceRoot);

            Transform existing = performanceRoot.transform.Find(ViewRootName);
            ResultNewspaperView view;
            if (existing != null)
            {
                view = existing.GetComponent<ResultNewspaperView>();
                Debug.Log("[ResultNewspaper] 기존 [ResultNewspaper] 계층이 있어 그대로 둔다 (다시 만들려면 지우고 실행).");
            }
            else
            {
                view = BuildView(performanceRoot.transform);
            }

            if (view != null)
            {
                SetObjectField(bridge, "newspaper", view);
                EditorUtility.SetDirty(bridge);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = view != null ? view.gameObject : performanceRoot;
        }

        static ResultNewspaperView BuildView(Transform parent)
        {
            TMP_FontAsset font = ProjectFontTool.TmpFont;

            // 캔버스 루트
            var rootObject = new GameObject(ViewRootName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Undo.RegisterCreatedObjectUndo(rootObject, "Create Result Newspaper");
            rootObject.transform.SetParent(parent, false);
            var canvas = rootObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 5000;
            var scaler = rootObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            // 뷰 컴포넌트는 캔버스 루트(항상 활성)에, 켜고 끄는 것은 자식 Content — 꺼진 오브젝트에서는 코루틴을 못 돌린다
            var view = rootObject.AddComponent<ResultNewspaperView>();
            var contentObject = new GameObject("Content", typeof(RectTransform));
            var contentRect = contentObject.GetComponent<RectTransform>();
            contentRect.SetParent(rootObject.transform, false);
            contentRect.anchorMin = Vector2.zero;
            contentRect.anchorMax = Vector2.one;
            contentRect.offsetMin = Vector2.zero;
            contentRect.offsetMax = Vector2.zero;
            Transform content = contentObject.transform;

            // 딤 배경 + 스킵 캐처
            Image backdropImage = Fullscreen(content, "Backdrop", new Color(0.05f, 0.03f, 0.06f, 1f));
            var backdropGroup = backdropImage.gameObject.AddComponent<CanvasGroup>();
            backdropGroup.alpha = 0f;
            backdropGroup.blocksRaycasts = true;
            backdropImage.raycastTarget = true;

            Image skipImage = Fullscreen(content, "SkipCatcher", new Color(0f, 0f, 0f, 0f));
            skipImage.raycastTarget = true;
            var skipButton = skipImage.gameObject.AddComponent<Button>();
            skipButton.transition = Selectable.Transition.None;

            // 신문 지면 (1672×941, 중앙)
            RectTransform paper = Rect(content, "Paper", Vector2.zero, new Vector2(1672f, 941f));
            var paperImage = paper.gameObject.AddComponent<Image>();
            paperImage.raycastTarget = false;
            paperImage.preserveAspect = true;

            // 제호 정보 (헤드라인 띠 오른쪽 끝)
            // 픽셀 폰트라 20px 아래로 내리면 깨져 보인다. 긴 공연장 이름은 줄바꿈으로 받는다
            TMP_Text mastheadInfo = Text(paper, "MastheadInfo", font, 22f, TextAlignmentOptions.MidlineRight, Secondary, new Vector2(536f, 168f), new Vector2(260f, 96f));

            // 헤드라인 그룹 (헤드라인 띠 = 지면 y 250~350 → 로컬 120~220, 아래 가로줄이 120)
            RectTransform headlineRoot = Group(paper, "Headline", out CanvasGroup headlineGroup);
            TMP_Text headline = Text(headlineRoot, "Title", font, 46f, TextAlignmentOptions.MidlineLeft, Ink, new Vector2(-98f, 193f), new Vector2(1125f, 56f));
            headline.textWrappingMode = TextWrappingModes.Normal;
            headline.overflowMode = TextOverflowModes.Truncate;
            headline.fontStyle = FontStyles.Bold;
            TMP_Text subtitle = Text(headlineRoot, "Subtitle", font, 26f, TextAlignmentOptions.MidlineLeft, Secondary, new Vector2(-98f, 148f), new Vector2(1125f, 34f));
            subtitle.overflowMode = TextOverflowModes.Ellipsis;

            // 사진 그룹 (왼쪽 프레임, 지면 y 385~650 → 로컬 85~−180 안쪽)
            RectTransform photoRoot = Group(paper, "Photo", out CanvasGroup photoGroup);
            RectTransform frame = Rect(photoRoot, "Frame", new Vector2(-266f, -38f), new Vector2(770f, 236f));
            var frameImage = frame.gameObject.AddComponent<Image>();
            frameImage.color = new Color(0.72f, 0.76f, 0.73f, 1f);
            frameImage.raycastTarget = false;
            // RectMask2D 는 회전을 무시하고 축 정렬로 자르므로 기울어진 지면에서는 Mask(스텐실)를 쓴다
            var mask = frame.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = true;
            RectTransform bgRect = Rect(frame, "Background", new Vector2(0f, 20f), new Vector2(770f, 433f));
            var photoBackground = bgRect.gameObject.AddComponent<Image>();
            photoBackground.raycastTarget = false;
            photoBackground.preserveAspect = true;
            RectTransform bandRect = Rect(frame, "Band", new Vector2(-190f, 6f), new Vector2(330f, 220f));
            var bandImage = bandRect.gameObject.AddComponent<Image>();
            bandImage.raycastTarget = false;
            bandImage.preserveAspect = true;
            // 사진 설명은 사진 안쪽 하단 띠에 (스킨마다 테두리·통계 칸 위치가 달라 바깥에 두면 줄과 겹친다)
            RectTransform captionStrip = Rect(frame, "CaptionStrip", new Vector2(0f, -102f), new Vector2(770f, 32f));
            var stripImage = captionStrip.gameObject.AddComponent<Image>();
            stripImage.color = new Color(0.1f, 0.07f, 0.12f, 0.62f);
            stripImage.raycastTarget = false;
            TMP_Text caption = Text(captionStrip, "Caption", font, 22f, TextAlignmentOptions.MidlineLeft, new Color32(0xEE, 0xE6, 0xF0, 0xFF), new Vector2(8f, 0f), new Vector2(740f, 32f));
            caption.overflowMode = TextOverflowModes.Ellipsis;

            // 성적 그룹 (오른쪽 칸 490×235, 중심 x 419)
            RectTransform verdictRoot = Group(paper, "Verdict", out CanvasGroup verdictGroup);
            RectTransform stamp = Rect(verdictRoot, "Stamp", new Vector2(419f, 50f), new Vector2(320f, 74f));
            TMP_Text stampText = Text(stamp, "Text", font, 38f, TextAlignmentOptions.Center, Ink, Vector2.zero, new Vector2(320f, 74f));
            stampText.fontStyle = FontStyles.Bold;
            Image[] stampFrame = Border(stamp, 5f, Ink);
            RectTransform rankRect = Rect(verdictRoot, "Rank", new Vector2(250f, -75f), new Vector2(120f, 120f));
            var rankImage = rankRect.gameObject.AddComponent<Image>();
            rankImage.raycastTarget = false;
            rankImage.preserveAspect = true;
            TMP_Text scoreText = Text(verdictRoot, "Score", font, 46f, TextAlignmentOptions.MidlineLeft, Ink, new Vector2(500f, -50f), new Vector2(340f, 60f));
            scoreText.fontStyle = FontStyles.Bold;
            TMP_Text targetText = Text(verdictRoot, "Target", font, 26f, TextAlignmentOptions.MidlineLeft, Secondary, new Vector2(500f, -95f), new Vector2(340f, 34f));
            TMP_Text ratioText = Text(verdictRoot, "Ratio", font, 26f, TextAlignmentOptions.MidlineLeft, Secondary, new Vector2(500f, -128f), new Vector2(340f, 34f));

            // 기록 · 기사 · 버튼 그룹
            RectTransform recordsRoot = Group(paper, "Records", out CanvasGroup recordsGroup);
            // 통계 칸 (지면 y 685~765). 칸 왼쪽 60px 는 스킨의 발바닥 아이콘 자리라 비운다
            // 스킨 장식이 칸 왼쪽(발바닥)이나 가운데(❖)에 있어 오른쪽 절반에 두 줄(라벨/값)로 쓴다
            TMP_Text statCombo = Text(recordsRoot, "StatCombo", font, 24f, TextAlignmentOptions.Center, Ink, new Vector2(-353f, -255f), new Vector2(210f, 76f));
            TMP_Text statFever = Text(recordsRoot, "StatFever", font, 24f, TextAlignmentOptions.Center, Ink, new Vector2(100f, -255f), new Vector2(210f, 76f));
            TMP_Text statAudience = Text(recordsRoot, "StatAudience", font, 24f, TextAlignmentOptions.Center, Ink, new Vector2(556f, -255f), new Vector2(210f, 76f));
            statCombo.richText = statFever.richText = statAudience.richText = true;
            // 기사란 (지면 y 780~840 → 로컬 −310~−370, 테두리 있는 스킨은 안쪽 −318~−362)
            // 테두리 있는 스킨의 안쪽(−328~−362)에 제목 한 줄 + 본문 한 줄
            TMP_Text articleTitle = Text(recordsRoot, "ArticleTitle", font, 24f, TextAlignmentOptions.MidlineLeft, Ink, new Vector2(-250f, -336f), new Vector2(800f, 28f));
            articleTitle.fontStyle = FontStyles.Bold;
            TMP_Text articleBody = Text(recordsRoot, "ArticleBody", font, 22f, TextAlignmentOptions.MidlineLeft, Secondary, new Vector2(-250f, -361f), new Vector2(800f, 26f));
            articleBody.overflowMode = TextOverflowModes.Ellipsis;

            RectTransform buttonRect = Rect(recordsRoot, "PrimaryButton", new Vector2(470f, -340f), new Vector2(360f, 58f));
            var buttonImage = buttonRect.gameObject.AddComponent<Image>();
            buttonImage.color = Ink;
            var button = buttonRect.gameObject.AddComponent<Button>();
            var colors = button.colors;
            colors.highlightedColor = new Color(0.85f, 0.8f, 0.88f, 1f);
            colors.pressedColor = new Color(0.65f, 0.6f, 0.68f, 1f);
            colors.disabledColor = new Color(1f, 1f, 1f, 0.4f);
            button.colors = colors;
            TMP_Text primaryLabel = Text(buttonRect, "Label", font, 30f, TextAlignmentOptions.Center, Paper, Vector2.zero, new Vector2(360f, 58f));

            // 바인딩
            var so = new SerializedObject(view);
            SetObjectReference(so, "root", contentObject);
            SetObjectReference(so, "backdrop", backdropGroup);
            SetObjectReference(so, "skipCatcher", skipButton);
            SetObjectReference(so, "paper", paper);
            SetObjectReference(so, "paperImage", paperImage);
            SetObjectReference(so, "mastheadInfo", mastheadInfo);
            SetObjectReference(so, "headlineGroup", headlineGroup);
            SetObjectReference(so, "headline", headline);
            SetObjectReference(so, "subtitle", subtitle);
            SetObjectReference(so, "photoGroup", photoGroup);
            SetObjectReference(so, "photoBackground", photoBackground);
            SetObjectReference(so, "bandImage", bandImage);
            SetObjectReference(so, "caption", caption);
            SetObjectReference(so, "verdictGroup", verdictGroup);
            SetObjectReference(so, "stamp", stamp);
            SetObjectReference(so, "stampText", stampText);
            SerializedProperty frameArray = so.FindProperty("stampFrame");
            frameArray.arraySize = stampFrame.Length;
            for (int i = 0; i < stampFrame.Length; i++) frameArray.GetArrayElementAtIndex(i).objectReferenceValue = stampFrame[i];
            SetObjectReference(so, "rankImage", rankImage);
            SetObjectReference(so, "scoreText", scoreText);
            SetObjectReference(so, "targetText", targetText);
            SetObjectReference(so, "ratioText", ratioText);
            SetObjectReference(so, "recordsGroup", recordsGroup);
            SetObjectReference(so, "statCombo", statCombo);
            SetObjectReference(so, "statFever", statFever);
            SetObjectReference(so, "statAudience", statAudience);
            SetObjectReference(so, "articleTitle", articleTitle);
            SetObjectReference(so, "articleBody", articleBody);
            SetObjectReference(so, "primaryButton", button);
            SetObjectReference(so, "primaryLabel", primaryLabel);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(view);

            contentObject.SetActive(false);
            return view;
        }

        // ---------------- 생성 도우미 ----------------

        static RectTransform Rect(Transform parent, string name, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        static RectTransform Group(Transform parent, string name, out CanvasGroup group)
        {
            RectTransform rect = Rect(parent, name, Vector2.zero, Vector2.zero);
            group = rect.gameObject.AddComponent<CanvasGroup>();
            // blocksRaycasts 를 끄면 자식 버튼까지 클릭을 못 받는다 (페이드 전용 그룹이라도 켜 둔다)
            group.blocksRaycasts = true;
            group.interactable = true;
            return rect;
        }

        static Image Fullscreen(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var image = go.GetComponent<Image>();
            image.color = color;
            return image;
        }

        static TMP_Text Text(Transform parent, string name, TMP_FontAsset font, float size, TextAlignmentOptions alignment, Color color, Vector2 position, Vector2 area)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = area;
            var text = go.GetComponent<TextMeshProUGUI>();
            if (font != null) text.font = font;
            text.fontSize = size;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Overflow;
            return text;
        }

        /// <summary>도장 테두리: 사각형 네 변.</summary>
        static Image[] Border(RectTransform parent, float thickness, Color color)
        {
            Vector2 size = parent.sizeDelta;
            var images = new Image[4];
            images[0] = Edge(parent, "Top", new Vector2(0f, size.y * 0.5f - thickness * 0.5f), new Vector2(size.x, thickness), color);
            images[1] = Edge(parent, "Bottom", new Vector2(0f, -size.y * 0.5f + thickness * 0.5f), new Vector2(size.x, thickness), color);
            images[2] = Edge(parent, "Left", new Vector2(-size.x * 0.5f + thickness * 0.5f, 0f), new Vector2(thickness, size.y), color);
            images[3] = Edge(parent, "Right", new Vector2(size.x * 0.5f - thickness * 0.5f, 0f), new Vector2(thickness, size.y), color);
            return images;
        }

        static Image Edge(Transform parent, string name, Vector2 position, Vector2 size, Color color)
        {
            RectTransform rect = Rect(parent, name, position, size);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }
    }
}
