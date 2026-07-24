using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameJamKit
{
    /// <summary>
    /// 타입 기반 전역 이벤트 버스. 매니저/오브젝트 간 직접 참조를 없애 협업 충돌을 줄인다.
    ///
    /// 구독:   EventBus.Subscribe&lt;PlayerDied&gt;(OnPlayerDied);   // OnEnable
    /// 해제:   EventBus.Unsubscribe&lt;PlayerDied&gt;(OnPlayerDied); // OnDisable (필수!)
    /// 발행:   EventBus.Raise(new PlayerDied { Position = pos });
    /// </summary>
    public static class EventBus
    {
        static readonly List<Action> s_clearers = new List<Action>();

        internal static void RegisterClearer(Action clearer) => s_clearers.Add(clearer);

        public static void Subscribe<T>(Action<T> handler) => EventBus<T>.Subscribe(handler);
        public static void Unsubscribe<T>(Action<T> handler) => EventBus<T>.Unsubscribe(handler);
        public static void Raise<T>(T evt) => EventBus<T>.Raise(evt);

        /// <summary>모든 이벤트 구독 해제. 씬 전환/재시작 시 호출하면 유령 구독을 막을 수 있다.</summary>
        public static void ClearAll()
        {
            for (int i = 0; i < s_clearers.Count; i++) s_clearers[i]?.Invoke();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay() => ClearAll();
    }

    public static class EventBus<T>
    {
        static Action<T> s_handlers;

        static EventBus() => EventBus.RegisterClearer(Clear);

        public static void Subscribe(Action<T> handler)
        {
            if (handler == null) return;
            s_handlers -= handler; // 중복 구독 방지
            s_handlers += handler;
        }

        public static void Unsubscribe(Action<T> handler)
        {
            if (handler == null) return;
            s_handlers -= handler;
        }

        public static void Raise(T evt)
        {
            var handlers = s_handlers;
            if (handlers == null) return;

            // 핸들러 하나가 예외를 던져도 나머지는 계속 실행되도록 개별 호출
            var list = handlers.GetInvocationList();
            for (int i = 0; i < list.Length; i++)
            {
                try { ((Action<T>)list[i]).Invoke(evt); }
                catch (Exception e) { Debug.LogException(e); }
            }
        }

        public static void Clear() => s_handlers = null;
    }
}
