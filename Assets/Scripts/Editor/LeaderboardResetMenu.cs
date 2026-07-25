using UnityEditor;
using UnityEngine;

namespace ContextStage.EditorTools
{
    /// <summary>
    /// 로컬 리더보드(PlayerPrefs "gjk_Leaderboard")만 초기화한다.
    ///
    ///   Tools/UI/Clear Leaderboard Records
    ///
    /// Tools/GameJamKit/Clear Saved Data 는 PlayerPrefs 전체(하이스코어, 옵션 값 등)를 지우므로
    /// 리더보드 기록만 테스트용으로 리셋하고 싶을 때는 이 메뉴를 쓴다.
    /// </summary>
    public static class LeaderboardResetMenu
    {
        [MenuItem("Tools/UI/Clear Leaderboard Records", false, 10)]
        public static void ClearLeaderboard()
        {
            if (!EditorUtility.DisplayDialog("Leaderboard",
                    "저장된 리더보드 기록을 모두 삭제합니다. 계속할까요?", "삭제", "취소")) return;

            LeaderboardStore.Clear();
            Debug.Log("[Leaderboard] 리더보드 기록을 초기화했습니다.");
        }
    }
}
