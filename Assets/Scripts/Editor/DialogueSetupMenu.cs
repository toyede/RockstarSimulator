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
                // 너구리: 주인공, 어리숙하지만 투어를 거치며 성장 (봇치)
                // 고슴도치: 드러머, 너구리의 오랜 친구, 정열적 (류지)
                // LUX//FAUNA: 생성형 AI 로봇 말투, 완벽주의
                CreateSequence("intro_stage_01", overwrite,
                    Line(Hedgehog, "야, 너구리! 오늘이 그날이라고! 골목이든 어디든 무대는 무대야!", DialogueSpeakerSide.Left, "excited"),
                    Line(Raccoon, "어, 어… 사람이 이렇게 많이 지나가는데… 우리 노래를… 들어줄까…?", DialogueSpeakerSide.Left, "nervous"),
                    Line(Hedgehog, "안 들으면 듣게 만들면 되지! 근데 무작정 크게 치진 마. 지나가는 사람은 취향이 전부 달라.", DialogueSpeakerSide.Left, "grin"),
                    Line(Raccoon, "취향… 옷차림이랑 표정을 보면… 알 수 있을지도. 강렬한 걸 원하는 사람, 같이 부르고 싶은 사람, 편하게 듣고 싶은 사람…", DialogueSpeakerSide.Left, "think"),
                    Line(Hedgehog, "오오, 이제 좀 밴드 같네! 자, 귀부터 열고 간다!", DialogueSpeakerSide.Left, "cheer"),
                    Line(Raccoon, "…응. 오늘은, 도망치지 않을게.", DialogueSpeakerSide.Left, "resolve")),

                CreateSequence("intro_stage_02", overwrite,
                    Line(Staff, "골목에서 사람들을 멈춰 세웠다는 밴드가 너희야? 오늘 오프닝 자리가 하나 비었어.", DialogueSpeakerSide.Right, "neutral"),
                    Line(Raccoon, "처, 천장이 있어… 조명도… 지, 진짜 무대다…!", DialogueSpeakerSide.Left, "surprised"),
                    Line(Hedgehog, "긴장 풀어! 여긴 단골이 많아서 원하는 걸 대놓고 말하는 팬도 있어. 오히려 편하지!", DialogueSpeakerSide.Left, "grin"),
                    Line(Raccoon, "원하는 걸 말해준다면… 그건 나도 알아들을 수 있어. 특별 관객이 요청하면 그 성향의 Special 카드를 직접 건네자.", DialogueSpeakerSide.Left, "think"),
                    Line(Hedgehog, "그거야! 대신 시간 안에 해. 단골은 기다려주지 않거든!", DialogueSpeakerSide.Left, "cheer")),

                CreateSequence("intro_stage_03", overwrite,
                    Line(Promoter, "페스티벌은 라이브홀과 달라. 옆 무대가 시끄러우면 관객은 바로 옮겨가. 소나기라도 오면… 알아서들 해.", DialogueSpeakerSide.Right, "hurried"),
                    Line(Rival, "분석 완료. 골목 출신 밴드, 팬 결집도 낮음. 위협 요소로 판단되지 않습니다.", DialogueSpeakerSide.Right, "cold"),
                    Line(Raccoon, "…지금 우릴, 위협이 아니래.", DialogueSpeakerSide.Left, "hurt"),
                    Line(Hedgehog, "말 한번 재수 없게 하네! 두고 봐, 오늘 깃발은 우리 쪽에서 흔들린다!", DialogueSpeakerSide.Left, "angry"),
                    Line(Raccoon, "옆 무대 경고가 뜨면 표시된 관객부터 붙잡고… 비가 오면 CHILL 카드로 우산을. 콤보를 이으면 깃발이 흔들려.", DialogueSpeakerSide.Left, "think"),
                    Line(Hedgehog, "머리엔 다 들어있네. 그럼 몸으로 보여주자고!", DialogueSpeakerSide.Left, "cheer")),

                CreateSequence("intro_stage_04", overwrite,
                    Line(Promoter, "전국 생방송이야. 카메라 큐시트대로 움직여야 하고… 방송 사고는 절대 없어야 해. 알았지?", DialogueSpeakerSide.Right, "serious"),
                    Line(Raccoon, "생, 생방송… 정전 같은 건… 안 나겠지…?", DialogueSpeakerSide.Left, "nervous"),
                    Line(Hedgehog, "나면 어때? 불이 꺼져도 관객이 어디 서 있는지는 우리가 알잖아!", DialogueSpeakerSide.Left, "grin"),
                    Line(Raccoon, "…그래. 큐시트 순서대로 카드를 내고, 불이 꺼지면 기억으로 연주한다. Miss 없이.", DialogueSpeakerSide.Left, "resolve"),
                    Line(Hedgehog, "여기까지 모은 카드랑 증강 전부 쏟아붓자. 이 무대 넘으면 스타디움이야!", DialogueSpeakerSide.Left, "cheer")),

                CreateSequence("intro_stage_05_boss", overwrite,
                    Line(Rival, "입장 확인. RACCOON ROLL. 예상 승률 3.2%. 오늘 관객의 87%는 이미 저희 쪽에 서 있습니다.", DialogueSpeakerSide.Right, "cold"),
                    Line(Raccoon, "…숫자로 다 아는 것처럼 말하네.", DialogueSpeakerSide.Left, "calm"),
                    Line(Rival, "저희는 관객의 눈치를 보지 않습니다. 관객이 저희를 따라오도록 설계할 뿐.", DialogueSpeakerSide.Right, "confident"),
                    Line(Hedgehog, "설계 좋아하네! 관객은 계산기가 아니야. 야, 너구리. 골목에서 뭐라고 했더라?", DialogueSpeakerSide.Left, "angry"),
                    Line(Raccoon, "…도망치지 않는다고 했어. 라이벌 무대의 팬이 많을수록 우리 점수가 깎여. 그러니까 뺏어온다. 한 명씩, 전부.", DialogueSpeakerSide.Left, "resolve"),
                    Line(Hedgehog, "그래야 우리 보컬이지! 패턴 뜨면 정면으로 받아쳐. 관객 과반수를 넘기면 저 로봇들, 화낼 거다!", DialogueSpeakerSide.Left, "cheer"),
                    Line(Raccoon, "Raccoon Roll… 시작하자!", DialogueSpeakerSide.Left, "shout")),

                CreateSequence("ending_common", overwrite,
                    Line(Crowd, "RACCOON ROLL! RACCOON ROLL!", DialogueSpeakerSide.Right, "cheer"),
                    Line(Rival, "…재계산 중. 예측 오차 원인: 불명. 관객이 밴드를 고른 것이 아니라, 밴드가 관객을… 들었다?", DialogueSpeakerSide.Right, "defeat"),
                    Line(Hedgehog, "처음 골목에선 세 명이었는데 말이야.", DialogueSpeakerSide.Left, "smile"),
                    Line(Raccoon, "숫자보다 중요한 걸 알았어. 다음 무대에서도, 먼저 관객부터 볼 거야. …이번엔, 안 떨렸어.", DialogueSpeakerSide.Left, "resolve")),
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
                portrait = PortraitOf(speaker),
            };
        }

        /// <summary>스탠딩 일러 (Sprites/0910_art/스탠딩일러). 없는 화자는 null — 일러 없이 이름만 표시된다.</summary>
        static Sprite PortraitOf(string speaker)
        {
            string file;
            switch (speaker)
            {
                case Raccoon: file = "raccoon_standing"; break;
                case Hedgehog: file = "hedgehog_standing"; break;
                case Rival: file = "lux_fauna_standing"; break;
                default: return null;
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Sprites/0910_art/스탠딩일러/{file}.png");
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
