using System;
using System.Collections.Generic;
using GameJamKit;
using UnityEngine;

namespace ContextStage
{
    /// <summary>
    /// Stage 2 이후 — 단골의 요청 (special_audience_requests). (기획서 §6)
    ///
    /// 새 매니저를 만들지 않고 기존 SpecialAudienceManager 를 켠다.
    /// 스테이지별 등장 간격(예: Stage 2 = 18초 뒤 첫 등장, 24초 간격, 8초 요청)은
    /// SpecialAudienceConfig 에셋을 스테이지별로 지정해 주입한다.
    /// </summary>
    public sealed class SpecialAudienceRequestsRule : StageRuleBehaviour
    {
        [Serializable]
        public struct StageOverride
        {
            [Tooltip("이 스테이지에서만 쓸 설정")]
            public StageDefinition stage;
            public SpecialAudienceConfig config;
        }

        [SerializeField, Tooltip("스테이지별 지정이 없을 때 쓸 설정. 비우면 씬의 SpecialAudienceManager 기본값")]
        SpecialAudienceConfig defaultConfig;

        [SerializeField]
        List<StageOverride> stageOverrides = new List<StageOverride>();

        [SerializeField, Tooltip("이 스테이지에서 성공한 Special Hit 횟수 (읽기 전용)")]
        int specialHitCount;

        public int SpecialHitCount => specialHitCount;

        protected override void OnActivate(StageRuleContext context)
        {
            specialHitCount = 0;
            EventBus.Subscribe<SpecialHitLanded>(OnSpecialHit);

            SpecialAudienceManager manager = context.SpecialAudience;
            if (manager == null)
            {
                Debug.LogWarning("[StageRule] SpecialAudienceManager 가 없어 특별 관객 룰을 켤 수 없습니다.", this);
                return;
            }

            manager.SetRuntimeConfig(ResolveConfig(context.Stage));
            manager.SetAutoStart(true);
            if (GameManager.HasInstance && GameManager.Instance.IsPlaying && !manager.IsRunning)
                manager.StartSystem();
        }

        protected override void OnDeactivate()
        {
            EventBus.Unsubscribe<SpecialHitLanded>(OnSpecialHit);

            SpecialAudienceManager manager = Context.SpecialAudience;
            if (manager == null) return;
            manager.SetAutoStart(false);
            manager.StopSystem();
            manager.SetRuntimeConfig(null);
        }

        SpecialAudienceConfig ResolveConfig(StageDefinition stage)
        {
            if (stage != null)
            {
                for (int i = 0; i < stageOverrides.Count; i++)
                {
                    StageOverride entry = stageOverrides[i];
                    if (entry.stage == null || entry.config == null) continue;
                    if (ReferenceEquals(entry.stage, stage) ||
                        string.Equals(entry.stage.StageId, stage.StageId, StringComparison.Ordinal))
                        return entry.config;
                }
            }

            return defaultConfig;
        }

        void OnSpecialHit(SpecialHitLanded e) => specialHitCount++;

#if UNITY_EDITOR
        /// <summary>[에디터 셋업 전용]</summary>
        public void EditorConfigure(SpecialAudienceConfig fallback, List<StageOverride> overrides)
        {
            defaultConfig = fallback;
            stageOverrides = overrides ?? new List<StageOverride>();
        }
#endif
    }
}
