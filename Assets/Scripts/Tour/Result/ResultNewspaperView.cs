using System;
using System.Collections;
using System.Collections.Generic;
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
        [SerializeField, Tooltip("기존 지면의 2줄 기사 제목. 한 줄 기사 레이아웃에서는 숨긴다.")] TMP_Text articleTitle;
        [SerializeField] TMP_Text articleBody;
        [SerializeField] Button primaryButton;
        [SerializeField] TMP_Text primaryLabel;

        [Header("연출")]
        [SerializeField, Min(0f)] float backdropAlpha = 0.72f;
        [SerializeField, Min(0f)] float paperFlyDuration = 0.5f;
        [SerializeField] float paperStartOffsetY = -1200f;
        [SerializeField] float paperStartTilt = -7f;
        [SerializeField] float paperRestTilt = 0f;
        [SerializeField, Min(0f)] float headlineAt = 0.6f;
        [SerializeField, Min(0f)] float stampAt = 0.9f;
        [SerializeField, Min(0f)] float stampPunchDuration = 0.15f;
        [SerializeField, Min(1f)] float stampPunchScale = 1.8f;
        [SerializeField] float stampTilt = -8f;
        [SerializeField, Min(0f)] float recordsAt = 1.2f;
        [SerializeField, Min(0f)] float fadeDuration = 0.2f;
        [SerializeField, Tooltip("도장 찍힐 때 재생 (비우면 없음)")] string stampSoundId = "";

        Coroutine _routine;
        // 스킨별 위치 보정의 기준(씬에서 잡은 위치). 첫 표시 때 한 번 저장한다
        bool _baseCached;
        Vector2 _headlineBase;
        Vector2 _recordsBase;
        Coroutine _bandRoutine;
        Action _onPrimary;
        bool _complete;
        float _stampRestScale = 1f;
        CanvasGroup _stampInkGroup;
        ResultRankInk _rankInk;
        Vector2[] _scoreBasePositions;
        const float RankInkDuration = .46f;
        readonly Vector2[] _recordBasePositions = new Vector2[4];
        readonly Vector2[] _recordBaseSizes = new Vector2[4];
        bool _recordLayoutCached;

        public bool IsShowing => root != null && root.activeSelf;
        public bool IsAnimating => _routine != null;

        void Awake()
        {
            if (stamp != null)
            {
                _stampInkGroup = stamp.GetComponent<CanvasGroup>();
                if (_stampInkGroup == null) _stampInkGroup = stamp.gameObject.AddComponent<CanvasGroup>();
                _stampInkGroup.blocksRaycasts = false;
                _stampInkGroup.interactable = false;
                stamp.SetAsLastSibling();
                stamp.anchoredPosition += Vector2.up * 27f;
            }
            if (rankImage != null)
            {
                rankImage.rectTransform.sizeDelta *= .78f;
                rankImage.rectTransform.anchoredPosition += Vector2.up * 24f;
                _rankInk = rankImage.gameObject.AddComponent<ResultRankInk>();
            }
            UIInteractionSfx.Ensure(skipCatcher, hover: false);
            UIInteractionSfx.Ensure(primaryButton);
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
            if (_bandRoutine != null) StopCoroutine(_bandRoutine);
            root.SetActive(true);
            _complete = false;
            _routine = StartCoroutine(Entrance());
            if (bandImage != null && data.bandFrames != null && data.bandFrames.Count > 1)
                _bandRoutine = StartCoroutine(AnimateBand(data.bandFrames, data.bandFrameInterval));
        }

        public void Hide()
        {
            if (_routine != null)
            {
                StopCoroutine(_routine);
                _routine = null;
            }
            if (_bandRoutine != null)
            {
                StopCoroutine(_bandRoutine);
                _bandRoutine = null;
            }
            _onPrimary = null;
            if (root != null) root.SetActive(false);
        }

        /// <summary>밴드 포즈 프레임을 교대로 보여준다 (2프레임 애니메이션).</summary>
        IEnumerator AnimateBand(IReadOnlyList<Sprite> frames, float interval)
        {
            int index = 0;
            while (true)
            {
                float elapsed = 0f;
                while (elapsed < interval)
                {
                    elapsed += Time.unscaledDeltaTime;
                    yield return null;
                }
                index = (index + 1) % frames.Count;
                if (frames[index] != null) bandImage.sprite = frames[index];
            }
        }

        // ---------------- 내용 ----------------

        /// <summary>지면(스킨)마다 제호 높이·칸 선 위치가 달라 헤드라인 묶음과 기록 묶음만 스킨 값만큼 옮긴다. 기준은 씬 위치.</summary>
        void ApplySkinOffsets(ResultNewspaperCatalog.Skin skin)
        {
            RectTransform headlineRect = headlineGroup != null ? headlineGroup.GetComponent<RectTransform>() : null;
            RectTransform recordsRect = recordsGroup != null ? recordsGroup.GetComponent<RectTransform>() : null;
            if (!_baseCached)
            {
                if (headlineRect != null) _headlineBase = headlineRect.anchoredPosition;
                if (recordsRect != null) _recordsBase = recordsRect.anchoredPosition;
                _baseCached = true;
            }
            Vector2 headlineOffset = skin != null ? skin.headlineOffset : Vector2.zero;
            Vector2 recordsOffset = skin != null ? skin.recordsOffset : Vector2.zero;
            if (headlineRect != null) headlineRect.anchoredPosition = _headlineBase + headlineOffset;
            if (recordsRect != null) recordsRect.anchoredPosition = _recordsBase + recordsOffset;
        }

        void Fill(ResultPresentation d)
        {
            if (paperImage != null && d.skin != null && d.skin.paper != null)
            {
                paperImage.sprite = d.skin.paper;
                paperImage.SetNativeSize();
            }

            ApplySkinOffsets(d.skin);

            Set(mastheadInfo, d.mastheadInfo);
            Set(headline, d.headline);
            // 보스 판정은 부제에서 명확히 표시하고, 하단 통계 기사와 공간을 분리한다.
            Set(subtitle, string.IsNullOrEmpty(d.outcomeLine) ? d.subtitle : $"{d.subtitle} · {d.outcomeLine}");
            Set(caption, d.caption);

            if (photoBackground != null)
            {
                photoBackground.sprite = d.photoBackground;
                photoBackground.enabled = d.photoBackground != null;
                photoBackground.color = d.success ? Color.white : new Color(0.75f, 0.75f, 0.8f, 1f);
            }
            if (bandImage != null)
            {
                Sprite first = d.bandFrames != null && d.bandFrames.Count > 0 ? d.bandFrames[0] : null;
                bandImage.sprite = first;
                bandImage.enabled = first != null;
                if (first != null)
                {
                    bandImage.SetNativeSize();
                    bandImage.rectTransform.sizeDelta *= d.bandSpriteScale;
                }
            }

            _stampRestScale = d.isBoss ? 1.12f : 1.28f;
            Set(stampText, d.stampText);
            float neutral = d.stampColor.grayscale;
            Color ink = Color.Lerp(d.stampColor, new Color(neutral, neutral, neutral, d.stampColor.a), .32f);
            ink.a *= .60f;
            if (stampText != null) stampText.color = ink;
            if (stampFrame != null)
                for (int i = 0; i < stampFrame.Length; i++)
                    if (stampFrame[i] != null) stampFrame[i].color = ink;

            bool hasRank = d.showRank && d.rankIcon != null;
            if (rankImage != null)
            {
                rankImage.sprite = d.rankIcon;
                rankImage.gameObject.SetActive(hasRank);
                if (hasRank) _rankInk?.Begin(d.rankLabel);
            }
            Set(scoreText, GameLanguage.Pick($"{d.score:N0}점", $"{d.score:N0} pts"));
            Set(targetText, GameLanguage.Pick($"/ 목표 {d.targetScore:N0}점", $"/ Goal: {d.targetScore:N0} pts"));
            Set(ratioText, GameLanguage.Pick($"목표 달성률 {d.achievedPercent}%", $"Goal reached: {d.achievedPercent}%"));
            ShiftScoreBlock(rankImage != null && hasRank);

            ApplyRecordLayout(d.skin);
            Set(statCombo, d.statCombo);
            Set(statFever, d.statFever);
            Set(statAudience, d.statAudience);
            if (articleTitle != null) articleTitle.gameObject.SetActive(false);
            Set(articleBody, d.articleBody);
            Set(primaryLabel, d.buttonLabel);
            FitTypography();
        }

        void ApplyRecordLayout(ResultNewspaperCatalog.Skin skin)
        {
            TMP_Text[] fields = { statCombo, statFever, statAudience, articleBody };
            bool hasLayout = skin != null && skin.statLeftEdges != Vector3.zero;
            for (int i = 0; i < fields.Length; i++)
            {
                TMP_Text field = fields[i];
                if (field == null) continue;
                RectTransform rect = field.rectTransform;
                if (!_recordLayoutCached)
                {
                    _recordBasePositions[i] = rect.anchoredPosition;
                    _recordBaseSizes[i] = rect.sizeDelta;
                }
                field.alignment = TextAlignmentOptions.MidlineLeft;
                field.textWrappingMode = TextWrappingModes.NoWrap;
                field.overflowMode = TextOverflowModes.Ellipsis;
                if (!hasLayout)
                {
                    rect.sizeDelta = _recordBaseSizes[i];
                    rect.anchoredPosition = _recordBasePositions[i];
                    continue;
                }
                bool footer = i == 3;
                Vector2 size = footer ? new Vector2(850f, 34f) : new Vector2(194f, 72f);
                float left = footer ? skin.footerStart.x : skin.statLeftEdges[i];
                float centerY = footer ? skin.footerStart.y : skin.statCenterY;
                rect.sizeDelta = size;
                rect.anchoredPosition = new Vector2(left + size.x * rect.pivot.x, centerY + size.y * (rect.pivot.y - .5f));
            }
            _recordLayoutCached = true;
        }

        void FitTypography()
        {
            TMP_Text[] labels = { mastheadInfo, headline, subtitle, caption, stampText, scoreText, targetText,
                ratioText, statCombo, statFever, statAudience, articleBody, primaryLabel };
            foreach (TMP_Text label in labels)
            {
                if (label == null) continue;
                // 가짜 볼드/외곽선이 얇은 픽셀 획을 뭉개지 않게 한다.
                label.fontStyle &= ~FontStyles.Bold;
                label.outlineWidth = 0f;
                label.extraPadding = true;
                PixelTextFit.Apply(label);
            }
        }

        static void Set(TMP_Text text, string value)
        {
            if (text != null) text.text = value ?? "";
        }

        /// <summary>등급 아이콘이 없을 때 점수 블록이 빈자리를 남기지 않도록 옮긴다. 씬의 원래 위치는 첫 호출 때 기억한다.</summary>
        void ShiftScoreBlock(bool hasRank)
        {
            TMP_Text[] block = { scoreText, targetText, ratioText };
            if (_scoreBasePositions == null)
            {
                _scoreBasePositions = new Vector2[block.Length];
                for (int i = 0; i < block.Length; i++)
                {
                    if (block[i] == null) continue;
                    _scoreBasePositions[i] = block[i].rectTransform.anchoredPosition;
                    block[i].fontSizeMax *= i == 0 ? .82f : .90f;
                    block[i].fontSizeMin = Mathf.Min(block[i].fontSizeMin, block[i].fontSizeMax);
                }
            }
            for (int i = 0; i < block.Length; i++)
            {
                if (block[i] == null) continue;
                block[i].rectTransform.anchoredPosition = _scoreBasePositions[i] +
                    new Vector2(hasRank ? 0f : -scoreShiftWithoutRank, 29f);
                block[i].alignment = hasRank ? TextAlignmentOptions.MidlineLeft : TextAlignmentOptions.Midline;
            }
        }

        // ---------------- 연출 ----------------

        IEnumerator Entrance()
        {
            SetGroup(headlineGroup, 0f);
            SetGroup(photoGroup, 0f);
            SetGroup(verdictGroup, 0f);
            SetGroup(_stampInkGroup, 0f);
            SetGroup(recordsGroup, 0f);
            if (backdrop != null) backdrop.alpha = 0f;
            if (primaryButton != null) primaryButton.interactable = false;
            if (skipCatcher != null) skipCatcher.gameObject.SetActive(true);
            if (paper != null)
            {
                paper.anchoredPosition = new Vector2(0f, paperStartOffsetY);
                paper.localRotation = Quaternion.Euler(0f, 0f, paperStartTilt);
            }
            float stampStartScale = Mathf.Max(stampPunchScale, _stampRestScale);
            if (stamp != null) stamp.localScale = Vector3.one * stampStartScale;

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
                    StartCoroutine(FadeGroup(verdictGroup, 1f, fadeDuration));
                }
                if (!stampShown && t >= stampAt)
                {
                    stampShown = true;
                    stampStarted = t;
                    SetGroup(_stampInkGroup, 1f);
                    if (!string.IsNullOrEmpty(stampSoundId)) Sound.Play(stampSoundId);
                }
                if (headlineShown && _rankInk != null && rankImage.gameObject.activeSelf)
                    _rankInk.SetProgress((t - headlineAt) / RankInkDuration);
                if (stampShown && stamp != null)
                {
                    float s = stampPunchDuration <= 0f ? 1f : Mathf.Clamp01((t - stampStarted) / stampPunchDuration);
                    stamp.localScale = Vector3.one * Mathf.Lerp(stampStartScale, _stampRestScale, s * s);
                    stamp.localRotation = Quaternion.Euler(0f, 0f, stampTilt);
                }
                if (!recordsShown && t >= recordsAt)
                {
                    recordsShown = true;
                    StartCoroutine(FadeGroup(recordsGroup, 1f, fadeDuration));
                }

                bool done = headlineShown && stampShown && recordsShown &&
                            t >= recordsAt + fadeDuration && t >= paperFlyDuration &&
                            t >= stampAt + stampPunchDuration &&
                            t >= headlineAt + RankInkDuration;
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
            _rankInk?.SetProgress(1f);
            if (backdrop != null) backdrop.alpha = backdropAlpha;
            if (paper != null)
            {
                paper.anchoredPosition = Vector2.zero;
                paper.localRotation = Quaternion.Euler(0f, 0f, paperRestTilt);
            }
            SetGroup(headlineGroup, 1f);
            SetGroup(photoGroup, 1f);
            SetGroup(verdictGroup, 1f);
            SetGroup(_stampInkGroup, 1f);
            SetGroup(recordsGroup, 1f);
            if (stamp != null)
            {
                stamp.localScale = Vector3.one * _stampRestScale;
                stamp.localRotation = Quaternion.Euler(0f, 0f, stampTilt);
            }
            if (skipCatcher != null) skipCatcher.gameObject.SetActive(false);
            if (recordsGroup != null)
            {
                recordsGroup.blocksRaycasts = true;   // 버튼이 클릭을 받으려면 조상 그룹이 레이캐스트를 막지 않아야 한다
                recordsGroup.interactable = true;
            }
            if (primaryButton != null) primaryButton.interactable = true;
        }

        void OnPrimaryClicked()
        {
            if (!_complete) return;
            _onPrimary?.Invoke();
        }
    }
}
