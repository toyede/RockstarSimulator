using UnityEngine;
using UnityEngine.UI;

namespace ContextStage
{
    /// <summary>
    /// 리더보드 한 행: 순위/닉네임/점수를 각각 별도 Text로 표시한다.
    /// 탭 문자로 Text 하나에 몰아 넣으면 이름 길이에 따라 열 정렬이 틀어지므로 열마다 분리했다.
    /// </summary>
    public class LeaderboardRowView : MonoBehaviour
    {
        [SerializeField] Text rankText;
        [SerializeField] Text nameText;
        [SerializeField] Text scoreText;
        [SerializeField, Tooltip("1등일 때만 활성화되는 왕관 아이콘")] GameObject crownIcon;

        public void SetData(int rank, string playerName, int score)
        {
            if (rankText != null) rankText.text = rank.ToString();
            if (nameText != null) nameText.text = playerName;
            if (scoreText != null) scoreText.text = $"{score}점";
            if (crownIcon != null) crownIcon.SetActive(rank == 1);
        }
    }
}
