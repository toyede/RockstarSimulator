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
        enum PendingOperationType
        {
            Subscribe,
            Unsubscribe,
            Clear
        }

        struct PendingOperation
        {
            public PendingOperationType Type;
            public Action<T> Handler;
        }

        static readonly List<Action<T>> s_handlers = new List<Action<T>>();
        static readonly List<PendingOperation> s_pendingOperations = new List<PendingOperation>();
        static int s_raiseDepth;

        static EventBus() => EventBus.RegisterClearer(Clear);

        public static void Subscribe(Action<T> handler)
        {
            if (handler == null) return;
            if (s_raiseDepth > 0)
            {
                s_pendingOperations.Add(new PendingOperation
                {
                    Type = PendingOperationType.Subscribe,
                    Handler = handler
                });
                return;
            }

            SubscribeImmediate(handler);
        }

        public static void Unsubscribe(Action<T> handler)
        {
            if (handler == null) return;
            if (s_raiseDepth > 0)
            {
                s_pendingOperations.Add(new PendingOperation
                {
                    Type = PendingOperationType.Unsubscribe,
                    Handler = handler
                });
                return;
            }

            s_handlers.Remove(handler);
        }

        public static void Raise(T evt)
        {
            if (s_handlers.Count == 0) return;

            s_raiseDepth++;
            try
            {
                int count = s_handlers.Count;
                for (int i = 0; i < count; i++)
                {
                    try { s_handlers[i].Invoke(evt); }
                    catch (Exception e) { Debug.LogException(e); }
                }
            }
            finally
            {
                s_raiseDepth--;
                if (s_raiseDepth == 0) ApplyPendingOperations();
            }
        }

        public static void Clear()
        {
            if (s_raiseDepth > 0)
            {
                s_pendingOperations.Add(new PendingOperation { Type = PendingOperationType.Clear });
                return;
            }

            s_handlers.Clear();
            s_pendingOperations.Clear();
        }

        static void SubscribeImmediate(Action<T> handler)
        {
            s_handlers.Remove(handler); // 중복 구독 방지 + 마지막 구독 순서 유지
            s_handlers.Add(handler);
        }

        static void ApplyPendingOperations()
        {
            for (int i = 0; i < s_pendingOperations.Count; i++)
            {
                PendingOperation operation = s_pendingOperations[i];
                switch (operation.Type)
                {
                    case PendingOperationType.Subscribe:
                        SubscribeImmediate(operation.Handler);
                        break;
                    case PendingOperationType.Unsubscribe:
                        s_handlers.Remove(operation.Handler);
                        break;
                    case PendingOperationType.Clear:
                        s_handlers.Clear();
                        break;
                }
            }

            s_pendingOperations.Clear();
        }
    }
}
