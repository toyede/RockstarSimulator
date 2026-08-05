using System;
using TMPro;
using UnityEngine;

namespace ContextStage
{
    [CreateAssetMenu(
        fileName = "AudiencePreferenceHoverConfig",
        menuName = "ContextStage/Audience/Preference Hover Config")]
    public sealed class AudiencePreferenceHoverConfig : ScriptableObject
    {
        [Header("Timing")]
        [SerializeField, Min(0f)] float revealDelay = 1f;
        [SerializeField, Min(0f)] float dialogueDelay = 0.45f;

        [Header("Dialogue Presentation")]
        [SerializeField] TMP_FontAsset dialogueFont;
        [SerializeField] Color dialogueBackground =
            new Color32(0x2E, 0x22, 0x2F, 0xF0);
        [SerializeField, Min(0f)] float dialogueWorldOffset = 1.15f;
        [SerializeField] Vector2 dialogueSize = new Vector2(460f, 100f);
        [SerializeField, Min(12f)] float dialogueFontSize = 25f;
        [SerializeField, Min(0f)] float dialogueFadeSpeed = 12f;
        [SerializeField] bool useHangulTypewriter = true;
        [SerializeField, Min(0.005f)] float dialogueTypingInterval = 0.035f;
        [SerializeField, Min(0f)] float dialoguePunctuationDelay = 0.08f;

        [Header("Card-matched Preference Colors")]
        [SerializeField] Color chillColor =
            new Color32(0x31, 0xDF, 0xEA, 0xFF);
        [SerializeField] Color singalongColor =
            new Color32(0x64, 0x31, 0xEA, 0xFF);
        [SerializeField] Color moshColor =
            new Color32(0xF0, 0x1F, 0x1F, 0xFF);

        [Header("Outline")]
        [SerializeField, Min(0f)] float outlineThickness = 2f;

        public float RevealDelay => Mathf.Max(0f, revealDelay);
        public float DialogueDelay => Mathf.Max(RevealDelay, dialogueDelay);
        public float OutlineThickness => Mathf.Max(0f, outlineThickness);
        public TMP_FontAsset DialogueFont => dialogueFont;
        public Color DialogueBackground => dialogueBackground;
        public float DialogueWorldOffset => Mathf.Max(0f, dialogueWorldOffset);
        public Vector2 DialogueSize => new Vector2(
            Mathf.Max(240f, dialogueSize.x),
            Mathf.Max(64f, dialogueSize.y));
        public float DialogueFontSize => Mathf.Max(12f, dialogueFontSize);
        public float DialogueFadeSpeed => Mathf.Max(0f, dialogueFadeSpeed);
        public bool UseHangulTypewriter => useHangulTypewriter;
        public float DialogueTypingInterval => Mathf.Max(0.005f, dialogueTypingInterval);
        public float DialoguePunctuationDelay => Mathf.Max(0f, dialoguePunctuationDelay);

        public Color GetColor(CrowdPreference preference)
        {
            switch (preference)
            {
                case CrowdPreference.Chill: return chillColor;
                case CrowdPreference.Singalong: return singalongColor;
                case CrowdPreference.Mosh: return moshColor;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(preference),
                        preference,
                        null);
            }
        }

        public string GetRandomDialogue(
            CrowdPreference preference,
            AudienceEngagementStage stage,
            int previousIndex,
            out int selectedIndex)
        {
            string[] lines = ResolveDialogueLines(preference, stage);
            if (lines == null || lines.Length == 0)
            {
                selectedIndex = -1;
                return string.Empty;
            }

            selectedIndex = UnityEngine.Random.Range(0, lines.Length);
            if (lines.Length > 1 && selectedIndex == previousIndex)
                selectedIndex = (selectedIndex + UnityEngine.Random.Range(1, lines.Length)) % lines.Length;

            return lines[selectedIndex] ?? string.Empty;
        }

        static string[] ResolveDialogueLines(
            CrowdPreference preference,
            AudienceEngagementStage stage)
        {
            switch (preference)
            {
                case CrowdPreference.Mosh:
                    return stage == AudienceEngagementStage.Calm
                        ? MoshCalm
                        : stage == AudienceEngagementStage.Middle
                            ? MoshMiddle
                            : MoshExcited;

                case CrowdPreference.Singalong:
                    return stage == AudienceEngagementStage.Calm
                        ? SingalongCalm
                        : stage == AudienceEngagementStage.Middle
                            ? SingalongMiddle
                            : SingalongExcited;

                case CrowdPreference.Chill:
                    return stage == AudienceEngagementStage.Calm
                        ? ChillCalm
                        : stage == AudienceEngagementStage.Middle
                            ? ChillMiddle
                            : ChillExcited;

                default:
                    return Array.Empty<string>();
            }
        }

