using GameJamKit;
using UnityEngine;

namespace ContextStage
{
    /// <summary>Title 씬의 랭킹 버튼에서 공용 랭킹 팝업을 여는 얇은 연결 컴포넌트.</summary>
    public sealed class TitleLeaderboardLauncher : MonoBehaviour
    {
        const string ClickSoundId = "ui_click";

        public void OpenLeaderboard()
        {
            Sound.Play(ClickSoundId);

            var manager = UIManager.Instance;
            if (manager == null)
            {
                Debug.LogWarning("[TitleLeaderboard] UIManager를 찾지 못했습니다.");
                return;
            }

            var popup = manager.Get<TitleLeaderboardPopup>();
            if (popup == null)
            {
                // Title의 팝업은 시작 후 숨겨지므로 매니저 등록이 비어 있어도 같은 씬에서 찾는다.
                foreach (var root in gameObject.scene.GetRootGameObjects())
                {
                    popup = root.GetComponentInChildren<TitleLeaderboardPopup>(true);
                    if (popup != null) break;
                }

                if (popup == null)
                {
                    Debug.LogWarning("[TitleLeaderboard] Title 씬에 랭킹 팝업이 없습니다.");
                    return;
                }

                manager.Register(popup);
            }

            popup.Open();
        }
    }
}
