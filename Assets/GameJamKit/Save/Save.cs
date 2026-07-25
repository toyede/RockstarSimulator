using UnityEngine;

namespace GameJamKit
{
    /// <summary>
    /// PlayerPrefs 얇은 래퍼. 게임잼 규모에서는 이걸로 충분하다.
    ///
    /// Save.SetInt("highScore", 1200);
    /// int best = Save.GetInt("highScore");
    /// Save.SetObject("progress", myProgressClass);   // JsonUtility 직렬화
    /// var p = Save.GetObject&lt;Progress&gt;("progress");
    ///
    /// 주의: SetObject 로 저장할 클래스는 [System.Serializable] 이어야 하고
    ///       Dictionary 는 JsonUtility 가 지원하지 않는다.
    /// </summary>
    public static class Save
    {
        const string Prefix = "gjk_";
        static bool s_dirty;

        static string K(string key) => Prefix + key;

        // ---------------- 기본 타입 ----------------

        public static void SetInt(string key, int value) { PlayerPrefs.SetInt(K(key), value); MarkDirty(); }
        public static int GetInt(string key, int fallback = 0) => PlayerPrefs.GetInt(K(key), fallback);

        public static void SetFloat(string key, float value) { PlayerPrefs.SetFloat(K(key), value); MarkDirty(); }
        public static float GetFloat(string key, float fallback = 0f) => PlayerPrefs.GetFloat(K(key), fallback);

        public static void SetString(string key, string value) { PlayerPrefs.SetString(K(key), value ?? string.Empty); MarkDirty(); }
        public static string GetString(string key, string fallback = "") => PlayerPrefs.GetString(K(key), fallback);

        public static void SetBool(string key, bool value) => SetInt(key, value ? 1 : 0);
        public static bool GetBool(string key, bool fallback = false) => GetInt(key, fallback ? 1 : 0) != 0;

        // ---------------- 오브젝트 ----------------

        public static void SetObject<T>(string key, T value)
        {
            if (value == null) { Delete(key); return; }
            SetString(key, JsonUtility.ToJson(value));
        }

        public static T GetObject<T>(string key) where T : new()
        {
            var json = GetString(key, null);
            if (string.IsNullOrEmpty(json)) return new T();

            try { return JsonUtility.FromJson<T>(json); }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[Save] '{key}' 역직렬화 실패: {e.Message}");
                return new T();
            }
        }

        // ---------------- 유틸 ----------------

        public static bool Has(string key) => PlayerPrefs.HasKey(K(key));
        public static void Delete(string key) { PlayerPrefs.DeleteKey(K(key)); MarkDirty(); }

        /// <summary>이 킷이 저장한 전체 데이터 삭제. (다른 키도 함께 지워지므로 주의)</summary>
        public static void DeleteAll() { PlayerPrefs.DeleteAll(); MarkDirty(); }

        /// <summary>
        /// 보류 중인 PlayerPrefs 변경을 디스크에 기록한다.
        /// 일반 setter는 연속 입력마다 동기 저장하지 않도록 dirty 상태만 갱신한다.
        /// </summary>
        public static void Flush()
        {
            if (!s_dirty) return;
            PlayerPrefs.Save();
            s_dirty = false;
        }

        /// <summary>기존 기록보다 높으면 저장하고 true 반환.</summary>
        public static bool TrySetHighScore(int score, string key = "HighScore")
        {
            if (score <= GetInt(key, 0)) return false;
            SetInt(key, score);
            return true;
        }

        public static int GetHighScore(string key = "HighScore") => GetInt(key, 0);

        static void MarkDirty() => s_dirty = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void RegisterLifecycleFlush()
        {
            s_dirty = false;
            Application.quitting -= Flush;
            Application.quitting += Flush;
            Application.focusChanged -= HandleFocusChanged;
            Application.focusChanged += HandleFocusChanged;
        }

        static void HandleFocusChanged(bool hasFocus)
        {
            if (!hasFocus) Flush();
        }
    }
}
