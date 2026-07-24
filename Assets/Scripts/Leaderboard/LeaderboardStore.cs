using System.Collections.Generic;
using GameJamKit;

namespace ContextStage
{
    /// <summary>
    /// 로컬 리더보드 저장/조회. 킷의 Save.SetObject/GetObject(PlayerPrefs+JsonUtility)를 그대로 사용한다.
    /// 저장 개수 제한 없음.
    /// </summary>
    public static class LeaderboardStore
    {
        const string Key = "Leaderboard";

        /// <summary>기록을 추가하고 점수 내림차순으로 정렬해 저장한다.</summary>
        public static void Add(string name, int score)
        {
            var list = Save.GetObject<ScoreRecordList>(Key);
            list.entries.Add(new ScoreRecord { playerName = name, score = score });
            list.entries.Sort((a, b) => b.score.CompareTo(a.score));
            Save.SetObject(Key, list);
        }

        /// <summary>점수 내림차순으로 정렬된 전체 기록.</summary>
        public static List<ScoreRecord> GetAll()
        {
            var list = Save.GetObject<ScoreRecordList>(Key);
            list.entries.Sort((a, b) => b.score.CompareTo(a.score));
            return list.entries;
        }
    }
}
