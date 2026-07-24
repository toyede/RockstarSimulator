using System.Collections.Generic;
using GameJamKit;
using UnityEngine;

namespace ContextStage
{
    /// <summary>
    /// 관객 상태 변화에 반응하는 것들이 구현하는 인터페이스.
    /// 관객 스프라이트(CrowdMemberView)뿐 아니라 조명·배경·카메라도 이걸 구현해서
    /// Director 에 등록하면 코드 수정 없이 같은 타이밍에 함께 반응한다.
    /// </summary>
    public interface ICrowdMoodReactor
    {
        /// <param name="tier">새 상태 데이터</param>
        /// <param name="index">상태 인덱스 (0 = 가장 낮음)</param>
        /// <param name="instant">true 면 블렌드 없이 즉시 적용 (등록 직후·리셋 등)</param>
        void OnCrowdMoodChanged(CrowdMoodTier tier, int index, bool instant);
    }

    /// <summary>
    /// 호응도 → 관객 상태(low/middle/high...)를 결정하고 반응자들에게 뿌리는 지휘자.
    ///
    /// - 상태 판정은 사운드와 같은 HypeTierUtil 을 쓰므로 소리와 그림이 같은 순간에 바뀐다
    /// - 반응자는 자기 자신을 등록만 하면 되고, 늦게 생성돼도 등록 즉시 현재 상태를 받는다
    /// - 연출용으로 특정 상태를 강제할 수 있다 (앙코르 등)
    ///
    /// [다른 담당자용 API]
    ///   CrowdMood.Current;                 // "Low" / "Middle" / "High"
    ///   CrowdMood.ForceMood("High");       // 연출 중 강제 고조
    ///   CrowdMood.ReleaseForcedMood();     // 호응도 추종으로 복귀
    /// </summary>
    [DisallowMultipleComponent]
    public class CrowdMoodDirector : MonoSingleton<CrowdMoodDirector>
    {
        [SerializeField, Tooltip("상태·움직임 밸런스 에셋. 비워두면 기본값으로 임시 생성됨 (경고 출력)")]
        CrowdMoodConfig config;

        readonly List<ICrowdMoodReactor> _reactors = new List<ICrowdMoodReactor>();

        int _currentIndex = -1;
        int _forcedIndex = -1;

        /// <summary>씬 재시작 시 새로 초기화되도록 씬에 종속시킨다. (HypeSystem 과 동일)</summary>
        protected override bool Persistent => false;

        public CrowdMoodConfig Config => config;
        public int CurrentIndex => _currentIndex;
        public CrowdMoodTier CurrentTier => config != null ? config.GetTier(_currentIndex) : null;
        public string CurrentMoodName => CurrentTier?.moodName ?? "-";
        public bool IsForced => _forcedIndex >= 0;

        protected override void OnAwake()
        {
            if (config == null)
            {
                Debug.LogWarning("[CrowdMood] CrowdMoodConfig 가 지정되지 않아 기본값으로 임시 생성합니다. " +
                                 "Tools/Crowd/Setup Crowd Scene 을 실행하세요.");
                config = ScriptableObject.CreateInstance<CrowdMoodConfig>();
            }
            _currentIndex = config.ResolveTierIndex(Hype.Normalized);
        }

        void OnEnable()
        {
            EventBus.Subscribe<HypeChanged>(OnHypeChanged);
            EventBus.Subscribe<GameStateChanged>(OnGameStateChanged);
        }

        void OnDisable()
        {
            EventBus.Unsubscribe<HypeChanged>(OnHypeChanged);
            EventBus.Unsubscribe<GameStateChanged>(OnGameStateChanged);
        }

        // ---------------- 등록 ----------------

        /// <summary>반응자 등록. 등록 즉시 현재 상태를 한 번 받아 늦게 생성돼도 어긋나지 않는다.</summary>
        public void Register(ICrowdMoodReactor reactor)
        {
            if (reactor == null || _reactors.Contains(reactor)) return;
            _reactors.Add(reactor);
            reactor.OnCrowdMoodChanged(CurrentTier, _currentIndex, instant: true);
        }

