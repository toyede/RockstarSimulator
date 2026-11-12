using UnityEngine;
using UnityEngine.SceneManagement;

namespace ContextStage
{
    /// <summary>References the heavy sprites already used by Main; no copy of their textures.</summary>
    public sealed class TourPerformanceWarmupAsset : ScriptableObject
    {
        const string ResourcesPath = "Stages/TourPerformanceWarmup";
        [SerializeField] Sprite[] companionFrames;

        static ResourceRequest _framesRequest;
        static ResourceRequest _visualsRequest;
        static TourPerformanceWarmupAsset _frames;
        static StageVisualCatalog _visuals;

        public int CompanionFrameCount => companionFrames?.Length ?? 0;

        internal static void Begin()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;

            if (_frames == null && _framesRequest == null)
            {
                var request = _framesRequest = Resources.LoadAsync<TourPerformanceWarmupAsset>(ResourcesPath);
                request.completed += _ =>
                {
                    if (_framesRequest != request) return;
                    _frames = request.asset as TourPerformanceWarmupAsset;
                    _framesRequest = null;
                };
            }

            if (_visuals == null && _visualsRequest == null)
            {
                var request = _visualsRequest = Resources.LoadAsync<StageVisualCatalog>(StageVisualCatalog.ResourcesPath);
                request.completed += _ =>
                {
                    if (_visualsRequest != request) return;
                    _visuals = request.asset as StageVisualCatalog;
                    _visualsRequest = null;
                };
            }
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode == LoadSceneMode.Single && scene.name != "TourHub") Clear();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Clear()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            _framesRequest = null;
            _visualsRequest = null;
            _frames = null;
            _visuals = null;
        }
    }
}
