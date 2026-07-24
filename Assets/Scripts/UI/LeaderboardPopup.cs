using GameJamKit;
using UnityEngine;
using UnityEngine.UI;

namespace ContextStage
{
    /// <summary>
    /// 로컬 리더보드 전체를 점수 내림차순으로 보여주는 팝업. 킷 UIPopup 상속.
    /// ScoreEntryPopup 에서 저장 직후 자동으로 열린다.
    ///
    /// 씬 배치: listContent 는 ScrollRect 의 Content(Vertical Layout Group 권장),
    /// rowPrefab 은 Text 컴포넌트 하나만 있는 간단한 행 프리팹.
    /// "타이틀로" 버튼은 OnClickTitle 을 OnClick 에 연결.
    /// </summary>
    public class LeaderboardPopup : UIPopup
    {
        [SerializeField] Transform listContent;
        [SerializeField] GameObject rowPrefab;

        protected override void OnOpen()
        {
            if (listContent == null || rowPrefab == null) return;

            foreach (Transform child in listContent) Destroy(child.gameObject);

            var records = LeaderboardStore.GetAll();
            for (int i = 0; i < records.Count; i++)
            {
                var row = Instantiate(rowPrefab, listContent);
                var text = row.GetComponentInChildren<Text>();
                if (text != null) text.text = $"{i + 1}. {records[i].playerName} - {records[i].score}";
            }
        }

        /// <summary>버튼 OnClick 에 연결.</summary>
        public void OnClickTitle() => UIManager.Instance.LoadScene("Title");
    }
}
