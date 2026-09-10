using System.Collections.Generic;
using UnityEngine;

namespace ContextStage
{
    /// <summary>
    /// 한 스테이지의 소품·조명 묶음 (씬 오브젝트). Main 씬 `Background/[StageSets]/<stageId>` 아래에 하나씩 둔다.
    ///
    /// 각 소품(ST01_FG_AmpLeft 같은 SpriteRenderer)은 자기 GameObject 라 위치·크기·회전을 씬에서 바로 고치고,
    /// 나중에 Animator 나 스쿼시·바운스 컴포넌트를 붙일 수 있다.
    /// StageBackgroundView 는 스테이지가 적용될 때 stageId 가 맞는 세트만 켜고 나머지는 끈다.
    /// 세트가 없는 스테이지는 예전처럼 StageVisualCatalog 로 런타임 생성한다.
    ///
    /// Tools/Tour/Setup Stage Sets 가 카탈로그 내용으로 초기 배치를 만든다 (이미 있으면 건드리지 않음).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StageSet : MonoBehaviour
    {
        [SerializeField, Tooltip("StageDefinition.StageId")] string stageId = "";
        [SerializeField, Tooltip("비우면 카탈로그의 Base 배경을 쓴다")] Sprite baseBackgroundOverride;
        [SerializeField, Tooltip("조명 레이어 (Base 위, Order 1~). 있으면 기존 stage_lights 를 숨긴다")] List<SpriteRenderer> lights = new List<SpriteRenderer>();
        [SerializeField, Tooltip("전경 소품 (Order 46). 코드에서 스쿼시·바운스를 걸 때 이 목록을 쓴다")] List<SpriteRenderer> props = new List<SpriteRenderer>();

        public string StageId => stageId;
        public Sprite BaseBackgroundOverride => baseBackgroundOverride;
        public IReadOnlyList<SpriteRenderer> Lights => lights;
        public IReadOnlyList<SpriteRenderer> Props => props;
        public bool HasLights => lights != null && lights.Count > 0;

        /// <summary>이름으로 소품 찾기 (예: "ST01_FG_AmpLeft").</summary>
        public SpriteRenderer FindProp(string name)
        {
            for (int i = 0; i < props.Count; i++)
                if (props[i] != null && props[i].name == name) return props[i];
            return null;
        }

#if UNITY_EDITOR
        /// <summary>[에디터 셋업 전용]</summary>
        public void EditorConfigure(string id, List<SpriteRenderer> lightRenderers, List<SpriteRenderer> propRenderers)
        {
            stageId = id ?? "";
            lights = lightRenderers ?? new List<SpriteRenderer>();
            props = propRenderers ?? new List<SpriteRenderer>();
        }
#endif
    }
}