        public void Unregister(ICrowdMoodReactor reactor)
        {
            if (reactor != null) _reactors.Remove(reactor);
        }

        // ---------------- 상태 결정 ----------------

        void OnHypeChanged(HypeChanged e)
        {
            if (_forcedIndex >= 0) return;
            ApplyMood(config.ResolveTierIndex(e.Normalized, _currentIndex));
        }

        void OnGameStateChanged(GameStateChanged e)
        {
            // 새 공연 준비 상태로 돌아가면 관객도 처음 상태로 (연출 강제도 함께 해제)
            if (e.Current != GameState.Ready) return;
            _forcedIndex = -1;
            ApplyMood(config.ResolveTierIndex(Hype.Normalized), instant: true);
        }

        /// <summary>[연출 담당] 호응도와 무관하게 상태를 고정. 해제는 ReleaseForcedMood().</summary>
        public void ForceMood(int index)
        {
            _forcedIndex = config.ClampIndex(index);
            ApplyMood(_forcedIndex);
        }

        /// <summary>이름으로 강제 지정. 예: ForceMood("High")</summary>
        public void ForceMood(string moodName)
        {
            int index = config.IndexOfTier(moodName);
            if (index < 0)
            {
                Debug.LogWarning($"[CrowdMood] '{moodName}' 상태를 콘픽에서 찾을 수 없습니다.");
                return;
            }
            ForceMood(index);
        }

        public void ReleaseForcedMood()
        {
            _forcedIndex = -1;
            ApplyMood(config.ResolveTierIndex(Hype.Normalized, _currentIndex));
        }

        void ApplyMood(int index, bool instant = false)
        {
            index = config.ClampIndex(index);
            if (index < 0) return;
            if (index == _currentIndex && !instant) return;

            int previous = _currentIndex;
            _currentIndex = index;
            var tier = config.GetTier(index);

            // 반응 중 등록/해제가 일어나도 안전하도록 뒤에서부터 순회한다
            for (int i = _reactors.Count - 1; i >= 0; i--)
            {
                var reactor = _reactors[i];
                if (reactor == null) { _reactors.RemoveAt(i); continue; }
                reactor.OnCrowdMoodChanged(tier, index, instant);
            }

            EventBus.Raise(new CrowdMoodChanged
            {
                PreviousIndex = previous,
                Index = index,
                MoodName = tier?.moodName
            });
        }
    }

    /// <summary>
    /// 어디서든 한 줄로 관객 상태를 다루는 전역 접근자. (Sound / Hype / CrowdAmbience 와 같은 패턴)
    /// </summary>
    public static class CrowdMood
    {
        public static bool Exists => CrowdMoodDirector.HasInstance;

        public static string Current => CrowdMoodDirector.HasInstance ? CrowdMoodDirector.Instance.CurrentMoodName : "-";
        public static int CurrentIndex => CrowdMoodDirector.HasInstance ? CrowdMoodDirector.Instance.CurrentIndex : -1;

        public static void ForceMood(string moodName) { if (CrowdMoodDirector.HasInstance) CrowdMoodDirector.Instance.ForceMood(moodName); }
        public static void ForceMood(int index)       { if (CrowdMoodDirector.HasInstance) CrowdMoodDirector.Instance.ForceMood(index); }
        public static void ReleaseForcedMood()        { if (CrowdMoodDirector.HasInstance) CrowdMoodDirector.Instance.ReleaseForcedMood(); }

        /// <summary>조명·배경 등 다른 연출도 관객과 같은 타이밍에 반응시키고 싶을 때 등록한다.</summary>
        public static void Register(ICrowdMoodReactor reactor)   { if (CrowdMoodDirector.HasInstance) CrowdMoodDirector.Instance.Register(reactor); }
        public static void Unregister(ICrowdMoodReactor reactor) { if (CrowdMoodDirector.HasInstance) CrowdMoodDirector.Instance.Unregister(reactor); }
    }
}
