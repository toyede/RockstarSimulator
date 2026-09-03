using System;
using System.Collections.Generic;
using UnityEngine;

namespace ContextStage
{
    /// <summary>
    /// audiencePresetId → AudienceStagePreset. (기획서 §10 StageContentCatalog)
    /// 룰 ID → 룰 컴포넌트 매핑은 씬의 StageRuntimeDirector 가 자식 StageRuleBehaviour 로 해결하므로
    /// 여기서는 데이터 에셋만 관리한다. 빈 ID·중복 ID·없는 ID 는 진입 전에 검증한다.
    /// </summary>
    [CreateAssetMenu(fileName = "StageContentCatalog", menuName = "ContextStage/Tour/Stage Content Catalog")]
    public sealed class StageContentCatalog : ScriptableObject
    {
        public const string ResourcesPath = "Stages/StageContentCatalog";

        [SerializeField]
        List<AudienceStagePreset> audiencePresets = new List<AudienceStagePreset>();

        public IReadOnlyList<AudienceStagePreset> AudiencePresets => audiencePresets;

        public static StageContentCatalog LoadDefault() =>
            Resources.Load<StageContentCatalog>(ResourcesPath);

        public bool TryGetAudiencePreset(string presetId, out AudienceStagePreset preset)
        {
            preset = null;
            if (string.IsNullOrWhiteSpace(presetId) || audiencePresets == null) return false;

            string trimmed = presetId.Trim();
            for (int i = 0; i < audiencePresets.Count; i++)
            {
                AudienceStagePreset candidate = audiencePresets[i];
                if (candidate != null &&
                    string.Equals(candidate.PresetId, trimmed, StringComparison.Ordinal))
                {
                    preset = candidate;
                    return true;
                }
            }

            return false;
        }

        public bool TryValidate(AudienceEngagementConfig baseConfig, out string error)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            if (audiencePresets == null)
            {
                error = "관객 프리셋 목록이 없습니다.";
                return false;
            }

            for (int i = 0; i < audiencePresets.Count; i++)
            {
                AudienceStagePreset preset = audiencePresets[i];
                if (preset == null)
                {
                    error = $"카탈로그의 {i}번 프리셋이 비어 있습니다.";
                    return false;
                }

                if (!preset.TryValidate(baseConfig, out error)) return false;
                if (!ids.Add(preset.PresetId))
                {
                    error = $"중복 presetId: {preset.PresetId}";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

#if UNITY_EDITOR
        /// <summary>[에디터 셋업 전용]</summary>
        public void EditorSetPresets(List<AudienceStagePreset> presets)
        {
            audiencePresets = presets ?? new List<AudienceStagePreset>();
        }
#endif
    }
}
