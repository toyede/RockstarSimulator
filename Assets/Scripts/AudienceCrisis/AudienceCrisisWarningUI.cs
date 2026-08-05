using System.Collections;
using GameJamKit;
using UnityEngine;
using UnityEngine.UI;

namespace ContextStage
{
    [DisallowMultipleComponent]
    public sealed class AudienceCrisisWarningUI : MonoBehaviour
    {
        [SerializeField] GameObject overlayRoot;
        [SerializeField] Text titleText;
        [SerializeField] Text descriptionText;
        [SerializeField] Text countdownText;
        [SerializeField] Text progressText;
        [SerializeField, Min(0f)] float resultHoldDuration = 1.35f;

        [SerializeField, Range(0f, 1f)] float initialDimAlpha = 0.08f;
        [SerializeField, Range(0f, 1f)] float activeDimAlpha = 0.02f;
        [SerializeField, Min(0f)] float initialWarningHold = 0.5f;
        [SerializeField, Min(0.01f)] float dimFadeDuration = 0.35f;

        Coroutine _hideRoutine;
        Coroutine _dimRoutine;
        Image _dimImage;
        Image _missionPanel;
        Outline _missionOutline;

        void OnEnable()
        {
            EnsurePresentation();
            EventBus.Subscribe<AudienceCrisisWarningStarted>(OnWarningStarted);
            EventBus.Subscribe<AudienceCrisisProgressChanged>(OnProgressChanged);
            EventBus.Subscribe<AudienceCrisisDepartureStarted>(OnDepartureStarted);
            EventBus.Subscribe<AudienceCrisisResolved>(OnResolved);
            EventBus.Subscribe<AudienceCrisisCancelled>(OnCancelled);
            EventBus.Subscribe<AudienceComebackStarted>(OnComebackStarted);
            EventBus.Subscribe<AudienceComebackResolved>(OnComebackResolved);
            HideImmediate();
        }

        void OnDisable()
        {
            EventBus.Unsubscribe<AudienceCrisisWarningStarted>(OnWarningStarted);
            EventBus.Unsubscribe<AudienceCrisisProgressChanged>(OnProgressChanged);
            EventBus.Unsubscribe<AudienceCrisisDepartureStarted>(OnDepartureStarted);
            EventBus.Unsubscribe<AudienceCrisisResolved>(OnResolved);
            EventBus.Unsubscribe<AudienceCrisisCancelled>(OnCancelled);
            EventBus.Unsubscribe<AudienceComebackStarted>(OnComebackStarted);
            EventBus.Unsubscribe<AudienceComebackResolved>(OnComebackResolved);
            if (_hideRoutine != null) StopCoroutine(_hideRoutine);
            _hideRoutine = null;
            if (_dimRoutine != null) StopCoroutine(_dimRoutine);
            _dimRoutine = null;
        }

        void OnWarningStarted(AudienceCrisisWarningStarted e)
        {
            if (!HasRequiredReferences()) return;
            if (_hideRoutine != null)
            {
                StopCoroutine(_hideRoutine);
                _hideRoutine = null;
            }

            ShowOverlay(new Color32(0xEA, 0x4F, 0x36, 0x18));
            titleText.text = "긴급 상황!";
            descriptionText.text =
                "옆동네 인기 밴드의 공연이 곧 시작됩니다\n" +
                "경고 표시가 뜬 관객에게 취향에 맞는 카드를 사용해 " +
                $"호응도를 {Mathf.CeilToInt(e.RetentionEngagement)} 이상으로 올리세요.\n";
            countdownText.text = $"관객 이탈까지 {e.Duration:0.0}초";
            progressText.text = $"붙잡은 관객 0 / {e.ThreatenedAudience.Length}";
        }

        void OnProgressChanged(AudienceCrisisProgressChanged e)
        {
            if (!HasRequiredReferences() || !overlayRoot.activeSelf) return;

            countdownText.text = $"관객 이탈까지 {e.Remaining:0.0}초";
            progressText.text =
                $"붙잡은 관객 {e.SecuredCount} / {e.ThreatenedCount}" +
                (e.SpecialSaveCount > 0 ? $"    ·    스페셜 구조 +{e.SpecialSaveCount}" : string.Empty);
        }

