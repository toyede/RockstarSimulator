using System.Collections;
using System.Collections.Generic;
using System.Text;
using GameJamKit;
using UnityEngine;
using UnityEngine.UI;

namespace ContextStage
{
    /// <summary>Title 씬에서 서버 TOP 10을 조회해 한 화면에 표시한다.</summary>
    public sealed class TitleLeaderboardPopup : UIPopup
    {
        const int DisplayCount = 10;
        const string ClickSoundId = "ui_click_wooden";

        [SerializeField] Text leaderboardText;

        Coroutine _refreshRoutine;
        int _refreshVersion;

        protected override void Awake()
        {
            if (leaderboardText == null)
                leaderboardText = FindLeaderboardText();

            base.Awake();
        }

        protected override void OnOpen()
        {
            Refresh();
        }

        protected override void OnClose()
        {
            _refreshVersion++;
            if (_refreshRoutine == null) return;

            StopCoroutine(_refreshRoutine);
            _refreshRoutine = null;
        }

        public void OnClickClose()
        {
            Sound.Play(ClickSoundId);
            Close();
        }

        public void Refresh()
        {
            if (leaderboardText == null) return;

            int version = ++_refreshVersion;
            if (!RemoteLeaderboardClient.IsConfigured)
            {
                RenderRecords(LeaderboardStore.GetAll(), "로컬 랭킹 TOP 10");
                return;
            }

            leaderboardText.text = "전체 랭킹 TOP 10\n\n랭킹을 불러오는 중...";
            if (_refreshRoutine != null)
                StopCoroutine(_refreshRoutine);
            _refreshRoutine = StartCoroutine(RefreshRemote(version));
        }

        IEnumerator RefreshRemote(int version)
        {
            bool succeeded = false;
            List<ScoreRecord> records = null;

            yield return RemoteLeaderboardClient.GetTopScores(
                DisplayCount,
                (success, result) =>
                {
                    succeeded = success;
                    records = result;
                });

            _refreshRoutine = null;
            if (version != _refreshVersion) yield break;

            if (succeeded && records != null)
            {
                RenderRecords(records, "전체 랭킹 TOP 10");
                yield break;
            }

            RenderRecords(
                LeaderboardStore.GetAll(),
                "서버 연결 실패 · 로컬 랭킹 TOP 10");
        }

        void RenderRecords(IReadOnlyList<ScoreRecord> records, string heading)
        {
            var builder = new StringBuilder(320);
            builder.AppendLine(heading);
            builder.AppendLine();
            builder.AppendLine("순위        이름                 점수");
            builder.AppendLine("────────────────────────");

            int count = records == null ? 0 : Mathf.Min(DisplayCount, records.Count);
            if (count == 0)
            {
                builder.AppendLine();
                builder.Append("아직 등록된 기록이 없습니다.");
            }
            else
            {
                for (int i = 0; i < count; i++)
                {
                    ScoreRecord record = records[i];
                    string playerName = RemoteLeaderboardClient.NormalizePlayerName(
                        record?.playerName);
                    int score = record == null ? 0 : record.score;
                    builder.AppendFormat(
                        "{0,2}위     {1,-12}     {2,9:N0}",
                        i + 1,
                        playerName,
                        score);
                    if (i < count - 1) builder.AppendLine();
                }
            }

            leaderboardText.text = builder.ToString();
        }

        Text FindLeaderboardText()
        {
            Text[] texts = GetComponentsInChildren<Text>(true);
            Text best = null;
            float bestArea = -1f;

            for (int i = 0; i < texts.Length; i++)
            {
                Text candidate = texts[i];
                if (candidate.GetComponentInParent<Button>() != null) continue;

                Rect rect = candidate.rectTransform.rect;
                float area = Mathf.Abs(rect.width * rect.height);
                if (area <= bestArea) continue;

                best = candidate;
                bestArea = area;
            }

            return best;
        }
    }
}
