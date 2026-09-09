using GameJamKit;
using UnityEngine;

namespace ContextStage
{
    /// <summary>카드 UI의 사용 요청을 검증하고 CardSystem에 전달한다.</summary>
    public sealed class CardInput : MonoBehaviour
    {
        /// <summary>
        /// [튜토리얼 전용 훅] null 이면 아무 영향 없음(평소).
        /// 튜토리얼이 "지금은 이 카드만" 을 강제할 때 채우고, 끝나면 반드시 null 로 되돌린다.
        /// 거부된 시도는 UseBlocked 로 알려 힌트 문구를 띄울 수 있게 한다.
        /// 카드는 소모되지 않고 손패로 되돌아간다 (기존 실패 경로 그대로).
        /// </summary>
        public static System.Func<int, bool> UseFilter;
        public static event System.Action<int> UseBlocked;

        /// <summary>
        /// [연출 전용] true 인 동안 카드 사용·드래그 시작을 모두 막는다 (보스 연출 등).
        /// UseFilter 와 독립이라 튜토리얼·KILL SWITCH 필터와 충돌하지 않는다. 연출 담당이 끝날 때 반드시 false 로 되돌린다.
        /// </summary>
        public static bool Locked;

        public bool CanUseCard(int handIndex)
        {
            if (Locked) return false;
            if (!CardSystem.HasInstance || !GameManager.HasInstance) return false;
            if (UIManager.HasInstance && UIManager.Instance.AnyPopupOpen) return false;
            if (handIndex < 0 || handIndex >= CardSystem.Instance.HandCount) return false;

            var gameManager = GameManager.Instance;
            return gameManager.State == GameState.Ready || gameManager.IsPlaying;
        }

        public bool TryUseCard(int handIndex)
            => TryUseCard(handIndex, SpecialCardRequest.None);

        public bool TryUseCard(int handIndex, SpecialCardRequest specialRequest)
        {
            if (!CanUseCard(handIndex)) return false;

            // 튜토리얼이 지정한 카드가 아니면 소모 없이 되돌린다 (평소에는 UseFilter 가 null)
            if (UseFilter != null && !UseFilter(handIndex))
            {
                UseBlocked?.Invoke(handIndex);
                return false;
            }

            var gameManager = GameManager.Instance;
            if (gameManager.State == GameState.Ready) gameManager.StartGame();
            return gameManager.IsPlaying &&
                   CardSystem.Instance.SelectCard(handIndex, specialRequest);
        }
    }
}