        void OnDepartureStarted(AudienceCrisisDepartureStarted e)
        {
            if (!HasRequiredReferences() || !overlayRoot.activeSelf) return;

            countdownText.text = string.Empty;
            titleText.text = e.DepartureCount > 0 ? "관객들이 떠나고 있습니다!" : "관객들이 남았습니다!";
        }

        void OnResolved(AudienceCrisisResolved e)
        {
            if (!HasRequiredReferences()) return;

            ShowResultOverlay(
                e.DepartedCount == 0
                    ? new Color32(0x30, 0xE1, 0xB9, 0x10)
                    : new Color32(0xF5, 0x7D, 0x4A, 0x10));
            titleText.text = e.DepartedCount == 0 ? "관객을 모두 붙잡았습니다!" : $"관객 {e.DepartedCount}명이 떠났습니다";
            descriptionText.text = e.DepartedCount == 0 ? "위기에 흔들리지 않고 공연을 이어갑니다." : "남은 관객의 호응을 다시 끌어올리세요.";
            countdownText.text = string.Empty;
            progressText.text = $"붙잡은 관객 {e.RetainedCount} / {e.ThreatenedCount}";

            if (_hideRoutine != null) StopCoroutine(_hideRoutine);
            _hideRoutine = StartCoroutine(HideAfterDelay());
        }

        IEnumerator HideAfterDelay()
        {
            if (resultHoldDuration > 0f)
                yield return new WaitForSeconds(resultHoldDuration);
            HideImmediate();
            _hideRoutine = null;
        }

        void OnCancelled(AudienceCrisisCancelled e) => HideImmediate();

        void OnComebackStarted(AudienceComebackStarted e)
        {
            if (!HasRequiredReferences()) return;
            if (_hideRoutine != null)
            {
                StopCoroutine(_hideRoutine);
                _hideRoutine = null;
            }

            ShowOverlay(new Color32(0x30, 0xE1, 0xB9, 0x10), initialDimAlpha, activeDimAlpha);
            titleText.text = "관객 지원 도착!";
            descriptionText.text =
                "공연 소문을 듣고 힙스터 팬들이 찾아왔습니다.\n" +
                "잠시 후 새로운 관객이 합류하고 전체 호응도가 상승합니다.";
            countdownText.text = "자동 지원";
            progressText.text = "새 관객 합류 · 전체 호응도 상승";
        }

        void OnComebackResolved(AudienceComebackResolved e)
        {
            if (!HasRequiredReferences()) return;

            ShowResultOverlay(new Color32(0x30, 0xE1, 0xB9, 0x10));
            titleText.text = "힙스터 팬 합류!";
            descriptionText.text = $"새 관객 {e.JoinedCount}명이 입장했고 공연장의 호응이 회복됐습니다.";
            countdownText.text = string.Empty;
            progressText.text = $"관객 {e.BoostedCount}명의 호응도 상승";

            if (_hideRoutine != null) StopCoroutine(_hideRoutine);
            _hideRoutine = StartCoroutine(HideAfterDelay());
        }

        void HideImmediate()
        {
            if (_dimRoutine != null)
            {
                StopCoroutine(_dimRoutine);
                _dimRoutine = null;
            }
            if (overlayRoot != null)
                overlayRoot.SetActive(false);
        }

        void ShowOverlay(Color accent) => ShowOverlay(accent, initialDimAlpha, activeDimAlpha);

        void ShowOverlay(Color accent, float fromAlpha, float toAlpha)
        {
            if (overlayRoot == null) return;
            EnsurePresentation();
            overlayRoot.SetActive(true);
            ApplyAccent(accent);
            SetDimAlpha(fromAlpha);

            if (_dimRoutine != null) StopCoroutine(_dimRoutine);
            _dimRoutine = StartCoroutine(FadeDim(fromAlpha, toAlpha));
        }

        void ShowResultOverlay(Color accent)
        {
            if (overlayRoot == null) return;
            EnsurePresentation();
            overlayRoot.SetActive(true);
            ApplyAccent(accent);
            SetDimAlpha(activeDimAlpha);
            if (_dimRoutine != null)
            {
                StopCoroutine(_dimRoutine);
                _dimRoutine = null;
            }
        }

