using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using static ContextStage.EditorTools.EditorSetupUtility;

namespace ContextStage.EditorTools
{
    /// <summary>
    /// 대화 데이터·프리팹 셋업. (다른 셋업 메뉴들과 같은 방식 — 이미 있는 것은 덮어쓰지 않는다)
    ///
    ///   Tools/Dialogue/Setup Dialogue Data
    ///     Settings/Dialogue/DialogueStyle.asset (DungGeunMo 폰트 연결)
    ///     Settings/Dialogue/Sequences/intro_stage_01 ~ 05_boss, ending_common (기획서 §12·§13 초안)
    ///     Resources/Dialogue/DialogueCatalog.asset
    ///
    ///   Tools/Dialogue/Rewrite Default Sequences (Overwrite)
    ///     시퀀스 대사를 기획서 초안으로 되돌린다 (손으로 고친 대사가 지워진다)
    ///
    ///   Tools/Dialogue/Create Dialogue Panel Prefab
    ///     Resources/Dialogue/DialoguePanel.prefab — 피그마 시안에 맞춰 손으로 고칠 수 있는 프리팹.
    ///     없으면 런타임이 같은 계층을 코드로 만든다.
    /// </summary>
    public static class DialogueSetupMenu
    {
        const string SettingsFolder = "Assets/Settings/Dialogue";
        const string SequenceFolder = SettingsFolder + "/Sequences";
        const string StylePath = SettingsFolder + "/DialogueStyle.asset";
        const string ResourceFolder = "Assets/Resources/Dialogue";
        const string CatalogPath = ResourceFolder + "/DialogueCatalog.asset";
        const string PrefabPath = ResourceFolder + "/DialoguePanel.prefab";
        const string BlurShaderPath = "Assets/Shaders/UIKawaseBlur.shader";

        // 화자 이름은 팀에서 확정하기 전까지 역할 가칭을 쓴다 (기획서 §11)
        const string Raccoon = "너구리";
        const string Hedgehog = "고슴도치";
        const string Bandmate = "밴드 동료";
        const string Promoter = "공연 기획자";
        const string Staff = "공연장 직원";
        const string Rival = "LUX//FAUNA";
        const string Crowd = "관객";

        [MenuItem("Tools/Dialogue/Setup Dialogue Data", false, 0)]
        public static void SetupData()
        {
            EnsureFolder(SettingsFolder);
            EnsureFolder(SequenceFolder);
            EnsureFolder(ResourceFolder);

            DialogueStyle style = GetOrCreateStyle();
            List<DialogueSequence> sequences = CreateDefaultSequences(overwrite: false);
            DialogueCatalog catalog = GetOrCreateCatalog(style, sequences);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (!catalog.TryValidate(out string error))
                Debug.LogError($"[Dialogue] 카탈로그 검증 실패: {error}", catalog);
            else
                Debug.Log($"[Dialogue] 셋업 완료. 시퀀스 {sequences.Count}개, 카탈로그 {CatalogPath}", catalog);
        }

        [MenuItem("Tools/Dialogue/Rewrite Default Sequences (Overwrite)", false, 1)]
        public static void RewriteSequences()
        {
            EnsureFolder(SettingsFolder);
            EnsureFolder(SequenceFolder);
            EnsureFolder(ResourceFolder);

            DialogueStyle style = GetOrCreateStyle();
            List<DialogueSequence> sequences = CreateDefaultSequences(overwrite: true);
            GetOrCreateCatalog(style, sequences);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[Dialogue] 기본 대사 {sequences.Count}개를 기획서 초안으로 다시 썼습니다.");
        }

        [MenuItem("Tools/Dialogue/Create Dialogue Panel Prefab", false, 20)]
        public static void CreatePanelPrefab()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
            {
                Debug.Log($"[Dialogue] 프리팹이 이미 있습니다: {PrefabPath} (덮어쓰지 않음). 다시 만들려면 'Recreate Dialogue Panel Prefab (Overwrite)'");
                return;
            }

