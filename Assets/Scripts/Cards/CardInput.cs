using GameJamKit;
using UnityEngine;

namespace ContextStage
{
    /// <summary>카드 UI의 사용 요청을 검증하고 CardSystem에 전달한다.</summary>
    public sealed class CardInput : MonoBehaviour
    {
        public bool CanUseCard(int handIndex)
        {
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

            var gameManager = GameManager.Instance;
            if (gameManager.State == GameState.Ready) gameManager.StartGame();
            return gameManager.IsPlaying &&
                   CardSystem.Instance.SelectCard(handIndex, specialRequest);
        }
    }
}
