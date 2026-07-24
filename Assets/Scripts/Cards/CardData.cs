using GameJamKit;
using UnityEngine;

namespace ContextStage
{
    /// <summary>
    /// 카드 한 장의 표시 정보와 유효 호응도 구간.
    /// 현재 호응도가 구간 안이면 Perfect, 밖이면 Miss로 판정한다.
    /// 실제 증감량은 HypeConfig에서 가져오므로 카드 에셋에는 수치를 중복 저장하지 않는다.
    /// </summary>
    [CreateAssetMenu(fileName = "NewCard", menuName = "ContextStage/Card")]
    public sealed class CardData : ScriptableObject
    {
        [SerializeField] string id = "card";
        [SerializeField] string displayName = "Card";
        [SerializeField, TextArea(2, 4)] string description = "";
        [SerializeField, Range(0f, 100f)] float favorableHypeMin;
        [SerializeField, Range(0f, 100f)] float favorableHypeMax = 100f;
        [SerializeField] Color prototypeColor = new Color(0.25f, 0.3f, 0.4f);

        public string Id => id;
        public string DisplayName => displayName;
        public string Description => description;
        public float FavorableHypeMin => Mathf.Min(favorableHypeMin, favorableHypeMax);
        public float FavorableHypeMax => Mathf.Max(favorableHypeMin, favorableHypeMax);
        public Color PrototypeColor => prototypeColor;

        public bool IsFavorable(float currentHype)
            => currentHype >= FavorableHypeMin && currentHype <= FavorableHypeMax;

        public HypeJudgement ResolveJudgement(float currentHype)
            => IsFavorable(currentHype) ? HypeJudgement.Perfect : HypeJudgement.Miss;

        public float GetPreviewDelta(float currentHype, HypeConfig config)
            => config != null ? config.GetDelta(ResolveJudgement(currentHype)) : 0f;
    }
}