        IEnumerator FadeDim(float fromAlpha, float toAlpha)
        {
            if (initialWarningHold > 0f)
                yield return new WaitForSecondsRealtime(initialWarningHold);

            float elapsed = 0f;
            while (elapsed < dimFadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                SetDimAlpha(Mathf.Lerp(fromAlpha, toAlpha, Mathf.Clamp01(elapsed / dimFadeDuration)));
                yield return null;
            }

            SetDimAlpha(toAlpha);
            _dimRoutine = null;
        }

        void EnsurePresentation()
        {
            if (overlayRoot == null) return;
            if (_dimImage == null)
                _dimImage = overlayRoot.GetComponent<Image>();

            Transform existing = overlayRoot.transform.Find("MissionPanel");
            if (existing != null)
            {
                _missionPanel = existing.GetComponent<Image>();
                _missionOutline = existing.GetComponent<Outline>();
            }
            else
            {
                var panelObject = new GameObject(
                    "MissionPanel",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image),
                    typeof(Outline));
                RectTransform panelRect = (RectTransform)panelObject.transform;
                panelRect.SetParent(overlayRoot.transform, false);

                // 화면 왼쪽 중앙 기준 (Anchor: Left-Center)
                panelRect.anchorMin = new Vector2(0f, 0.5f);
                panelRect.anchorMax = new Vector2(0f, 0.5f);
                panelRect.pivot = new Vector2(0f, 0.5f);

                // 왼쪽 여백 40px, 패널 크기 (520 x 380)
                panelRect.anchoredPosition = new Vector2(40f, 0f);
                panelRect.sizeDelta = new Vector2(520f, 380f);
                panelRect.SetAsFirstSibling();

                _missionPanel = panelObject.GetComponent<Image>();
                _missionPanel.color = new Color(0.04f, 0.03f, 0.06f, 0.70f);
                _missionPanel.raycastTarget = false;
                _missionOutline = panelObject.GetComponent<Outline>();
                _missionOutline.effectDistance = new Vector2(2f, -2f);
                _missionOutline.useGraphicAlpha = true;
            }

            // 설명 폰트 크기 및 높이 확대 적용 (descriptionText: 22 -> 25)
            RectTransform target = _missionPanel.rectTransform;
            ConfigureText(titleText, target, new Vector2(0f, 130f), new Vector2(480f, 50f), 36, false);
            ConfigureText(descriptionText, target, new Vector2(0f, 28f), new Vector2(480f, 130f), 25, true, 18);
            ConfigureText(countdownText, target, new Vector2(0f, -70f), new Vector2(480f, 45f), 32, false);
            ConfigureText(progressText, target, new Vector2(0f, -130f), new Vector2(480f, 40f), 22, false);
        }

        static void ConfigureText(
            Text text,
            RectTransform parent,
            Vector2 position,
            Vector2 size,
            int fontSize,
            bool bestFit,
            int minFontSize = 16)
        {
            if (text == null || parent == null) return;
            RectTransform rect = text.rectTransform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            text.alignment = TextAnchor.MiddleCenter;
            text.fontSize = fontSize;
            text.resizeTextForBestFit = bestFit;
            if (bestFit)
            {
                text.resizeTextMinSize = minFontSize;
                text.resizeTextMaxSize = fontSize;
            }
        }

        void ApplyAccent(Color accent)
        {
            if (_missionOutline != null)
            {
                Color outlineColor = accent;
                outlineColor.a *= 0.3f;
                _missionOutline.effectColor = outlineColor;
            }
            if (titleText != null) titleText.color = accent;
            if (countdownText != null) countdownText.color = accent;
        }

        void SetDimAlpha(float alpha)
        {
            if (_dimImage == null) return;
            Color color = _dimImage.color;
            color.a = Mathf.Clamp01(alpha);
            _dimImage.color = color;
        }

        bool HasRequiredReferences()
        {
            return overlayRoot != null &&
                   titleText != null &&
                   descriptionText != null &&
                   countdownText != null &&
                   progressText != null;
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            resultHoldDuration = Mathf.Max(0f, resultHoldDuration);
            initialDimAlpha = Mathf.Clamp01(initialDimAlpha);
            activeDimAlpha = Mathf.Clamp01(activeDimAlpha);
            initialWarningHold = Mathf.Max(0f, initialWarningHold);
            dimFadeDuration = Mathf.Max(0.01f, dimFadeDuration);
        }
#endif
    }
}