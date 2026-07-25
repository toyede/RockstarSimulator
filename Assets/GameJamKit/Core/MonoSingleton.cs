using UnityEngine;

namespace GameJamKit
{
    /// <summary>
    /// 모든 매니저의 베이스 클래스.
    /// - Instance 접근 시 씬에 없으면 자동 생성된다.
    /// - Persistent 가 true 면 DontDestroyOnLoad (기본값).
    /// - 중복 인스턴스는 스스로 파괴된다.
    ///
    /// 사용법: public class MyManager : MonoSingleton&lt;MyManager&gt; { }
    /// 주의: Awake/OnDestroy 를 오버라이드할 때는 반드시 base 를 호출하거나,
    ///       대신 OnAwake() 를 오버라이드할 것.
    /// </summary>
    public abstract class MonoSingleton<T> : MonoBehaviour where T : MonoSingleton<T>
    {
        static T s_instance;

        public static bool HasInstance => !SingletonRuntime.IsQuitting && s_instance != null;

        public static T Instance
        {
            get
            {
                if (SingletonRuntime.IsQuitting) return null;
                if (s_instance != null) return s_instance;

                s_instance = FindFirstObjectByType<T>();
                if (s_instance == null)
                {
                    var go = new GameObject($"[{typeof(T).Name}]");
                    s_instance = go.AddComponent<T>(); // AddComponent 가 Awake 를 호출하며 s_instance 를 채운다
                }
                return s_instance;
            }
        }

        /// <summary>씬 전환 시에도 살아남을지 여부. 씬마다 새로 필요한 매니저는 false 로 오버라이드.</summary>
        protected virtual bool Persistent => true;

        protected virtual void Awake()
        {
            if (s_instance != null && s_instance != this)
            {
                Destroy(gameObject);
                return;
            }

            s_instance = (T)this;
            if (Persistent && transform.parent == null)
                DontDestroyOnLoad(gameObject);

            OnAwake();
        }

        /// <summary>Awake 대신 여기에 초기화 코드를 작성한다.</summary>
        protected virtual void OnAwake() { }

        protected virtual void OnDestroy()
        {
            if (s_instance == this) s_instance = null;
        }

        protected virtual void OnApplicationQuit()
        {
            SingletonRuntime.IsQuitting = true;
        }
    }

    /// <summary>
    /// 종료 플래그 보관용 비제네릭 클래스.
    /// [RuntimeInitializeOnLoadMethod] 는 제네릭 클래스 안에 둘 수 없어서 분리했다.
    /// (Enter Play Mode 에서 Domain Reload 를 꺼도 플래그가 초기화되도록 하는 역할)
    /// </summary>
    public static class SingletonRuntime
    {
        public static bool IsQuitting;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay() => IsQuitting = false;
    }
}
