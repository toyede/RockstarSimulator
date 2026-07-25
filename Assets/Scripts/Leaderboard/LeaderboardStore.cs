using System.Collections.Generic;
using GameJamKit;

namespace ContextStage
{
    /// <summary>
    /// 로컬 리더보드 저장/조회. 킷의 Save.SetObject/GetObject(PlayerPrefs+JsonUtility)를 그대로 사용한다.
    /// 저장량은 로컬 PlayerPrefs가 무한히 커지지 않도록 상위 기록으로 제한한다.
    /// </summary>
    public static class LeaderboardStore
    {
        const string Key = "Leaderboard";
        public const int MaxEntries = 100;

        /// <summary>기록을 추가하고 점수 내림차순으로 정렬해 저장한다.</summary>
        public static void Add(string name, int score)
        {
            var list = Load();
            list.entries.Add(new ScoreRecord { playerName = name, score = score });
            Sort(list.entries);
            if (list.entries.Count > MaxEntries)
                list.entries.RemoveRange(MaxEntries, list.entries.Count - MaxEntries);
            Save.SetObject(Key, list);
            Save.Flush();
        }

        /// <summary>점수 내림차순으로 정렬된 전체 기록.</summary>
        public static IReadOnlyList<ScoreRecord> GetAll()
        {
            var list = Load();
            Sort(list.entries);
            return list.entries.AsReadOnly();
        }

        /// <summary>리더보드 기록만 초기화한다 (다른 PlayerPrefs 값은 건드리지 않음).</summary>
        public static void Clear()
        {
            Save.Delete(Key);
            Save.Flush();
        }

        static ScoreRecordList Load()
        {
            ScoreRecordList list = Save.GetObject<ScoreRecordList>(Key);
            list.entries ??= new List<ScoreRecord>();
            list.entries.RemoveAll(record => record == null);
            return list;
        }

        static void Sort(List<ScoreRecord> entries) =>
            entries.Sort(CompareScores);

        static int CompareScores(ScoreRecord a, ScoreRecord b) =>
            b.score.CompareTo(a.score);
    }
}
