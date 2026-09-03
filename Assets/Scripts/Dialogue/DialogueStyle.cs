using TMPro;
using UnityEngine;

namespace ContextStage
{
    /// <summary>
    /// 대화창 표현 수치 모음. 코드에 수치를 박지 않고 여기서 조절한다.
    /// (피그마 시안에 맞추는 작업은 이 에셋과 DialoguePanel 프리팹만 만지면 된다)
    /// </summary>
    [CreateAssetMenu(fileName = "DialogueStyle", menuName = "ContextStage/Dialogue/Style")]
    public sealed class DialogueStyle : ScriptableObject
    {
        [Header("폰트")]
        [SerializeField, Tooltip("본문·이름표 TMP 폰트. 비우면 TMP 기본 폰트(한글 미지원)")]
        TMP_FontAsset font;
        [SerializeField, Min(12f)] float bodyFontSize = 34f;
        [SerializeField, Min(12f)] float nameFontSize = 28f;
        [SerializeField, Min(12f)] float venueFontSize = 24f;
        [SerializeField, Min(12f)] float ruleTitleFontSize = 40f;
        [SerializeField, Min(12f)] float ruleBodyFontSize = 28f;

        [Header("색")]
        [SerializeField] Color boxColor = new Color32(0x2E, 0x22, 0x2F, 0xF4);
        [SerializeField] Color boxBorderColor = new Color32(0xF9, 0xC2, 0x2B, 0xFF);
        [SerializeField] Color nameTagColor = new Color32(0x0B, 0x8A, 0x8F, 0xFF);
        [SerializeField] Color bodyTextColor = Color.white;
        [SerializeField] Color nameTextColor = Color.white;
        [SerializeField] Color arrowColor = new Color32(0xF9, 0xC2, 0x2B, 0xFF);
        [SerializeField, Tooltip("대화 중이 아닌 쪽 초상화 색 (어둡게)")]
        Color inactivePortraitColor = new Color(0.45f, 0.45f, 0.5f, 1f);
        [SerializeField, Tooltip("배경 스프라이트 위에 덮는 어둡기")]
        Color backdropTint = new Color(0.42f, 0.4f, 0.48f, 1f);
        [SerializeField, Range(0f, 1f)] float dimAlpha = 0.35f;

        [Header("타이핑")]
        [SerializeField] bool useHangulTypewriter = true;
        [SerializeField, Min(0.005f), Tooltip("프레임(자모) 하나당 간격(초)")]
        float typingInterval = 0.028f;
        [SerializeField, Min(0f), Tooltip("문장 부호 뒤 추가 정지(초)")]
        float punctuationDelay = 0.12f;
        [SerializeField, Tooltip("타이핑 중 재생할 짧은 효과음 ID. 비우면 없음")]
        string typingSoundId = "";
        [SerializeField, Min(1), Tooltip("몇 프레임마다 타이핑 효과음을 낼지")]
        int typingSoundEvery = 3;

        [Header("화살표 (다음 표시)")]
        [SerializeField, Tooltip("아트 화살표. 비우면 픽셀 삼각형을 런타임에 만든다")]
        Sprite arrowSprite;
        [SerializeField] Vector2 arrowSize = new Vector2(36f, 24f);
        [SerializeField, Min(0f), Tooltip("위아래로 튀는 높이(px)")]
        float arrowBobAmplitude = 8f;
        [SerializeField, Min(0f), Tooltip("튀는 속도")]
        float arrowBobSpeed = 4.5f;
        [SerializeField, Tooltip("픽셀 느낌으로 계단식으로 움직일지 (부드럽게 움직이면 끈다)")]
        bool arrowStepMotion = true;
        [SerializeField, Min(1f), Tooltip("계단식 이동 시 한 칸(px)")]
        float arrowStepSize = 4f;

        [Header("상자 아트 (피그마 시안)")]
        [SerializeField, Tooltip("대사 상자 스프라이트(1_dialogue, 320×72). 비우면 단색 상자 + 테두리")]
        Sprite boxSprite;
        [SerializeField, Tooltip("상자 화면 크기(px). 320×72 를 약 4.75배")]
        Vector2 boxSize = new Vector2(1520f, 300f);
        [SerializeField, Tooltip("화면 하단 중앙 기준 상자 위치")]
        Vector2 boxOffset = new Vector2(0f, 90f);
        [SerializeField, Tooltip("이름 영역 (상자 기준 0~1). 시안의 그라데이션 띠 위")]
        Vector2 nameAreaMin = new Vector2(0.10f, 0.66f);
        [SerializeField] Vector2 nameAreaMax = new Vector2(0.50f, 0.95f);
        [SerializeField, Tooltip("본문 영역 (상자 기준 0~1)")]
        Vector2 bodyAreaMin = new Vector2(0.06f, 0.10f);
        [SerializeField] Vector2 bodyAreaMax = new Vector2(0.94f, 0.62f);
        [SerializeField, Tooltip("본문 가운데 정렬 (끄면 좌상단)")]
        bool centerBodyText = true;
        [SerializeField, Tooltip("상자 우하단 기준 화살표 위치")]
        Vector2 arrowOffset = new Vector2(-34f, 28f);

