using System.Collections.Generic;
using GameJamKit;
using UnityEngine;

namespace ContextStage
{
    /// <summary>
    /// 카드를 사용할 때 효과음을 낸다. [임시] 기타 스트로크 한 방으로 타격감만 먼저 확보하는 단계.
    ///
    /// 카드 코드는 건드리지 않고 CardSelected 이벤트만 구독하므로,
    /// 카드 담당이 로직을 바꿔도 이쪽은 영향을 받지 않는다.
    ///
    /// 나중에 카드별·판정별로 소리를 나누고 싶으면 인스펙터에서 항목만 추가하면 된다.
    ///   - cardOverrides : "guitar_solo" 카드만 다른 소리
    ///   - 판정 효과음   : Perfect 는 함성, Miss 는 야유 (기본값은 비어 있어 아무 소리도 안 난다)
    /// </summary>
    [DisallowMultipleComponent]
    public class CardSfxPlayer : MonoBehaviour
    {
        [System.Serializable]
        public class CardSfxOverride
        {
            [Tooltip("CardData 의 id")] public string cardId;
            [Tooltip("이 카드일 때 대신 재생할 SoundLibrary ID")] public string sfxId;
        }

        [Header("카드 사용")]
        [SerializeField, Tooltip("모든 카드에 공통으로 나는 소리. 비우면 재생하지 않는다")]
        string defaultSfxId = "guitar_stroke";

        [SerializeField, Tooltip("특정 카드만 다른 소리를 내고 싶을 때")]
        List<CardSfxOverride> cardOverrides = new List<CardSfxOverride>();

        [SerializeField, Range(0f, 1f), Tooltip("카드 효과음 볼륨 배율")]
        float volumeScale = 1f;

        [Header("판정 반응 (비워두면 재생 안 함)")]
        [SerializeField, Tooltip("Perfect 판정 시 추가로 낼 소리")] string perfectSfxId = "hey_high";
        [SerializeField, Tooltip("Good 판정 시 추가로 낼 소리")] string goodSfxId = "";
        [SerializeField, Tooltip("Miss 판정 시 추가로 낼 소리")] string missSfxId = "";

        [SerializeField, Tooltip("RiskMiss 판정 시 추가로 낼 소리. 특수 카드를 빗나가게 냈을 때 울린다")]
        string riskMissSfxId = "crowd_mistake";

        void OnEnable() => EventBus.Subscribe<CardSelected>(OnCardSelected);
        void OnDisable() => EventBus.Unsubscribe<CardSelected>(OnCardSelected);

        void OnCardSelected(CardSelected e)
        {
            PlayIfSet(ResolveCardSfx(e.CardId));
            PlayIfSet(ResolveJudgementSfx(e.Judgement));
        }

        /// <summary>빈 ID 는 조용히 넘긴다. (킷은 없는 ID 에 경고를 찍으므로 로그가 더러워지는 걸 막는다)</summary>
        void PlayIfSet(string id)
        {
            if (!string.IsNullOrEmpty(id)) Sound.Play(id, volumeScale);
        }

        string ResolveCardSfx(string cardId)
        {
            for (int i = 0; i < cardOverrides.Count; i++)
            {
                var o = cardOverrides[i];
                if (o != null && !string.IsNullOrEmpty(o.cardId) && o.cardId == cardId)
                    return o.sfxId;
            }
            return defaultSfxId;
        }

        string ResolveJudgementSfx(HypeJudgement judgement)
        {
            switch (judgement)
            {
                case HypeJudgement.Perfect:  return perfectSfxId;
                case HypeJudgement.Good:     return goodSfxId;
                case HypeJudgement.Miss:     return missSfxId;
                case HypeJudgement.RiskMiss: return riskMissSfxId;
                default:                     return null;
            }
        }
    }
}
