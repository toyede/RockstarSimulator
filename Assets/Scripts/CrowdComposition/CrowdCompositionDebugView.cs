using GameJamKit;
using UnityEngine;

namespace ContextStage
{
    /// <summary>Temporary, zero-art debug HUD for prototype validation.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CrowdCompositionManager))]
    public sealed class CrowdCompositionDebugView : MonoBehaviour
    {
        [SerializeField] CrowdCompositionManager manager;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        [SerializeField] bool showDebugPanel = true;
#endif
        [SerializeField] Vector2 position = new Vector2(12f, 12f);

        GUIStyle _box;
        GUIStyle _label;
        GUIStyle _banner;
        string _bannerText;
        float _bannerUntil;

        void Awake()
        {
            if (manager == null) manager = GetComponent<CrowdCompositionManager>();
        }

        void OnEnable()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            EventBus.Subscribe<CrowdShiftStarted>(OnCrowdShiftStarted);
#endif
        }

        void OnDisable()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            EventBus.Unsubscribe<CrowdShiftStarted>(OnCrowdShiftStarted);
#endif
        }

        void OnCrowdShiftStarted(CrowdShiftStarted e)
        {
            _bannerText = $"CROWD SHIFT!\n{e.EventName}";
            _bannerUntil = Time.unscaledTime + 1.8f;
        }

        void OnGUI()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!showDebugPanel || manager == null) return;
            EnsureStyles();

            Rect rect = new Rect(position.x, position.y, 325f, 240f);
            GUI.Box(rect, GUIContent.none, _box);
            GUILayout.BeginArea(new Rect(rect.x + 12f, rect.y + 9f, rect.width - 24f, rect.height - 18f));
            GUILayout.Label("CROWD", _label);
            GUILayout.Label($"PRESET: {manager.CurrentPresetDisplayName}", _label);
            GUILayout.Space(4f);
            DrawCount(CrowdPreference.Chill);
            DrawCount(CrowdPreference.Singalong);
            DrawCount(CrowdPreference.Mosh);
            GUILayout.Space(6f);
            GUILayout.Label("REACTION", _label);
            DrawReaction(CrowdPreference.Chill);
            DrawReaction(CrowdPreference.Singalong);
            DrawReaction(CrowdPreference.Mosh);
            GUILayout.Space(6f);
            GUILayout.Label("F1 Balanced  F2 Formal  F3 Britpop  F4 Hardcore", _label);
            GUILayout.EndArea();

            if (Time.unscaledTime < _bannerUntil)
            {
                Rect bannerRect = new Rect(Screen.width * 0.5f - 230f, 28f, 460f, 74f);
                GUI.Box(bannerRect, _bannerText, _banner);
            }
#endif
        }

        void DrawCount(CrowdPreference preference)
        {
            GUILayout.Label(
                $"{preference,-10} {manager.GetCount(preference),2}/{manager.ExpectedCrowdSize}  " +
                $"{manager.GetRatio(preference) * 100f,5:0.0}%",
                _label);
        }

        void DrawReaction(CrowdPreference preference) =>
            GUILayout.Label($"{preference,-10} {manager.EvaluateReaction(preference)}", _label);

        void EnsureStyles()
        {
            if (_box != null) return;
            _box = new GUIStyle(GUI.skin.box);
            _label = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                normal = { textColor = Color.white }
            };
            _banner = new GUIStyle(GUI.skin.box)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
        }
    }
}
