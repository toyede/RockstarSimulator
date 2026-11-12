using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ContextStage
{
    /// <summary>엔딩 대화 뒤에만 보이는 투어 기록지. 저장과 화면 이동은 TourPrototypeUI가 담당한다.</summary>
    [DisallowMultipleComponent]
    internal sealed class TourEndingSummaryView : MonoBehaviour
    {
        public enum RankingStatus { Ready, Uploading, Uploaded, LocalOnly }

        static readonly Vector2 FrameSize = new Vector2(1560f, 790f);
        static readonly Color Ink = new Color32(33, 29, 39, 255);
        static readonly Color Paper = new Color32(248, 237, 216, 255);
        static readonly Color MutedInk = new Color32(99, 86, 85, 255);
        static readonly Color WarmWhite = new Color32(255, 243, 221, 255);
        static readonly Color PaleText = new Color32(210, 196, 191, 255);

        RectTransform _frame;
        RectTransform _thanksPanel;
        GameObject _thanksBackdrop;
        CanvasGroup _recordGroup;
        Image _accentRule, _illustration;
        Font _font;
        Text _archiveTitle, _artLabel, _artFallback, _endingTitle, _summary, _score, _ranks, _audience;
        Text _recordLabel;
        InputField _nameInput;
        Button _recordButton, _returnButton;

        public string PlayerName => _nameInput.text;

        public static TourEndingSummaryView Create(Transform parent, Action onRecord, Action onReturn)
        {
            Image backdrop = TourPrototypeUIFactory.CreateFullscreenImage(parent, new Color32(20, 16, 28, 255));
            backdrop.gameObject.name = "TourEndingSummary";
            backdrop.gameObject.SetActive(false);
            var view = backdrop.gameObject.AddComponent<TourEndingSummaryView>();
            view.Build(onRecord, onReturn);
            return view;
        }

        public void Show(TourEndingVerdict verdict, string title, string summary, string audience, int totalScore,
            Sprite art, bool isEndingIllustration, bool resetName, RankingStatus rankingStatus)
        {
            Color accent = AccentFor(verdict?.kind);
            _accentRule.color = accent;
            _archiveTitle.color = Color.Lerp(accent, WarmWhite, .75f);
            _archiveTitle.text = verdict != null && verdict.failed ? "RACCOON ROLL  /  TOUR CLOSED" : "RACCOON ROLL  /  TOUR COMPLETE";
            _artLabel.text = isEndingIllustration ? GameLanguage.Pick("엔딩 일러스트", "Ending Illustration")
                : art != null ? GameLanguage.Pick("마지막 공연장", "Final Venue") : "";
            _illustration.sprite = art;
            _illustration.enabled = art != null;
            _artFallback.enabled = art == null;
            _endingTitle.text = title;
            _summary.text = summary;
            _score.text = totalScore.ToString("N0");
            _score.color = accent;
            _ranks.text = !string.IsNullOrEmpty(verdict?.ranks) ? verdict.ranks.Replace(" ", " / ") : "-";
            _audience.text = verdict != null ? $"{audience} ({verdict.topPreferenceScore:N0})" : "-";
            ApplyButtonColors(_recordButton, accent, new Color32(127, 116, 110, 255));
            if (resetName)
            {
                _nameInput.SetTextWithoutNotify("");
                ShowThanks(true);
            }
            SetRankingStatus(rankingStatus);
            gameObject.SetActive(true);
            FitFrame();
        }

        public void Hide() => gameObject.SetActive(false);

        public void SetRankingStatus(RankingStatus status)
        {
            bool ready = status == RankingStatus.Ready;
            _nameInput.interactable = ready;
            _recordButton.interactable = ready;
            // 씬 이동으로 전송 코루틴이 끊기지 않도록 응답(또는 타임아웃)까지만 기다린다.
            _returnButton.interactable = status != RankingStatus.Uploading;
            _recordLabel.text = status switch
            {
                RankingStatus.Uploading => GameLanguage.Pick("온라인 랭킹 등록 중…", "Submitting score..."),
                RankingStatus.Uploaded => GameLanguage.Pick("온라인 랭킹 등록 완료", "Score submitted"),
                RankingStatus.LocalOnly => GameLanguage.Pick("로컬 저장 완료 · 온라인 등록 실패", "Saved locally; upload failed"),
                _ => GameLanguage.Pick("랭킹 등록", "Submit Score"),
            };
        }

        void Build(Action onRecord, Action onReturn)
        {
            _font = DialogueCatalog.LoadDefault()?.Style?.Font?.sourceFontFile;
            _frame = Plate(transform, "TourRecordFrame", 0, 0, FrameSize.x, FrameSize.y, new Color32(58, 45, 61, 255));
            _recordGroup = _frame.gameObject.AddComponent<CanvasGroup>();
            _accentRule = Plate(_frame, "TopRule", 0, 391, FrameSize.x, 8, Ink).GetComponent<Image>();

            RectTransform left = Plate(_frame, "EndingPrint", -326, 0, 860, 734, Ink);
            _archiveTitle = Label(left, "ArchiveTitle", "", -120, 333, 580, 42, 24, WarmWhite);
            _artLabel = Label(left, "ArtLabel", "", 302, 333, 220, 42, 21, PaleText, TextAnchor.MiddleRight);
            RectTransform mat = Plate(left, "IllustrationMat", 0, 45, 820, 516, new Color32(17, 14, 24, 255));
            _illustration = Plate(mat, "EndingIllustration", 0, 0, 814, 510, Color.white).GetComponent<Image>();
            _illustration.preserveAspect = true;
            _artFallback = Label(mat, "ArtworkFallback", "RACCOON ROLL", 0, 0, 780, 82, 49, PaleText, TextAnchor.MiddleCenter);
            _endingTitle = Label(left, "EndingTitle", "", 0, -253, 820, 58, 46, WarmWhite);
            _summary = Label(left, "EndingSummary", "", 0, -323, 820, 66, 24, PaleText, wrap: true);

            RectTransform right = Plate(_frame, "ScoreTicket", 445, 0, 622, 734, Paper);
            Label(right, "RecordKicker", GameLanguage.Pick("TOUR RECORD  /  투어 기록", "TOUR RECORD"), 0, 317, 546, 40, 24, MutedInk);
            Label(right, "TotalLabel", GameLanguage.Pick("투어 총점", "Total Score"), 0, 255, 546, 38, 28, Ink);
            _score = Label(right, "TotalScore", "", 0, 190, 546, 96, 76, Ink);
            Color rule = new Color32(189, 172, 157, 255);
            Plate(right, "ScoreRule", 0, 125, 546, 3, rule);
            Label(right, "RanksLabel", GameLanguage.Pick("공연별 등급", "Show Ranks"), -143, 79, 260, 36, 22, MutedInk);
            _ranks = Label(right, "Ranks", "", -143, 35, 260, 48, 28, Ink);
            Label(right, "AudienceLabel", GameLanguage.Pick("가장 호응한 관객", "Top Fans"), 143, 79, 260, 36, 22, MutedInk);
            _audience = Label(right, "Audience", "", 143, 35, 260, 48, 28, Ink);
            Plate(right, "StatsDivider", -12, 56, 2, 70, rule);
            Plate(right, "EntryRule", 0, -21, 546, 3, rule);

            Label(right, "EntryTitle", GameLanguage.Pick("랭킹에 남길 이름", "Leaderboard Name"), 0, -70, 546, 39, 25, Ink);
            _nameInput = CreateNameInput(right);
            _recordButton = MakeButton(right, "RecordRanking", GameLanguage.Pick("랭킹 등록", "Submit Score"), -216, WarmWhite, onRecord, out _recordLabel);
            Plate(right, "ReturnOutline", 0, -305, 546, 68, new Color32(126, 109, 105, 255));
            _returnButton = MakeButton(right, "ReturnToTitle", GameLanguage.Pick("타이틀로 돌아가기", "Return to Title"), -305, Ink, onReturn, out _);
            TourPrototypeUIFactory.SetRect((RectTransform)_returnButton.transform, new Vector2(0, -305), new Vector2(540, 62));
            ApplyButtonColors(_returnButton, new Color32(226, 213, 194, 255), MutedInk);
            BuildThanks();
        }

        void BuildThanks()
        {
            Image shade = TourPrototypeUIFactory.CreateFullscreenImage(transform, new Color32(20, 16, 28, 190));
            shade.name = "EndingThanks";
            shade.raycastTarget = true;
            _thanksBackdrop = shade.gameObject;
            _thanksPanel = Plate(shade.transform, "ThanksPanel", 0, 0, 920, 300, Paper);
            Label(_thanksPanel, "ThanksTitle", GameLanguage.Pick(
                "Raccoon Roll을 플레이 해주셔서 감사합니다!", "Thank you for playing Raccoon Roll!"),
                0, 79, 860, 64, 37, Ink, TextAnchor.MiddleCenter);
            Label(_thanksPanel, "FeedbackAddress", GameLanguage.Pick(
                "피드백/건의사항은 proveprove2023@gmail.com으로 주실 수 있습니다.",
                "Send feedback or suggestions to proveprove2023@gmail.com."),
                0, 5, 840, 78, 28, MutedInk, TextAnchor.MiddleCenter, wrap: true);
            Button close = MakeButton(_thanksPanel, "CloseThanks", GameLanguage.Pick("확인", "OK"),
                -85, WarmWhite, () => ShowThanks(false), out _);
            ApplyButtonColors(close, Ink, MutedInk);
            ShowThanks(false);
        }

        void ShowThanks(bool show)
        {
            if (show && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(null);
            _recordGroup.interactable = !show;
            _recordGroup.blocksRaycasts = !show;
            _thanksBackdrop.SetActive(show);
        }

        InputField CreateNameInput(RectTransform parent)
        {
            RectTransform border = Plate(parent, "NameBorder", 0, -135, 546, 66, new Color32(126, 109, 105, 255));
            RectTransform field = Plate(border, "RankingName", 0, 0, 540, 60, new Color32(255, 249, 237, 255));
            Image background = field.GetComponent<Image>();
            background.raycastTarget = true;

            var viewport = new GameObject("NameViewport", typeof(RectTransform), typeof(RectMask2D)).GetComponent<RectTransform>();
            viewport.SetParent(field, false);
            TourPrototypeUIFactory.SetRect(viewport, Vector2.zero, new Vector2(498, 52));
            Text value = Label(viewport, "Value", "", 0, 0, 498, 52, 28, Ink);
            value.resizeTextForBestFit = false;
            Text hint = Label(viewport, "Placeholder", GameLanguage.Pick("닉네임 입력", "Enter nickname"), 0, 0, 498, 52, 25, MutedInk);

            InputField input = field.gameObject.AddComponent<InputField>();
            input.targetGraphic = background;
            input.textComponent = value;
            input.placeholder = hint;
            input.characterLimit = 12; // 랭킹 서버에 기록되는 이름 길이에 맞춘다.
            input.lineType = InputField.LineType.SingleLine;
            ApplyButtonColors(input, background.color, new Color32(230, 218, 199, 255));
            return input;
        }

        Button MakeButton(RectTransform parent, string name, string caption, float y, Color labelColor, Action action, out Text label)
        {
            RectTransform rect = Plate(parent, name, 0, y, 546, 68, Color.white);
            Image image = rect.GetComponent<Image>();
            image.raycastTarget = true;
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => action());
            UIInteractionSfx.Ensure(button);
            label = Label(rect, "Label", caption, 0, 0, 512, 60, 30, labelColor, TextAnchor.MiddleCenter);
            return button;
        }

        Text Label(Transform parent, string name, string value, float x, float y, float w, float h,
            int size, Color color, TextAnchor anchor = TextAnchor.MiddleLeft, bool wrap = false)
        {
            Text label = TourPrototypeUIFactory.CreateText(parent, name, size, anchor, color);
            if (_font != null) label.font = _font;
            label.supportRichText = false;
            label.text = value;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = Mathf.Max(16, size - 8);
            label.resizeTextMaxSize = size;
            if (wrap) label.lineSpacing = 1.12f;
            TourPrototypeUIFactory.SetRect(label.rectTransform, new Vector2(x, y), new Vector2(w, h));
            return label;
        }

        static RectTransform Plate(Transform parent, string name, float x, float y, float w, float h, Color color)
        {
            RectTransform plate = TourPrototypeUIFactory.CreatePanel(parent, name, new Vector2(w, h), new Vector2(x, y), color);
            plate.GetComponent<Image>().raycastTarget = false;
            return plate;
        }

        static void ApplyButtonColors(Selectable button, Color normal, Color disabled)
        {
            ColorBlock colors = button.colors;
            colors.normalColor = normal;
            colors.highlightedColor = Color.Lerp(normal, Color.white, .16f);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = Color.Lerp(normal, Ink, .18f);
            colors.disabledColor = disabled;
            button.colors = colors;
        }

        static Color AccentFor(TourEndingKind? kind) => kind switch
        {
            TourEndingKind.Chill => new Color32(15, 106, 109, 255),
            TourEndingKind.Singalong => new Color32(103, 59, 137, 255),
            TourEndingKind.Mosh => new Color32(157, 46, 68, 255),
            TourEndingKind.Bad => new Color32(120, 58, 46, 255),
            _ => new Color32(124, 85, 24, 255),
        };

        void OnRectTransformDimensionsChange() => FitFrame();

        void FitFrame()
        {
            if (_frame == null) return;
            Vector2 area = ((RectTransform)transform).rect.size;
            float fit = Mathf.Min(1f, area.x * .93f / FrameSize.x, area.y * .90f / FrameSize.y);
            if (fit > 0f) _frame.localScale = Vector3.one * fit;
            if (fit > 0f && _thanksPanel != null) _thanksPanel.localScale = Vector3.one * fit;
        }
    }
}
