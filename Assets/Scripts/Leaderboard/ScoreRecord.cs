using System.Collections.Generic;

namespace ContextStage
{
    /// <summary>로컬 리더보드 한 줄(이름 + 점수).</summary>
    [System.Serializable]
    public class ScoreRecord
    {
        public string playerName;
        public int score;
    }

    /// <summary>
    /// JsonUtility는 최상위 List/배열을 직렬화하지 못하므로(GameJamKit/Save/Save.cs 참고)
    /// Save.SetObject/GetObject에 넘기기 위한 wrapper.
    /// </summary>
    [System.Serializable]
    public class ScoreRecordList
    {
        public List<ScoreRecord> entries = new List<ScoreRecord>();
    }
}
