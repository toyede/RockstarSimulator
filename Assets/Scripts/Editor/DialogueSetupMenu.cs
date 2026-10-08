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
    ///     Settings/Dialogue/Sequences/intro_stage_01 ~ 05_boss, 엔딩 대사 초안
    ///     Resources/Dialogue/DialogueCatalog.asset
    ///
    ///   Tools/Dialogue/Rewrite Default Sequences (Overwrite)
    ///     시퀀스 대사를 승인된 스토리 초안으로 되돌린다 (손으로 고친 대사가 지워진다)
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
        const string Skunk = "스컹크";
        const string Rival = "LUX//FAUNA";

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

            // 대사만 교체할 때는 기존 스타일·아트 임포트·수동 배치를 다시 셋업하지 않는다.
            var existingCatalog = AssetDatabase.LoadAssetAtPath<DialogueCatalog>(CatalogPath);
            DialogueStyle style = existingCatalog != null ? existingCatalog.Style : null;
            if (style == null) style = AssetDatabase.LoadAssetAtPath<DialogueStyle>(StylePath);
            if (style == null) style = GetOrCreateStyle();
            List<DialogueSequence> sequences = CreateDefaultSequences(overwrite: true);
            DialogueCatalog catalog = GetOrCreateCatalog(style, sequences);

            // 현재 씬이나 다른 팀원이 수정 중인 에셋까지 저장하지 않는다.
            foreach (DialogueSequence sequence in sequences) AssetDatabase.SaveAssetIfDirty(sequence);
            AssetDatabase.SaveAssetIfDirty(catalog);
            if (!catalog.TryValidate(out string error))
                Debug.LogError($"[Dialogue] 카탈로그 검증 실패: {error}", catalog);
            else
                Debug.Log($"[Dialogue] 스토리 초안 {sequences.Count}개 적용 완료. 인트로 5개는 기존 투어에서 재생되며, 엔딩 자동 분기는 별도 연결이 필요합니다.", catalog);
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
                // 게임 규칙은 기존 룰 카드가 안내한다. 아래는 캐릭터의 말과 짧은 상황 지문만.
                // 3인 밴드: 너구리(보컬/기타), 고슴도치(드럼), Stage 2부터 스컹크(베이스).
                CreateSequence("intro_stage_01", overwrite,
                    Narration("퇴근길 골목. 너구리와 고슴도치가 작은 앰프 앞에서 공연을 준비한다."),
                    Line(Raccoon, "몇 시에 시작한다고 올렸지?"),
                    Line(Hedgehog, "일곱 시."),
                    Line(Raccoon, "아직 좀 남았나?"),
                    Line(Hedgehog, "지났어."),
                    Line(Raccoon, "아, 안녕하세요. 저희는...", "nervous"),
                    Line(Hedgehog, "마이크."),
                    Line(Raccoon, "왜?"),
                    Line(Hedgehog, "안 켰어."),
                    Narration("마이크를 켜자 맞은편 전광판에서 광고 음악이 울린다.\n'LUX//FAUNA 스타디움 공연'"),
                    Line(Raccoon, "...저거 끝나고 하자."),
                    Line(Hedgehog, "아까도 그랬잖아. 계속 나오는 거야."),
                    Line(Raccoon, "알았어. 잠깐만."),
                    Narration("너구리가 기타를 고쳐 메고 고개를 끄덕인다."),
                    Line(Hedgehog, "원, 투. 원, 투, 쓰리, 포!", "excited")),

                CreateSequence("intro_stage_02", overwrite,
                    Narration("지하 라이브홀의 오프닝 무대.\n구석에서는 스컹크가 베이스와 장비를 정리하고 있다."),
                    Line(Raccoon, "기타 앰프에서 소리가 안 나. 입력 케이블이 헐거운가?", "nervous"),
                    Line(Hedgehog, "아까는 났잖아."),
                    CablePopNarration(),
                    Line(Skunk, "볼륨 켠 채로 입력 케이블을 뽑은 거야?"),
                    Line(Raccoon, "소리가 안 나서요.", "nervous"),
                    Line(Skunk, "그래서 그냥 뽑았어?"),
                    Line(Raccoon, "...네."),
                    Line(Skunk, "앰프 볼륨부터 내려. 케이블은 내가 꽂을게."),
                    Line(Hedgehog, "고장 난 거예요?"),
                    Line(Skunk, "잠깐."),
                    Narration("스컹크가 앰프를 살핀다. 너구리는 고슴도치 쪽으로 바짝 붙는다."),
                    Line(Raccoon, "이거 비싸 보이냐?"),
                    Line(Hedgehog, "나한테 물어보지 마."),
                    Line(Skunk, "입력 잭 다시 꽂았어. 기타 줄 한 번 튕겨봐."),
                    Narration("스컹크가 연결을 확인하고 자기 베이스를 튕긴다. 낮은 음이 정상적으로 울린다."),
                    Line(Raccoon, "...다행이다.", "relieved"),
                    Line(Skunk, "내가 할 말이고. 내 앰프야."),
                    Line(Skunk, "오늘 공연하는 애들이 너희야?"),
                    Line(Hedgehog, "네."),
                    Line(Skunk, "몇 시?"),
                    Line(Hedgehog, "십 분 뒤요."),
                    Line(Skunk, "베이스는 어디 있는데?"),
                    Line(Raccoon, "아직 못 구했어요."),
                    Line(Skunk, "첫 곡 인트로 여덟 마디만 맞춰보자. 베이스는 내가 넣을게."),
                    Narration("합주가 시작되자 스컹크가 베이스를 얹는다.\n돌아보는 너구리에게 앞을 보라는 눈짓이 돌아온다."),
                    Line(Raccoon, "그대로 공연까지 해주시면 안 돼요?"),
                    Line(Skunk, "지금?"),
                    Line(Hedgehog, "지금 아니면 다음 사람이 없어요."),
                    Narration("스컹크는 시계를 보고도 베이스를 내려놓지 않는다."),
                    Line(Skunk, "곡 순서랑 후렴 들어가는 마디 적어줘. 오늘 베이스는 내가 맡을게.")),

                CreateSequence("intro_stage_03", overwrite,
                    Narration("페스티벌 대기 구역. 너구리가 휴대전화에서 오래된 공연 영상을 찾았다."),
                    Line(Raccoon, "형, 이 밴드에도 있었어요?"),
                    Line(Skunk, "그걸 어디서 찾았어?"),
                    Line(Raccoon, "검색하니까 나오던데요. 밴드가 몇 개예요?"),
                    Line(Skunk, "그만 좀 넘겨."),
                    Line(Raccoon, "이 곡은 제목이 뭐예요? 음원이 안 나오는데."),
                    Line(Skunk, "안 냈어."),
                    Line(Hedgehog, "왜요? 괜찮은데."),
                    Line(Skunk, "전에 잘된 곡이 있었거든. 그런 걸 먼저 내자고 해서."),
                    Line(Raccoon, "그럼 이건요?"),
                    Line(Skunk, "Verse 2 뒤 베이스 솔로를 빼자더라. 후렴을 한 번 더 넣으래."),
                    Line(Raccoon, "결국 안 했어요?"),
                    Line(Skunk, "응. 다른 곡 가져가도 비슷했고."),
                    Line(Raccoon, "그래서 나온 거예요?"),
                    Line(Skunk, "솔로 여덟 마디는 들어보고 자르든가. 리허설도 하기 전에 빼자잖아."),
                    Narration("너구리가 영상을 멈춘다. 스컹크는 휴대전화에서 시선을 뗀다."),
                    Line(Skunk, "우리 순서나 봐."),
                    Narration("메인 무대 전광판에 'HEADLINER: LUX//FAUNA'가 뜬다.\n안내 방송을 들은 관객들이 이동하기 시작한다."),
                    Line(Hedgehog, "저쪽도 지금 시작하나 봐요."),
                    Line(Raccoon, "우리랑 겹치네. 다 저기로 가는데?"),
                    Narration("스컹크가 작은 무대 앞에 남아 있는 사람들을 가리킨다."),
                    Line(Skunk, "저기 기다리잖아. 들어가자.")),

                CreateSequence("intro_stage_04", overwrite,
                    Narration("방송국 대기실. 모니터에서 파이널 예고가 흘러나온다.\n'LUX//FAUNA와 맞설 마지막 도전자는?'"),
                    Narration("화면이 대기실의 세 사람으로 바뀐다. 너구리가 황급히 자세를 고친다."),
                    Line(Raccoon, "저거 지금 나가는 거야?", "nervous"),
                    Line(Hedgehog, "대기 화면 아니야?"),
                    Line(Skunk, "빨간 불 들어왔는데."),
                    Line(Raccoon, "아까부터 나갔나?"),
                    Line(Hedgehog, "나 방금 이 쑤셨는데."),
                    Line(Raccoon, "너 방송 링크 보냈어?"),
                    Line(Hedgehog, "응. 단체방에."),
                    Line(Raccoon, "지금?"),
                    Line(Hedgehog, "보내달라며."),
                    Line(Raccoon, "...그러긴 했지."),
                    Narration("스컹크가 케이블을 다시 확인하고 베이스를 짧게 튕긴다."),
                    Line(Skunk, "내 소리 들려?"),
                    Line(Hedgehog, "네."),
                    Narration("스컹크가 같은 음을 한 번 더 튕긴다."),
                    Line(Hedgehog, "들려요. 방금도 들렸어요."),
                    Line(Raccoon, "형은 방송 해봤죠?"),
                    Line(Skunk, "녹화는."),
                    Line(Raccoon, "생방은요?"),
                    Line(Skunk, "...처음."),
                    Line(Raccoon, "오늘 통과하면 진짜 저기 가는 거네."),
                    Line(Hedgehog, "일단 오늘 것부터 하자. 나 손에 땀나."),
                    Narration("고슴도치가 여분 스틱을 옆에 놓는다. 스태프가 세 사람을 부른다."),
                    Line(Skunk, "휴대전화 내려놔. 우리 들어간다.")),

                CreateSequence("intro_stage_05_boss", overwrite,
                    Narration("스타디움 파이널. 무대 입구에서 전자음악 듀오 LUX//FAUNA와 마주친다."),
                    Line(Rival, "방송 봤어요. 후렴 좋던데."),
                    Line(Raccoon, "아, 감사합니다."),
                    Line(Rival, "후렴 앞 베이스 솔로 여덟 마디, 네 마디로 줄여보세요."),
                    Line(Rival, "바로 후렴으로 들어가면 반응이 더 빠를 거예요."),
                    Narration("베이스를 만지던 스컹크의 손이 잠깐 멈춘다."),
                    Line(Raccoon, "오늘은 맞춰온 대로 하려고요."),
                    Line(Rival, "그래요? 저희는 첫 드랍 전에 객석이 식으면 빌드업부터 잘라요."),
                    Line(Raccoon, "그 베이스 솔로 듣고 만든 후렴이라서요. 같이 연주하려고요."),
                    Line(Rival, "그럼 공연 때 들어볼게요."),
                    Narration("LUX//FAUNA가 스태프를 따라 무대로 올라간다."),
                    Line(Hedgehog, "뭐래?"),
                    Line(Raccoon, "후렴 앞 베이스 솔로를 여덟 마디에서 네 마디로 줄이래."),
                    Line(Hedgehog, "지금 와서?", "angry"),
                    Line(Skunk, "안 줄여. 아까 맞춘 대로 가."),
                    Narration("맞은편 무대에서 첫 전자음이 울린다. 객석이 환호로 들썩인다."),
                    Line(Raccoon, "저쪽은 소리만 내도 좋아하네."),
                    Line(Skunk, "네 기타는 소리 나와?"),
                    Narration("너구리가 줄을 튕긴다. 아무 소리도 나지 않는다."),
                    Line(Raccoon, "...잠깐."),
                    Narration("너구리가 기타 볼륨을 올린다. 이번에는 소리가 난다."),
                    Line(Hedgehog, "좋아. 이제 우리도 소리는 난다."),
                    Line(Raccoon, "저희 쪽도 잘 들리세요?"),
                    Narration("무대 앞에서 몇몇 관객이 손을 들고 소리친다."),
                    Line(Raccoon, "됐죠?"),
                    Line(Skunk, "응."),
                    Line(Hedgehog, "가?"),
                    Line(Raccoon, "가자."),
                    Line(Hedgehog, "원, 투. 원, 투, 쓰리, 포!", "excited")),

                // 기존 ID는 호환용으로 유지. 엔딩 선택/점수 판정은 이 셋업의 책임이 아니다.
                CreateSequence("ending_common", overwrite,
                    Narration("공연이 끝난 뒤. 세 사람은 무대 뒤에서 장비를 챙긴다."),
                    Line(Raccoon, "오늘 영상 찍혔겠죠?"),
                    Line(Hedgehog, "응. 네 기타 소리 안 나던 데부터."),
                    Line(Raccoon, "거긴 좀 잘라."),
                    Line(Skunk, "영상은 나가서 봐. 뒤에 다른 팀 기다린다."),
                    Line(Raccoon, "형, 앰프는 제가 옮길게요."),
                    Line(Skunk, "케이블은 건드리지 말고.")),

                CreateSequence("ending_mosh", overwrite,
                    Narration("투어가 끝난 뒤, 다시 찾은 지하 클럽.\n마지막 곡이 끝나도 무대 앞의 팬들은 물러서지 않는다."),
                    Line(Raccoon, "받아줄 거지?", "excited"),
                    Narration("관객들이 손을 든다. 너구리가 무대 밖으로 몸을 기울인다."),
                    Line(Hedgehog, "야. 기타."),
                    Line(Raccoon, "아."),
                    Narration("기타를 벗어놓은 너구리가 관객들의 손 위로 뛰어든다."),
                    Line(Hedgehog, "오, 됐다."),
                    Narration("스컹크도 웃으며 무대 앞으로 다가온다. 너구리는 점점 객석 뒤로 밀려간다."),
                    Line(Raccoon, "잠깐! 나 어디 가?"),
                    Line(Hedgehog, "뒤로 간다!"),
                    Line(Raccoon, "무대 쪽! 무대 쪽으로!"),
                    Narration("너구리가 관객들 위를 떠간다. 벗겨진 왕관은 어느 팬의 손에 들려 있다.")),

                CreateSequence("ending_singalong", overwrite,
                    Narration("야외 공연이 끝난 뒤. 밖에서는 아직도 팬들이 후렴을 부르고 있다."),
                    Line(Raccoon, "오늘 영상 이걸로 올릴까?"),
                    Line(Hedgehog, "좀 앞으로 돌려봐."),
                    Narration("휴대전화에서 관객들의 노랫소리가 터져 나온다."),
                    Line(Raccoon, "내 목소리가 거의 안 들리네."),
                    Line(Hedgehog, "뒤쪽 봐. 저기까지 다 부른다."),
                    Narration("스컹크도 두 사람 옆으로 와서 화면을 들여다본다."),
                    Line(Raccoon, "다른 각도도 찾아볼까요?"),
                    Line(Skunk, "왜? 이거 좋은데."),
                    Line(Hedgehog, "여기. 마이크 내리는 데부터 올려."),
                    Line(Raccoon, "아직도 부르네.", "happy"),
                    Line(Hedgehog, "너 아까 두 번 더 시켰잖아."),
                    Line(Raccoon, "...그랬지."),
                    Narration("너구리가 영상을 올린다. 열린 문 사이로 후렴이 한 번 더 들려온다.")),

                CreateSequence("ending_chill", overwrite,
                    Narration("세 사람이 직접 녹음하고 올린 새 싱글.\n작은 작업실에서 음원과 라이브 영상의 댓글을 확인한다."),
                    Line(Raccoon, "우리 곡 추천 목록에 들어갔어.", "happy"),
                    Line(Hedgehog, "어디?"),
                    Line(Raccoon, "여기. 사진은 이걸로 나가네."),
                    Line(Hedgehog, "내가 눈 감은 거?"),
                    Line(Raccoon, "...작게 보면 괜찮아."),
                    Line(Hedgehog, "난 크게 보이는데."),
                    Line(Raccoon, "형, 이거 물어보는데요."),
                    Line(Skunk, "뭘?"),
                    Line(Raccoon, "베이스 어떻게 녹음했냐고. 1분 42초."),
                    Narration("스컹크가 해당 구간을 듣더니 구석의 앰프를 가리킨다."),
                    Line(Skunk, "저 앰프."),
                    Line(Hedgehog, "네가 터뜨릴 뻔한 거."),
                    Line(Raccoon, "안 터졌잖아."),
                    Line(Raccoon, "사진도 올려줄까요?"),
                    Line(Skunk, "응. 뒤쪽도 찍어줘."),
                    Narration("너구리가 앰프 사진을 찍는다. 스컹크는 녹음 방법을 댓글로 적고 있다."),
                    Line(Hedgehog, "형, 그걸 다 쓰게요?"),
                    Line(Skunk, "물어봤잖아.")),

                CreateSequence("ending_all_s", overwrite,
                    Narration("스타디움 밖. 대형 전광판에 RACCOON ROLL의 다음 투어 광고가 뜬다."),
                    Line(Raccoon, "잠깐. 우리 나온다.", "happy"),
                    Line(Hedgehog, "아까도 봤어."),
                    Line(Raccoon, "사진 좀 찍어줘. 광고 바뀌기 전에."),
                    Line(Hedgehog, "그럼 이거 받아."),
                    Narration("장비를 넘겨받는 사이 전광판이 다른 광고로 바뀐다."),
                    Line(Raccoon, "아."),
                    Line(Hedgehog, "또 나오겠지."),
                    Line(Raccoon, "형 먼저 가셔도 돼요."),
                    Line(Skunk, "나도 찍어야지."),
                    Narration("스컹크가 장비를 내려놓는다. 잠시 뒤 세 사람의 광고가 다시 돌아온다."),
                    Line(Skunk, "너도 들어와. 타이머 해놓고."),
                    Line(Hedgehog, "폰 어디 세워?"),
                    Line(Raccoon, "내 케이스에 기대."),
                    Narration("사진이 찍히는 순간, 너구리 혼자 뒤돌아서 전광판을 보고 있다."),
                    Line(Hedgehog, "...한 번 더 찍자.")),

                // 같은 배드엔딩의 합류 전/후 대사. Stage 1 실패에 스컹크가 나타나지 않도록 분리한다.
                CreateSequence("ending_bad_before_join", overwrite, CreateBadEndingLines(withSkunk: false)),
                CreateSequence("ending_bad", overwrite, CreateBadEndingLines(withSkunk: true)),
            };

            return result;
        }

        static DialogueLine[] CreateBadEndingLines(bool withSkunk)
        {
            var lines = new List<DialogueLine>
            {
                Narration("공연이 중단되고 장비를 빼달라는 말을 들었다. 밖에는 케이스들이 쌓여 있다."),
                Line(Raccoon, "드럼도 지금 다 빼달래."),
                Line(Hedgehog, "의자 하나 남았어. 가져올게."),
                Line(Raccoon, "오늘 영상 찍었어?"),
                Line(Hedgehog, "켜놓긴 했는데."),
                Line(Raccoon, "그건 올리지 마."),
                Line(Hedgehog, "알았어."),
                Line(Raccoon, "내가 나중에 옮길게. 너 먼저 가."),
                Line(Hedgehog, "그걸 혼자 어떻게 들어."),
                Line(Raccoon, "조금씩 옮기면...", "crying"),
                Line(Hedgehog, "됐어. 반대쪽 잡아.", "angry"),
                Narration("둘이 드럼 케이스의 양쪽을 잡고 들어 올린다."),
            };
            if (withSkunk)
            {
                lines.Add(Narration("스컹크가 출입문을 잡아준다."));
                lines.Add(Line(Skunk, "천천히. 문턱 있어."));
                lines.Add(Line(Raccoon, "...형 앰프도 제가 옮길게요."));
                lines.Add(Line(Skunk, "내 건 내가 들어. 네 기타부터 챙겨."));
                lines.Add(Narration("스컹크는 두 사람이 나올 때까지 문을 잡고 기다린다."));
            }
            return lines.ToArray();
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
                if (!created) Undo.RecordObject(sequence, "Rewrite Dialogue Story");
                sequence.EditorInitialize(id, new List<DialogueLine>(lines));
                EditorUtility.SetDirty(sequence);
            }

            return sequence;
        }

        static DialogueLine Line(string speaker, string text, string emotion = "neutral")
        {
            return new DialogueLine
            {
                speakerId = SpeakerIdOf(speaker),
                speakerName = speaker,
                text = text,
                side = speaker == Raccoon ? DialogueSpeakerSide.Left : DialogueSpeakerSide.Right,
                emotionId = emotion,
                portrait = PortraitOf(speaker),
            };
        }

        // 연출 에셋이 없는 행동/광고는 짧은 지문으로 전달한다. 캐릭터의 발화로 처리하지 않는다.
        static DialogueLine Narration(string text) => new DialogueLine { text = text };

        static DialogueLine CablePopNarration() => new DialogueLine
        {
            text = "너구리가 볼륨을 켠 채 입력 케이블을 뽑는다.\n펑! 앰프에서 큰 소리가 나며 먼지가 튄다.",
            sfxId = "guitar_stroke",
            presentationCue = "cable_pop"
        };

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
                case Skunk: return "skunk";
                case Rival: return "rival";
                default: return speaker;
            }
        }
    }
}
