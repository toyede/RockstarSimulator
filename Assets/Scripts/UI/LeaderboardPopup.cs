using GameJamKit;
using UnityEngine;

namespace ContextStage
{
    /// <summary>
    /// 로컬 리더보드 전체를 점수 내림차순으로 보여주는 팝업. 킷 UIPopup 상속.
    /// ScoreEntryPopup 에서 저장 직후 자동으로 열리고, 그와 별개로 GameOver 상태가 되면
    /// 성공/실패와 무관하게 스스로도 자동으로 열린다 — 목표 미달성으로 점수를 저장하지
    /// 못한 판이라도 기존 리더보드는 "조회"할 수 있어야 하기 때문이다.
    ///
    /// 씬 배치: listContent 는 ScrollRect 의 Content(Vertical Layout Group 권장),
    /// rowPrefab 은 LeaderboardRowView 가 붙어 있는 행 프리팹(순위/닉네임/점수 Text 3개).
    /// "타이틀로" 버튼은 OnClickTitle 을 OnClick 에 연결.
    /// </summary>
    public class LeaderboardPopup : UIPopup
    {
        [SerializeField] Transform listContent;
        [SerializeField] GameObject rowPrefab;

        protected override void Awake()
        {
            base.Awake(); // 킷 규칙: UIPopup.Awake 를 반드시 호출해야 UIManager 에 등록된다

            // 주의: 닫힌 팝업은 SetActive(false) 상태라서 OnEnable/OnDisable 로 구독하면
            // 닫혀 있는 동안 이벤트를 놓친다. 그래서 Awake/OnDestroy 에서 구독한다.
            EventBus.Subscribe<GameStateChanged>(OnGameStateChanged);
        }

        protected override void OnDestroy()
        {
            EventBus.Unsubscribe<GameStateChanged>(OnGameStateChanged);
            base.OnDestroy(); // 킷 규칙: UIManager 등록 해제
        }

        void OnGameStateChanged(GameStateChanged e)
        {
            // 성공/실패 무관하게 GameOver가 되면 리더보드를 조회할 수 있게 스스로 연다.
            if (e.Current == GameState.GameOver) Open();
        }

        protected override void OnOpen() => Refresh();

        /// <summary>
        /// 목록을 다시 그린다. 이미 열려 있는 상태에서 새 점수가 저장됐을 때
        /// (ScoreEntryPopup.OnClickSave) 갱신하려면 Open() 만으로는 부족하므로 직접 호출한다
        /// — Open()은 이미 열려 있으면 아무 것도 하지 않아 OnOpen이 다시 불리지 않는다.
        /// </summary>
        /// <summary>스크롤 없이 한 화면에 그대로 보여줄 표시 개수.</summary>
        const int DisplayCount = 10;

        public void Refresh()
        {
            if (listContent == null || rowPrefab == null) return;

            foreach (Transform child in listContent) Destroy(child.gameObject);

            var records = LeaderboardStore.GetAll();
            int count = Mathf.Min(DisplayCount, records.Count);
            for (int i = 0; i < count; i++)
            {
                var row = Instantiate(rowPrefab, listContent);
                var rowView = row.GetComponent<LeaderboardRowView>();
                if (rowView != null) rowView.SetData(i + 1, records[i].playerName, records[i].score);
            }
        }

        /// <summary>버튼 OnClick 에 연결.</summary>
        public void OnClickTitle() => TitleReturn.Go();
    }
}
