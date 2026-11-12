using GameJamKit;
using UnityEngine;
using UnityEngine.UI;

namespace ContextStage
{
    /// <summary>
    /// 타이틀의 "옵션" 버튼으로 여는 설정 패널. 킷 UIPopup 상속.
    ///
    /// 볼륨 조절은 AudioVolumeSlider 가 슬라이더별로 알아서 처리하므로
    /// 이 클래스는 닫기 버튼 콜백만 있으면 된다.
    ///
    /// 씬 배치 규칙 (킷 UIPopup 공통):
    /// - 팝업 루트 오브젝트는 반드시 "활성 상태"로 둘 것 (startHidden 이 알아서 숨김)
    /// </summary>
    public class OptionsPopup : UIPopup
    {
        PausePopup _coveredPause;
        Text _language, _metrics, _metricsNote;

        protected override void Awake()
        {
            base.Awake();
            OnClosed += RestorePauseMenu;
        }

        protected override void OnOpen()
        {
            _coveredPause = UIManager.Instance.Get<PausePopup>();
            if (_coveredPause != null && _coveredPause.IsOpen) _coveredPause.SetMenuCovered(true);
            PausePopup.RaiseOverlay(this);
            if (_language == null) BuildExtras();
            GameLanguage.RefreshMenus();
            RefreshExtras();
        }

        void BuildExtras()
        {
            RectTransform parent = transform.Find("Content") as RectTransform;
            if (parent == null) return;
            Font font = parent.Find("Title")?.GetComponent<Text>()?.font;
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _language = CreateOption(parent, "GameLanguage", -377f, font, () =>
            {
                GameLanguage.SetEnglish(!GameLanguage.English);
                RefreshExtras();
            });
            _metrics = CreateOption(parent, "PlaytestMetrics", -436f, font, () =>
            {
                PlaytestMetrics.SetEnabled(!PlaytestMetrics.Enabled);
                RefreshExtras();
            });
            _metricsNote = CreateLabel(parent, "PlaytestMetricsNotice", -490f, 50f, font, 15);
        }

        void RefreshExtras()
        {
            if (_language == null) return;
            _language.text = GameLanguage.Pick("언어: 한국어  /  English (Preview)", "Language: English (Preview)  /  한국어");
            bool remote = PlaytestMetrics.RemoteConfigured;
            bool enabled = PlaytestMetrics.Enabled;
            string state = enabled ? GameLanguage.Pick("켜짐", "ON") : GameLanguage.Pick("꺼짐", "OFF");
            _metrics.text = (remote ? GameLanguage.Pick("플레이테스트 지표 공유", "Share playtest metrics")
                : GameLanguage.Pick("플레이테스트 지표 기록", "Record playtest metrics")) + ": " + state;
            _metricsNote.text = PlaytestMetrics.RemoteConfigured
                ? GameLanguage.Pick("카드 선택·튜토리얼 통계만 전송합니다. 이름은 수집하지 않습니다.",
                    "Sends card and tutorial statistics only. No player names.")
                : GameLanguage.Pick("서버 전송 미설정 · 동의 시 테스트 기기에만 기록합니다.",
                    "Uploads not configured. Opted-in metrics stay on this device.");
        }

        static Text CreateOption(RectTransform parent, string name, float y, Font font, UnityEngine.Events.UnityAction onClick)
        {
            var row = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            RectTransform rect = (RectTransform)row.transform;
            rect.SetParent(parent, false);
            Place(rect, y, 47f);
            var image = row.GetComponent<Image>();
            image.color = new Color32(50, 41, 59, 255);
            var button = row.GetComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(onClick);
            UIInteractionSfx.Ensure(button);
            Text label = CreateLabel(rect, "Label", 0f, 0f, font, 22);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.sizeDelta = Vector2.zero;
            label.rectTransform.anchoredPosition = Vector2.zero;
            return label;
        }

        static Text CreateLabel(RectTransform parent, string name, float y, float height, Font font, int size)
        {
            var label = new GameObject(name, typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            label.rectTransform.SetParent(parent, false);
            Place(label.rectTransform, y, height);
            label.alignment = TextAnchor.MiddleCenter;
            label.font = font;
            label.fontSize = size;
            label.color = Color.white;
            label.raycastTarget = false;
            return label;
        }

        static void Place(RectTransform rect, float y, float height)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1f);
            rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = new Vector2(0f, y);
            rect.sizeDelta = new Vector2(420f, height);
        }

        void OnDisable() => RestorePauseMenu();

        void RestorePauseMenu()
        {
            if (_coveredPause != null) _coveredPause.SetMenuCovered(false);
            _coveredPause = null;
        }
        public void OnClickClose() => Close();
    }
}