        const string MoshAccent = "#EA4F36";
        const string SingalongAccent = "#A884F3";
        const string ChillAccent = "#8FF8E2";

        static readonly string[] MoshCalm =
        {
            $"너무 얌전하잖아… <color={MoshAccent}>기타</color> 좀 더 세게 쳐!",
            $"이러다 목도 안 풀리겠어. <color={MoshAccent}>모쉬핏</color>은 언제 열려?",
            "발라드 공연이야? 한 방 크게 터뜨려 봐!",
        };

        static readonly string[] MoshMiddle =
        {
            $"오, 이제 좀 시동 걸리는데? <color={MoshAccent}>기타 솔로</color> 한번 가자.",
            $"앞줄 공간 좀 비워 둬. 곧 <color={MoshAccent}>모쉬핏</color>이 열릴 것 같아.",
            "조금만 더 거칠어지면 제대로 놀 수 있겠어.",
        };

        static readonly string[] MoshExcited =
        {
            $"누가 <color={MoshAccent}>모쉬핏</color> 좀 열어 봐!!",
            $"<color={MoshAccent}>모쉬핏</color>! <color={MoshAccent}>모쉬핏</color>!",
            $"이 <color={MoshAccent}>기타</color> 맛이지! 더 세게!",
        };

        static readonly string[] SingalongCalm =
        {
            "나도 아는 노래인데… 같이 부를 틈이 없네.",
            $"<color={SingalongAccent}>마이크</color> 한 번만 넘겨주면 안 돼?",
            $"우리도 <color={SingalongAccent}>손 머리 위로</color> 들 타이밍 좀 줘!",
        };

        static readonly string[] SingalongMiddle =
        {
            "기타 솔로도 좋은데… 다 같이 부를 차례는 언제야?",
            $"<color={SingalongAccent}>마이크</color>로 따라 부르고 싶다…",
            $"후렴 오면 <color={SingalongAccent}>손 머리 위로</color> 들 준비됐어.",
        };

        static readonly string[] SingalongExcited =
        {
            $"<color={SingalongAccent}>마이크</color> 이리 줘! 내가 부를게!",
            "오오오오—! 다 같이!",
            $"<color={SingalongAccent}>손 머리 위로</color>! 더 높이!",
        };

        static readonly string[] ChillCalm =
        {
            $"너무 정신없어… <color={ChillAccent}>템포</color>를 좀 정리해 줘.",
            $"우리 반응부터 살피면서 천천히 <color={ChillAccent}>호응</color>을 끌어내 봐.",
            "소리만 크다고 좋은 공연은 아닌데…",
        };

        static readonly string[] ChillMiddle =
        {
            $"오, <color={ChillAccent}>템포</color>가 이제 귀에 들어오네.",
            $"이 흐름이면 우리도 조금씩 <color={ChillAccent}>호응</color>할 수 있겠어.",
            "서두르지 마. 지금 그루브가 꽤 괜찮아.",
        };

        static readonly string[] ChillExcited =
        {
            $"좋아, 이 <color={ChillAccent}>템포</color> 그대로 가자.",
            "크게 날뛰진 않아도 완전히 빠져든 상태라고.",
            $"우리도 지금 제대로 <color={ChillAccent}>호응</color>하고 있어.",
        };

#if UNITY_EDITOR
        void OnValidate()
        {
            revealDelay = Mathf.Max(0f, revealDelay);
            dialogueDelay = Mathf.Max(revealDelay, dialogueDelay);
            outlineThickness = Mathf.Max(0f, outlineThickness);
            dialogueWorldOffset = Mathf.Max(0f, dialogueWorldOffset);
            dialogueSize.x = Mathf.Max(240f, dialogueSize.x);
            dialogueSize.y = Mathf.Max(64f, dialogueSize.y);
            dialogueFontSize = Mathf.Max(12f, dialogueFontSize);
            dialogueFadeSpeed = Mathf.Max(0f, dialogueFadeSpeed);
            dialogueTypingInterval = Mathf.Max(0.005f, dialogueTypingInterval);
            dialoguePunctuationDelay = Mathf.Max(0f, dialoguePunctuationDelay);
        }
#endif
    }
}
