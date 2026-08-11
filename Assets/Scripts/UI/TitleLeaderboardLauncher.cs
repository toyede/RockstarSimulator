using GameJamKit;
using UnityEngine;

namespace ContextStage
{
    /// <summary>Title 씬의 랭킹 버튼에서 공용 랭킹 팝업을 여는 얇은 연결 컴포넌트.</summary>
    public sealed class TitleLeaderboardLauncher : MonoBehaviour
    {
        const string ClickSoundId = "ui_click_wooden";

        public void OpenLeaderboard()
        {
            Sound.Play(ClickSoundId);

            if (!UIManager.HasInstance)
            {
                Debug.LogWarning("[TitleLeaderboard] UIManager를 찾지 못했습니다.");
                return;
            }

            UIManager.Instance.Open<TitleLeaderboardPopup>();
        }
    }
}