        [Header("스킵 (우하단)")]
        [SerializeField, Tooltip("키 아이콘(1_f_key). 비우면 글자만")]
        Sprite skipKeySprite;
        [SerializeField] Vector2 skipKeySize = new Vector2(48f, 48f);
        [SerializeField] string skipLabelText = "스킵하기";
        [SerializeField, Tooltip("이 키로 스킵한다")] bool skipWithFKey = true;

        [Header("배경")]
        [SerializeField, Tooltip("이전 화면을 한 번 캡처해 흐리게 깐다 (일시정지 메뉴와 같은 UIBlurBackdrop). 끄면 스테이지 그림 + 딤")]
        bool useScreenBlur = true;
        [SerializeField, Tooltip("블러 위에 곱하는 색 (어둡기)")]
        Color blurTint = new Color(0.5f, 0.5f, 0.58f, 1f);
        [SerializeField, Tooltip("좌상단 공연장 이름·아이콘 표시 (시안에는 없음)")]
        bool showVenueLabel = false;

        [Header("전환")]
        [SerializeField, Min(0f)] float fadeInDuration = 0.18f;
        [SerializeField, Min(0f)] float fadeOutDuration = 0.14f;

        public TMP_FontAsset Font => font;
        public float BodyFontSize => bodyFontSize;
        public float NameFontSize => nameFontSize;
        public float VenueFontSize => venueFontSize;
        public float RuleTitleFontSize => ruleTitleFontSize;
        public float RuleBodyFontSize => ruleBodyFontSize;
        public Color BoxColor => boxColor;
        public Color BoxBorderColor => boxBorderColor;
        public Color NameTagColor => nameTagColor;
        public Color BodyTextColor => bodyTextColor;
        public Color NameTextColor => nameTextColor;
        public Color ArrowColor => arrowColor;
        public Color InactivePortraitColor => inactivePortraitColor;
        public Color BackdropTint => backdropTint;
        public float DimAlpha => dimAlpha;
        public bool UseHangulTypewriter => useHangulTypewriter;
        public float TypingInterval => Mathf.Max(0.005f, typingInterval);
        public float PunctuationDelay => Mathf.Max(0f, punctuationDelay);
        public string TypingSoundId => typingSoundId;
        public int TypingSoundEvery => Mathf.Max(1, typingSoundEvery);
        public Sprite ArrowSprite => arrowSprite;
        public Vector2 ArrowSize => arrowSize;
        public float ArrowBobAmplitude => arrowBobAmplitude;
        public float ArrowBobSpeed => arrowBobSpeed;
        public bool ArrowStepMotion => arrowStepMotion;
        public float ArrowStepSize => Mathf.Max(1f, arrowStepSize);
        public float FadeInDuration => fadeInDuration;
        public float FadeOutDuration => fadeOutDuration;
        public Sprite BoxSprite => boxSprite;
        public Vector2 BoxSize => boxSize;
        public Vector2 BoxOffset => boxOffset;
        public Vector2 NameAreaMin => nameAreaMin;
        public Vector2 NameAreaMax => nameAreaMax;
        public Vector2 BodyAreaMin => bodyAreaMin;
        public Vector2 BodyAreaMax => bodyAreaMax;
        public bool CenterBodyText => centerBodyText;
        public Vector2 ArrowOffset => arrowOffset;
        public Sprite SkipKeySprite => skipKeySprite;
        public Vector2 SkipKeySize => skipKeySize;
        public string SkipLabelText => skipLabelText;
        public bool SkipWithFKey => skipWithFKey;
        public bool UseScreenBlur => useScreenBlur;
        public Color BlurTint => blurTint;
        public bool ShowVenueLabel => showVenueLabel;

#if UNITY_EDITOR
        /// <summary>[에디터 셋업 전용] 프로젝트 폰트를 연결한다.</summary>
        public void EditorSetFont(TMP_FontAsset fontAsset)
        {
            font = fontAsset;
        }

        /// <summary>[에디터 셋업 전용] 시안 아트를 연결한다 (비어 있는 칸만).</summary>
        public void EditorSetArt(Sprite box, Sprite arrow, Sprite skipKey)
        {
            if (boxSprite == null) boxSprite = box;
            if (arrowSprite == null) arrowSprite = arrow;
            if (skipKeySprite == null) skipKeySprite = skipKey;
            if (arrow != null)
            {
                arrowSize = new Vector2(arrow.rect.width * 4f, arrow.rect.height * 4f);
                // 시안: 상자 우하단 흰 영역 위의 검은 ▼
                arrowColor = new Color32(0x10, 0x0E, 0x14, 0xFF);
            }
        }
#endif
    }
}
