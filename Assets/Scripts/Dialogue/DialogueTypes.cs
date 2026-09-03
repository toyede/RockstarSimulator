using System;
using UnityEngine;

namespace ContextStage
{
    /// <summary>초상화가 서는 쪽. 화자가 바뀔 때 반대편 초상화는 어둡게 내려간다.</summary>
    public enum DialogueSpeakerSide
    {
        Left,
        Right
    }

    /// <summary>
    /// 대사 한 줄. (기획서 §14 DialogueLine)
    /// 선택지·호감도·분기용 필드는 지금 추가하지 않는다.
    /// </summary>
    [Serializable]
    public sealed class DialogueLine
    {
        [Tooltip("화자 ID (raccoon / hedgehog / zebra / hyena / promoter / rival ...). 초상화 교체·통계용")]
        public string speakerId = "";

        [Tooltip("화면에 표시할 화자 이름")]
        public string speakerName = "";

        [Tooltip("초상화. 비워두면 이름표만 표시한다")]
        public Sprite portrait;

        [Tooltip("표정 ID. 초상화 세트가 준비되면 이 값으로 스프라이트를 고른다")]
        public string emotionId = "";

        [TextArea(2, 4), Tooltip("본문. 리치 텍스트(<color>) 사용 가능. 한 화면 두 문장을 넘기지 않는다")]
        public string text = "";

        [Tooltip("초상화가 서는 쪽")]
        public DialogueSpeakerSide side = DialogueSpeakerSide.Left;

        [Tooltip("이 줄이 시작될 때 재생할 사운드 ID (SoundLibrary). 비우면 없음")]
        public string sfxId = "";

        public bool HasText => !string.IsNullOrWhiteSpace(text);
    }

    /// <summary>
    /// 대화 화면이 시퀀스 바깥에서 받아야 하는 표현 정보.
    /// (현재 공연장 이름·배경·룰 카드는 스테이지가 결정하므로 시퀀스 데이터에 넣지 않는다)
    /// </summary>
    public struct DialoguePresentationContext
    {
        [Tooltip("좌상단에 표시할 공연장 이름. 비우면 숨긴다")]
        public string venueName;

        [Tooltip("대화 뒤에 어둡게 깔 배경. 비우면 단색")]
        public Sprite backdrop;

        [Tooltip("마지막에 보여줄 룰 카드. ruleTitle 이 비어 있으면 룰 카드를 건너뛴다")]
        public string ruleTitle;
        public string ruleBody;
        public Sprite ruleIcon;

        [Tooltip("룰 카드 확인 버튼 문구. 비우면 기본값")]
        public string confirmLabel;

        public bool HasRuleCard => !string.IsNullOrWhiteSpace(ruleTitle);
    }
}
