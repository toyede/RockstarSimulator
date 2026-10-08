using UnityEngine;

namespace ContextStage
{
    /// <summary>표시용 설정만 보관. 점수·관객 비율·미션 조건은 변경하지 않는다.</summary>
    [CreateAssetMenu(menuName = "Raccoon Roll/Lighting/Stage Show Profile")]
    public sealed class StageLightingProfile : ScriptableObject
    {
        public string stageId;
        [Range(1, 5)] public int venue = 1;
        public bool rival;
        public Color primary = Color.cyan;
        public Color secondary = Color.magenta;
        [Range(0f, 1f)] public float beamIntensity = 0.2f;
        [Min(0f)] public float sweepDegrees = 15f;
        [Min(0.01f)] public float sweepPeriod = 4f;
        [Min(0.05f)] public float ledStep = 0.16f;
        [Min(0.05f)] public float transitionSeconds = 0.3f;
        [Min(0.1f)] public float smokeInterval = 4f;
        [Range(1, 10)] public int smokeParticlesPerEmitter = 10;
        public Material beamMaterial;
        public Material laserMaterial;
        [Min(0)] public float ambientFloor;
        public Material fixtureMaterial;
        public Material smokeMaterial;
        [Header("공연장 큐 (표시 전용)")]
        [Range(1f, 1.6f)] public float feverBoost = 1.3f;
        [Range(0, 0.4f)] public float washAlpha = 0.12f;
        [Range(0, 0.5f)] public float washPeakAlpha = 0.24f;
        [Min(0.1f)] public float washCueDuration = 1f;
        [Min(1f)] public float washCueCooldown = 3f;
        [Min(1f)] public float audienceSweepInterval = 12f;
        [Min(0.5f)] public float audienceSweepDuration = 2.5f;
        [Min(0.05f)] public float studioStepSeconds = 0.22f;
        [Min(0.1f)] public float rimDuration = 0.8f;
        [Range(0, 1)] public float rimStrength = 0.5f;
        [Range(0.1f, 0.8f)] public float dropQuietFraction = 0.45f;
        [Range(0, 8)] public int normalLasers = 4;
        [Range(0, 8)] public int dropLasers = 6;
        [Range(0, 8)] public int peakLasers = 8;
        [Header("드랍 시작/복귀 큐 (표시 전용)")]
        [Min(0.05f)] public float dropOpenSeconds = 0.12f;
        [Min(0.1f)] public float dropPeakSeconds = 0.5f;
        [Min(0.1f)] public float dropRecoverSeconds = 0.35f;
    }
}
