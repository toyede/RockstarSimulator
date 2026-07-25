using System;
using UnityEngine;

namespace ContextStage
{
    [CreateAssetMenu(
        fileName = "AudiencePreferenceHoverConfig",
        menuName = "ContextStage/Audience/Preference Hover Config")]
    public sealed class AudiencePreferenceHoverConfig : ScriptableObject
    {
        [Header("Timing")]
        [SerializeField, Min(0f)] float revealDelay = 1f;

        [Header("Card-matched Preference Colors")]
        [SerializeField] Color chillColor =
            new Color32(0x31, 0xDF, 0xEA, 0xFF);
        [SerializeField] Color singalongColor =
            new Color32(0x64, 0x31, 0xEA, 0xFF);
        [SerializeField] Color moshColor =
            new Color32(0xF0, 0x1F, 0x1F, 0xFF);

        [Header("Outline")]
        [SerializeField, Min(0f)] float outlineThickness = 2f;

        public float RevealDelay => Mathf.Max(0f, revealDelay);
        public float OutlineThickness => Mathf.Max(0f, outlineThickness);

        public Color GetColor(CrowdPreference preference)
        {
            switch (preference)
            {
                case CrowdPreference.Chill: return chillColor;
                case CrowdPreference.Singalong: return singalongColor;
                case CrowdPreference.Mosh: return moshColor;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(preference),
                        preference,
                        null);
            }
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            revealDelay = Mathf.Max(0f, revealDelay);
            outlineThickness = Mathf.Max(0f, outlineThickness);
        }
#endif
    }
}