            BuildPanelPrefab();
        }

        [MenuItem("Tools/Dialogue/Recreate Dialogue Panel Prefab (Overwrite)", false, 21)]
        public static void RecreatePanelPrefab()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
                AssetDatabase.DeleteAsset(PrefabPath);
            BuildPanelPrefab();
        }

        /// <summary>
        /// TourHub 씬에 대화창을 <b>게임오브젝트로</b> 배치한다. 위치·크기는 씬에서 직접 고친다.
        /// 씬에 DialoguePanel 이 있으면 런타임(Dialogue.TryPlay)이 프리팹보다 먼저 그것을 쓴다.
        /// </summary>
        [MenuItem("Tools/Dialogue/Setup Dialogue Scene (TourHub)", false, 30)]
        public static void SetupDialogueScene()
        {
            const string hubScenePath = "Assets/Scenes/TourHub.unity";
            const string rootName = "[Dialogue]";

            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != hubScenePath)
            {
                if (!UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                    hubScenePath, UnityEditor.SceneManagement.OpenSceneMode.Single);
            }

            var existing = Object.FindFirstObjectByType<DialoguePanel>(FindObjectsInactive.Include);
            if (existing != null)
            {
                Debug.Log("[Dialogue] 씬에 DialoguePanel 이 이미 있습니다. 다시 만들려면 지우고 실행하세요.", existing);
                Selection.activeGameObject = existing.gameObject;
                return;
            }

            DialogueStyle style = GetOrCreateStyle();
            DialoguePanel panel = DialoguePanelFactory.Create(null, style);
            GameObject root = panel.gameObject;
            root.name = rootName;
            Undo.RegisterCreatedObjectUndo(root, "Create Dialogue");

            var blur = root.GetComponentInChildren<UIBlurBackdrop>(true);
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(BlurShaderPath);
            if (blur != null && shader != null) blur.EditorAssignShader(shader);

            root.SetActive(false); // Dialogue.TryPlay 가 켠다
            EditorUtility.SetDirty(panel);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = root;

            Debug.Log(
                $"[Dialogue] TourHub 씬에 '{rootName}' 을 배치했습니다. DialogueBox / NameTag / BodyText / NextArrow / SkipButton 을 " +
                "인스펙터에서 직접 옮기면 됩니다 (DialoguePanel 의 layoutFromStyle 이 꺼져 있어야 유지됨).",
                panel);
        }

        static void BuildPanelPrefab()
        {
            EnsureFolder(SettingsFolder);
            EnsureFolder(ResourceFolder);

            DialogueStyle style = GetOrCreateStyle();
            DialoguePanel panel = DialoguePanelFactory.Create(null, style);
            GameObject root = panel.gameObject;
            root.name = "DialoguePanel";

            // 블러 셰이더를 직접 연결해 빌드에서 스트리핑되지 않게 한다 (Shader.Find 는 에디터 폴백)
            var blur = root.GetComponentInChildren<UIBlurBackdrop>(true);
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(BlurShaderPath);
            if (blur != null && shader != null) blur.EditorAssignShader(shader);

            root.SetActive(false);
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();

            Debug.Log($"[Dialogue] 대화창 프리팹 생성: {PrefabPath}. 피그마 시안에 맞춰 이 프리팹·DialogueStyle 을 수정하면 된다.", saved);
        }

        // ---------------- 에셋 ----------------

        static DialogueStyle GetOrCreateStyle()
        {
            var style = AssetDatabase.LoadAssetAtPath<DialogueStyle>(StylePath);
            if (style == null)
            {
                style = ScriptableObject.CreateInstance<DialogueStyle>();
                AssetDatabase.CreateAsset(style, StylePath);
            }

            if (style.Font == null)
            {
                style.EditorSetFont(ProjectFontTool.TmpFont);
                EditorUtility.SetDirty(style);
            }

            // 시안 아트 (Sprites/0823_art). 임포트 교정은 TourMapSetup 이 같이 한다
            TourMapSetup.FixArtImport();
            style.EditorSetArt(
                TourMapSetup.LoadArt("1_dialogue"),
                TourMapSetup.LoadArt("1_print_done"),
                TourMapSetup.LoadArt("1_f_key"));
            EditorUtility.SetDirty(style);

            return style;
        }

        static DialogueCatalog GetOrCreateCatalog(DialogueStyle style, List<DialogueSequence> sequences)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<DialogueCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<DialogueCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            // 기존 카탈로그에 손으로 추가한 시퀀스는 유지하고, 기본 시퀀스만 보장한다
            var merged = new List<DialogueSequence>(catalog.Sequences);
            for (int i = 0; i < sequences.Count; i++)
                if (!merged.Contains(sequences[i])) merged.Add(sequences[i]);
            merged.RemoveAll(sequence => sequence == null);

            catalog.EditorConfigure(style, merged);
            EditorUtility.SetDirty(catalog);
            return catalog;
        }

        static List<DialogueSequence> CreateDefaultSequences(bool overwrite)
        {
            var result = new List<DialogueSequence>
            {
                CreateSequence("intro_stage_01", overwrite,
                    Line(Staff, "오늘 무대는 취소야. 아직 너희를 보러 올 관객이 없거든.", DialogueSpeakerSide.Right, "neutral"),
                    Line(Raccoon, "관객이 없어서 무대가 없다고? 그럼 먼저 관객을 만들면 되잖아!", DialogueSpeakerSide.Left, "surprised"),
                    Line(Hedgehog, "또 무작정 크게 연주하려는 건 아니지? 지나가는 사람은 취향이 전부 달라.", DialogueSpeakerSide.Left, "sigh"),
                    Line(Bandmate, "복장과 움직임을 봐. 누구는 강렬한 솔로를, 누구는 떼창을, 누구는 편한 흐름을 원해.", DialogueSpeakerSide.Left, "explain"),
                    Line(Raccoon, "좋아. 오늘은 귀부터 열고 연주한다!", DialogueSpeakerSide.Left, "resolve")),

                CreateSequence("intro_stage_02", overwrite,
                    Line(Promoter, "골목에서 사람을 멈춰 세웠다며? 오늘 오프닝 자리가 하나 비었어.", DialogueSpeakerSide.Right, "interested"),
                    Line(Raccoon, "드디어 천장과 조명이 있는 무대다!", DialogueSpeakerSide.Left, "excited"),
                    Line(Hedgehog, "여긴 단골이 많아. 가끔 원하는 걸 대놓고 요구하는 팬도 있어.", DialogueSpeakerSide.Left, "wary"),
                    Line(Bandmate, "특별 관객의 요청과 같은 Special 카드를 그 관객에게 직접 건네. 시간 안에 말이야.", DialogueSpeakerSide.Left, "explain"),
                    Line(Raccoon, "팬이 원하는 걸 말해 준다고? 그건 나도 알아들을 수 있지!", DialogueSpeakerSide.Left, "confident")),

                CreateSequence("intro_stage_03", overwrite,
                    Line(Promoter, "페스티벌은 라이브홀이랑 달라. 마음에 안 들면 관객은 바로 옆 무대로 가 버려.", DialogueSpeakerSide.Right, "hurried"),
                    Line(Rival, "작은 골목의 환호가 진짜 인기인 줄 알았나 봐?", DialogueSpeakerSide.Right, "sneer"),
                    Line(Raccoon, "우리 관객은 우리 공연을 선택할 거야!", DialogueSpeakerSide.Left, "angry"),
                    Line(Hedgehog, "경고가 뜨면 표시된 관객부터 봐. 호응도를 올리거나 Special 카드로 붙잡아.", DialogueSpeakerSide.Left, "calm"),
                    Line(Bandmate, "계획보다 관객이 먼저야. 흐름은 다시 만들 수 있어.", DialogueSpeakerSide.Left, "resolve")),

                CreateSequence("intro_stage_04", overwrite,
                    Line(Promoter, "오늘 공연은 생방송이야. 잘하면 견제가 몰리고, 흔들리면 늦게라도 팬들이 도우러 올 거야.", DialogueSpeakerSide.Right, "serious"),
                    Line(Raccoon, "잘해도 사건, 못해도 사건이라고?", DialogueSpeakerSide.Left, "confused"),
                    Line(Hedgehog, "그러니까 화면을 봐. 무슨 일이든 미리 알려 줄 테니 대응하면 돼.", DialogueSpeakerSide.Left, "smile"),
                    Line(Bandmate, "여기까지 얻은 카드와 증강을 전부 써 보자. 이 무대를 넘으면 스타디움이야.", DialogueSpeakerSide.Left, "cheer"),
                    Line(Raccoon, "변수까지 우리 공연의 일부로 만든다!", DialogueSpeakerSide.Left, "resolve")),

                CreateSequence("intro_stage_05_boss", overwrite,
                    Line(Rival, "우리는 관객 눈치를 보지 않아. 관객이 우리를 따라오게 만들지.", DialogueSpeakerSide.Right, "confident"),
                    Line(Raccoon, "나도 처음엔 그렇게 생각했어. 제일 큰 소리만 내면 되는 줄 알았지.", DialogueSpeakerSide.Left, "think"),
                    Line(Raccoon, "하지만 관객은 배경이 아니야. 각자 듣고 싶은 게 있는 우리 공연의 일부라고.", DialogueSpeakerSide.Left, "smile"),
                    Line(Hedgehog, "라이벌이 노리는 팬층을 전광판에 띄울 거야. 표시된 관객부터 지켜.", DialogueSpeakerSide.Left, "wary"),
                    Line(Bandmate, "마지막까지 관객의 목소리를 놓치지 마.", DialogueSpeakerSide.Left, "resolve"),
                    Line(Raccoon, "Raccoon Roll, 시작하자!", DialogueSpeakerSide.Left, "shout")),

                CreateSequence("ending_common", overwrite,
                    Line(Crowd, "RACCOON ROLL! RACCOON ROLL!", DialogueSpeakerSide.Right, "cheer"),
                    Line(Rival, "관객이 밴드를 고른 게 아니라, 밴드가 관객을 제대로 들은 건가….", DialogueSpeakerSide.Right, "defeat"),
                    Line(Hedgehog, "처음 골목에선 세 명뿐이었는데.", DialogueSpeakerSide.Left, "smile"),
                    Line(Raccoon, "숫자보다 중요한 걸 이제 알겠어. 다음 무대에도 먼저 관객부터 볼 거야.", DialogueSpeakerSide.Left, "resolve")),
            };

            return result;
        }

        static DialogueSequence CreateSequence(string id, bool overwrite, params DialogueLine[] lines)
        {
            string path = $"{SequenceFolder}/{id}.asset";
            var sequence = AssetDatabase.LoadAssetAtPath<DialogueSequence>(path);
            bool created = sequence == null;
            if (created)
            {
                sequence = ScriptableObject.CreateInstance<DialogueSequence>();
                AssetDatabase.CreateAsset(sequence, path);
            }

            if (created || overwrite)
            {
                sequence.EditorInitialize(id, new List<DialogueLine>(lines));
                EditorUtility.SetDirty(sequence);
            }

            return sequence;
        }

        static DialogueLine Line(string speaker, string text, DialogueSpeakerSide side, string emotion)
        {
            return new DialogueLine
            {
                speakerId = SpeakerIdOf(speaker),
                speakerName = speaker,
                text = text,
                side = side,
                emotionId = emotion,
            };
        }

        static string SpeakerIdOf(string speaker)
        {
            switch (speaker)
            {
                case Raccoon: return "raccoon";
                case Hedgehog: return "hedgehog";
                case Bandmate: return "bandmate";
                case Promoter: return "promoter";
                case Staff: return "staff";
                case Rival: return "rival";
                case Crowd: return "crowd";
                default: return speaker;
            }
        }
    }
}
