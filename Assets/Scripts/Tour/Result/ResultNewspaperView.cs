using System;
using System.Collections;
using GameJamKit;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ContextStage
{
    /// <summary>
    /// 결과 화면 (신문). 받은 ResultPresentation 을 표시하고 다음 진행 요청만 전달한다.
    /// 씬 오브젝트는 Tools/Tour/Setup Result Newspaper 가 만들고 여기 필드에 묶는다 — 위치·크기는 씬에서 고친다.
    ///
    /// 등장 연출(약 1.5초): 배경 딤 → 신문이 아래에서 날아와 살짝 기울어져 정착 → 헤드라인·사진 → 도장 '쿵' → 점수·기록·버튼.
    /// 클릭하면 연출을 즉시 완료하고, 그 클릭은 다음 화면으로 넘기지 않는다. 읽는 동안 자동으로 넘어가지 않는다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ResultNewspaperView : MonoBehaviour
    {
        [Header("루트")]
        [SerializeField] GameObject root;
        [SerializeField] CanvasGroup backdrop;
        [SerializeField] Button skipCatcher;
        [SerializeField] RectTransform paper;
        [SerializeField] Image paperImage;

        [Header("제호 · 헤드라인")]
        [SerializeField] TMP_Text mastheadInfo;
        [SerializeField] CanvasGroup headlineGroup;
        [SerializeField] TMP_Text headline;
        [SerializeField] TMP_Text subtitle;

        [Header("사진")]
        [SerializeField] CanvasGroup photoGroup;
        [SerializeField] Image photoBackground;
        [SerializeField] Image bandImage;
        [SerializeField] TMP_Text caption;

        [Header("성적")]
        [SerializeField] CanvasGroup verdictGroup;
        [SerializeField] RectTransform stamp;
        [SerializeField] TMP_Text stampText;
        [SerializeField] Image[] stampFrame;
        [SerializeField] Image rankImage;
        [SerializeField] TMP_Text scoreText;
        [SerializeField] TMP_Text targetText;
        [SerializeField] TMP_Text ratioText;
        [SerializeField, Tooltip("등급을 숨길 때(보스전) 점수 블록을 왼쪽으로 당기는 거리")] float scoreShiftWithoutRank = 110f;

        [Header("기록 · 기사 · 버튼")]
        [SerializeField] CanvasGroup recordsGroup;
        [SerializeField] TMP_Text statCombo;
        [SerializeField] TMP_Text statFever;
        [SerializeField] TMP_Text statAudience;
        [SerializeField] TMP_Text articleTitle;
        [SerializeField] TMP_Text articleBody;
        [SerializeField] Button primaryButton;
        [SerializeField] TMP_Text primaryLabel;

        [Header("연출")]
        [SerializeField, Min(0f)] float backdropAlpha = 0.72f;
        [SerializeField, Min(0f)] float paperFlyDuration = 0.5f;
        [SerializeField] float paperStartOffsetY = -1200f;
        [SerializeField] float paperStartTilt = -7f;
        [SerializeField] float paperRestTilt = -1.5f;
        [SerializeField, Min(0f)] float headlineAt = 0.6f;
        [SerializeField, Min(0f)] float stampAt = 0.9f;
        [SerializeField, Min(0f)] float stampPunchDuration = 0.15f;
        [SerializeField, Min(1f)] float stampPunchScale = 1.8f;
        [SerializeField] float stampTilt = -8f;
        [SerializeField, Min(0f)] float recordsAt = 1.2f;
        [SerializeField, Min(0f)] float fadeDuration = 0.2f;
        [SerializeField, Tooltip("도장 찍힐 때 재생 (비우면 없음)")] string stampSoundId = "";

        Coroutine _routine;
        Action _onPrimary;
        bool _complete;
        Vector2[] _scoreBasePositions;

        public bool IsShowing => root != null && root.activeSelf;
        public bool IsAnimating => _routine != null;

        void Awake()
        {
            if (skipCatcher != null) skipCatcher.onClick.AddListener(CompleteImmediately);
            if (primaryButton != null) primaryButton.onClick.AddListener(OnPrimaryClicked);
            if (root != null) root.SetActive(false);
        }

        /// <summary>내용을 채우고 등장 연출을 시작한다. onPrimary 는 다음 진행 버튼에서만 불린다.</summary>
        public void Show(ResultPresentation data, Action onPrimary)
        {
            if (data == null || root == null) return;
            _onPrimary = onPrimary;
            Fill(data);

            if (_routine != null) StopCoroutine(_routine);
            root.SetActive(true);
            _complete = false;
            _routine = StartCoroutine(Entrance());
        }

        public void Hide()
        {
            if (_routine != null)
            {
                StopCoroutine(_routine);
                _routine = null;
            }
            _onPrimary = null;
            if (root != null) root.SetActive(false);
        }

        // ---------------- 내용 ----------------

        void Fill(ResultPresentation d)
        {
            if (paperImage != null && d.skin != null && d.skin.paper != null)
            {
                paperImage.sprite = d.skin.paper;
                paperImage.SetNativeSize();
            }

            Set(mastheadInfo, d.mastheadInfo);
            Set(headline, d.headline);
            Set(subtitle, d.subtitle);
            Set(caption, d.caption);

            if (photoBackground != null)
            {
                photoBackground.sprite = d.photoBackground;
                photoBackground.enabled = d.photoBackground != null;
                photoBackground.color = d.success ? Color.white : new Color(0.75f, 0.75f, 0.8f, 1f);
            }
            if (bandImage != null)
            {
                bandImage.sprite = d.bandSprite;
                bandImage.enabled = d.bandSprite != null;
                if (d.bandSprite != null)
                {
                    bandImage.SetNativeSize();
                    bandImage.rectTransform.sizeDelta *= d.bandSpriteScale;
                }
            }

            Set(stampText, d.stampText);
            if (stampText != null) stampText.color = d.stampColor;
            if (stampFrame != null)
                for (int i = 0; i < stampFrame.Length; i++)
                    if (stampFrame[i] != null) stampFrame[i].color = d.stampColor;

            if (rankImage != null)
            {
                rankImage.sprite = d.rankIcon;
                rankImage.gameObject.SetActive(d.showRank && d.rankIcon != null);
            }
            Set(scoreText, $"{d.score:N0}점");
            Set(targetText, $"/ 목표 {d.targetScore:N0}점");
            Set(ratioText, $"목표 달성률 {d.achievedPercent}%");
            ShiftScoreBlock(d.showRank && d.rankIcon != null ? 0f : -scoreShiftWithoutRank);

            Set(statCombo, d.statCombo);
            Set(statFever, d.statFever);
            Set(statAudience, d.statAudience);
            Set(articleTitle, d.articleTitle);
            Set(articleBody, string.IsNullOrEmpty(d.outcomeLine) ? d.articleBody : $"{d.articleBody}\n{d.outcomeLine}");
            Set(primaryLabel, d.buttonLabel);
        }

        static void Set(TMP_Text text, string value)
        {
            if (text != null) text.text = value ?? "";
        }

        /// <summary>등급 아이콘이 없을 때 점수 블록이 빈자리를 남기지 않도록 옮긴다. 씬의 원래 위치는 첫 호출 때 기억한다.</summary>
        void ShiftScoreBlock(float offsetX)
        {
            TMP_Text[] block = { scoreText, targetText, ratioText };
            if (_scoreBasePositions == null)
            {
                _scoreBasePositions = new Vector2[block.Length];
                for (int i = 0; i < block.Length; i++)
                    _scoreBasePositions[i] = block[i] != null ? block[i].rectTransform.anchoredPosition : Vector2.zero;
            }
            for (int i = 0; i < block.Length; i++)
                if (block[i] != null) block[i].rectTransform.anchoredPosition = _scoreBasePositions[i] + new Vector2(offsetX, 0f);
        }

        // ---------------- 연출 ----------------

        IEnumerator Entrance()
        {
            SetGroup(headlineGroup, 0f);
            SetGroup(photoGroup, 0f);
            SetGroup(verdictGroup, 0f);
            SetGroup(recordsGroup, 0f);
            if (backdrop != null) backdrop.alpha = 0f;
            if (primaryButton != null) primaryButton.interactable = false;
            if (skipCatcher != null) skipCatcher.gameObject.SetActive(true);
            if (paper != null)
            {
                paper.anchoredPosition = new Vector2(0f, paperStartOffsetY);
                paper.localRotation = Quaternion.Euler(0f, 0f, paperStartTilt);
            }
            if (stamp != null) stamp.localScale = Vector3.one * stampPunchScale;

            float t = 0f;
            bool headlineShown = false, stampShown = false, recordsShown = false;
            float stampStarted = -1f;
            while (true)
            {
                t += Time.unscaledDeltaTime;

                if (backdrop != null) backdrop.alpha = backdropAlpha * Mathf.Clamp01(t / 0.25f);
                if (paper != null)
                {
                    float f = paperFlyDuration <= 0f ? 1f : Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / paperFlyDuration));
                    paper.anchoredPosition = new Vector2(0f, Mathf.Lerp(paperStartOffsetY, 0f, f));
                    paper.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(paperStartTilt, paperRestTilt, f));
                }

                if (!headlineShown && t >= headlineAt)
                {
                    headlineShown = true;
                    StartCoroutine(FadeGroup(headlineGroup, 1f, fadeDuration));
                    StartCoroutine(FadeGroup(photoGroup, 1f, fadeDuration));
                }
                if (!stampShown && t >= stampAt)
                {
                    stampShown = true;
                    stampStarted = t;
                    SetGroup(verdictGroup, 1f);
                    if (!string.IsNullOrEmpty(stampSoundId)) Sound.Play(stampSoundId);
                }
                if (stampShown && stamp != null)
                {
                    float s = stampPunchDuration <= 0f ? 1f : Mathf.Clamp01((t - stampStarted) / stampPunchDuration);
                    stamp.localScale = Vector3.one * Mathf.Lerp(stampPunchScale, 1f, s * s);
                    stamp.localRotation = Quaternion.Euler(0f, 0f, stampTilt);
                }
                if (!recordsShown && t >= recordsAt)
                {
                    recordsShown = true;
                    StartCoroutine(FadeGroup(recordsGroup, 1f, fadeDuration));
                }

                bool done = headlineShown && stampShown && recordsShown &&
                            t >= recordsAt + fadeDuration && t >= paperFlyDuration &&
                            t >= stampAt + stampPunchDuration;
                if (done) break;
                yield return null;
            }

            _routine = null;
            ApplyFinalState();
        }

        IEnumerator FadeGroup(CanvasGroup group, float target, float duration)
        {
            if (group == null) yield break;
            float from = group.alpha;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                group.alpha = Mathf.Lerp(from, target, duration <= 0f ? 1f : Mathf.Clamp01(elapsed / duration));
                yield return null;
            }
            group.alpha = target;
        }

        static void SetGroup(CanvasGroup group, float alpha)
        {
            if (group != null) group.alpha = alpha;
        }

        /// <summary>클릭·터치로 연출을 즉시 완료한다. 이 입력은 다음 화면으로 넘기지 않는다.</summary>
        public void CompleteImmediately()
        {
            if (_complete) return;
            if (_routine != null)
            {
                StopCoroutine(_routine);
                _routine = null;
            }
            StopAllCoroutines();
            ApplyFinalState();
        }

        void ApplyFinalState()
        {
            _complete = true;
            if (backdrop != null) backdrop.alpha = backdropAlpha;
            if (paper != null)
            {
                paper.anchoredPosition = Vector2.zero;
                paper.localRotation = Quaternion.Euler(0f, 0f, paperRestTilt);
            }
            SetGroup(headlineGroup, 1f);
            SetGroup(photoGroup, 1f);
            SetGroup(verdictGroup, 1f);
            SetGroup(recordsGroup, 1f);
            if (stamp != null)
            {
                stamp.localScale = Vector3.one;
                stamp.localRotation = Quaternion.Euler(0f, 0f, stampTilt);
            }
            if (skipCatcher != null) skipCatcher.gameObject.SetActive(false);
            if (primaryButton != null) primaryButton.interactable = true;
        }

        void OnPrimaryClicked()
        {
            if (!_complete) return;
            _onPrimary?.Invoke();
        }
    }
}
