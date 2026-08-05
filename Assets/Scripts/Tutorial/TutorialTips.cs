using GameJamKit;
using UnityEngine;
using UnityEngine.UI;

namespace ContextStage
{
    /// <summary>
    /// 본 게임에서 기능이 "처음 발생하는 순간" 한 줄짜리 팁을 띄운다.
    /// (튜토리얼에 다 넣으면 기억되지 않으므로 발생 시점에 나눠서 알려준다 — 기획 지침)
    ///
    /// 각 팁은 Save 플래그로 1회만 표시된다. 튜토리얼 진행 중에는 조용히 있는다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TutorialTips : MonoBehaviour
    {
        [Header("표시")]
        [SerializeField, Tooltip("팁 텍스트. 비워두면 아무것도 하지 않는다 (null-safe)")]
        Text tipText;

        [SerializeField, Min(1f), Tooltip("표시 유지 시간(초)")]
        float holdDuration = 4f;

        [SerializeField, Min(0.05f), Tooltip("페이드아웃 시간(초)")]
        float fadeDuration = 0.6f;

        [Header("한 번만 보여줄 팁 (비우면 비활성)")]
        [SerializeField, TextArea] string comboTip =
            "카드의 총 반응이 양수면 COMBO가 이어집니다. 콤보가 높을수록 점수 배율도 올라갑니다.";
        [SerializeField, TextArea] string specialAudienceTip =
            "특별 관객 등장!\n요청 아이콘과 같은 SPECIAL 카드를 직접 전달하세요.";
        [SerializeField, TextArea] string crisisTip =
            "관객 이탈 위기! 경고가 끝나기 전에 높은 호응을 만들어 이탈 인원을 줄이세요.";
        [SerializeField, TextArea] string utilityTip =
            "DRAW는 카드를 보충하고, REROLL은 손패를 교체합니다.\n막힌 손패를 바꿀 때 사용하세요.";

        float _visibleUntil;
        float _hiddenAt;
        bool _animating;

        void Awake()
        {
            ConfigureTipLayout();
        }

        void OnEnable()
        {
            EventBus.Subscribe<ComboChanged>(OnComboChanged);
            EventBus.Subscribe<SpecialAudienceSpawned>(OnSpecialSpawned);
            EventBus.Subscribe<AudienceCrisisWarningStarted>(OnCrisisWarning);
            EventBus.Subscribe<CardResolved>(OnCardResolved);
            SetAlpha(0f);
        }

        void OnDisable()
        {
            EventBus.Unsubscribe<ComboChanged>(OnComboChanged);
            EventBus.Unsubscribe<SpecialAudienceSpawned>(OnSpecialSpawned);
            EventBus.Unsubscribe<AudienceCrisisWarningStarted>(OnCrisisWarning);
            EventBus.Unsubscribe<CardResolved>(OnCardResolved);
        }

        void Update()
        {
            if (!_animating) return;

            float now = Time.unscaledTime;
            if (now < _visibleUntil) return;
            if (now >= _hiddenAt) { SetAlpha(0f); _animating = false; return; }
            SetAlpha((_hiddenAt - now) / Mathf.Max(0.05f, fadeDuration));
        }

        // ---------------- 트리거 ----------------

        void OnComboChanged(ComboChanged e)
        {
            if (e.CurrentCombo >= 2) ShowOnce("tip_combo", comboTip);
        }

        void OnSpecialSpawned(SpecialAudienceSpawned e) => ShowOnce("tip_special", specialAudienceTip);

        void OnCrisisWarning(AudienceCrisisWarningStarted e) => ShowOnce("tip_crisis", crisisTip);

        void OnCardResolved(CardResolved e)
        {
            if (e.Role == CardRole.Utility) ShowOnce("tip_utility", utilityTip);
        }

        // ---------------- 표시 ----------------

        void ShowOnce(string flagKey, string message)
        {
            if (TutorialFlow.IsRunning) return;          // 튜토리얼 중에는 겹치지 않는다
            if (tipText == null || string.IsNullOrEmpty(message)) return;
            if (Save.GetBool(flagKey, false)) return;    // 이미 봤다

            Save.SetBool(flagKey, true);
            tipText.text = message;
            SetAlpha(1f);
            _visibleUntil = Time.unscaledTime + holdDuration;
            _hiddenAt = _visibleUntil + fadeDuration;
            _animating = true;
        }

        void SetAlpha(float alpha)
        {
            if (tipText == null) return;
            var c = tipText.color;
            c.a = alpha;
            tipText.color = c;
        }

        void ConfigureTipLayout()
        {
            if (tipText == null) return;

            RectTransform rect = tipText.rectTransform;
            rect.sizeDelta = new Vector2(1100f, 90f);

            tipText.fontSize = 26;
            tipText.resizeTextForBestFit = true;
            tipText.resizeTextMinSize = 22;
            tipText.resizeTextMaxSize = 28;
            tipText.alignment = TextAnchor.MiddleCenter;
            tipText.horizontalOverflow = HorizontalWrapMode.Wrap;
            tipText.verticalOverflow = VerticalWrapMode.Overflow;
            tipText.lineSpacing = 0.9f;
        }

        [ContextMenu("Debug/Reset All Tip Flags")]
        void DebugResetFlags()
        {
            Save.SetBool("tip_combo", false);
            Save.SetBool("tip_special", false);
            Save.SetBool("tip_crisis", false);
            Save.SetBool("tip_utility", false);
            Debug.Log("[Tips] 팁 표시 기록을 전부 지웠습니다.");
        }
    }
}
