using GameJamKit;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace ContextStage
{
    /// <summary>실제 손실만 화면 테두리/카메라에 표시한다. 점수 숫자는 기존 ScoreUI가 담당한다.</summary>
    [DisallowMultipleComponent]
    public sealed class ScoreLossPresentation : MonoBehaviour
    {
        [SerializeField, Range(0f, .3f)] float edgeOpacity = .13f;
        [SerializeField, Min(.1f)] float duration = .5f;
        [SerializeField, Min(0f)] float shakeWorldDistance = .035f;
        static ScoreLossPresentation _active;
        DamageEdgeGraphic _edge;
        GameObject _canvas;
        bool _pending, _boss;
        float _age = -1f, _strength;
        Camera _appliedCamera;
        Vector3 _offset;

        void OnEnable()
        {
            if (_active != null && _active != this) { enabled = false; return; }
            _active = this;
            EventBus.Subscribe<ScoreChanged>(OnScore);
            EventBus.Subscribe<BossDrainApplied>(OnDrain);
            EventBus.Subscribe<GameStateChanged>(OnState);
            RenderPipelineManager.beginCameraRendering += BeforeCamera;
            RenderPipelineManager.endCameraRendering += AfterCamera;
        }
        bool Playing => GameManager.HasInstance && GameManager.Instance.IsPlaying;
        void OnScore(ScoreChanged e) { if (Playing && e.Delta < 0) _pending = true; }
        void OnDrain(BossDrainApplied e) { if (Playing && e.Amount > 0) { _pending = true; _boss = true; } }
        void OnState(GameStateChanged e)
        {
            if (e.Current == GameState.Ready || e.Current == GameState.GameOver) Clear();
        }
        void LateUpdate()
        {
            if (GameManager.HasInstance && GameManager.Instance.State == GameState.Paused) return;
            if (_pending)
            {
                // 한 프레임의 ScoreChanged/Drain을 한 번으로 합성. 연속 손실은 상한 내에서만 갱신한다.
                float strength = _boss ? 1f : .65f;
                if (_age < 0f || _age > .15f || strength > _strength) { _age = 0f; _strength = strength; }
                _pending = _boss = false;
                EnsureView();
            }
            if (_age < 0f) return;
            _age += Time.deltaTime;
            if (_age >= duration) { Clear(); return; }
            float t = _age / duration;
            float envelope = Mathf.Min(1f, t / .1f) * Mathf.Pow(1f - t, 1.5f);
            float blackout = 1f - Mathf.Clamp01(AudienceMemberActor.SilhouetteBlend);
            _edge.color = new Color(.88f, .05f, .08f, edgeOpacity * envelope * _strength * blackout);
        }
        void EnsureView()
        {
            if (_edge != null) return;
            _canvas = new GameObject("ScoreLossScreen", typeof(RectTransform), typeof(Canvas));
            var canvas = _canvas.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = -1000; // 모든 무대 위, 카드/텍스트/팝업 아래. 입력은 받지 않는다.
            var graphic = new GameObject("SoftRedBorder", typeof(RectTransform), typeof(CanvasRenderer));
            graphic.transform.SetParent(_canvas.transform, false);
            var rect = (RectTransform)graphic.transform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            _edge = graphic.AddComponent<DamageEdgeGraphic>();
            _edge.raycastTarget = false; _edge.color = Color.clear;
        }
        void BeforeCamera(ScriptableRenderContext context, Camera camera)
        {
            if (_age < 0f || camera != Camera.main || AudienceMemberActor.SilhouetteBlend > .5f) return;
            RestoreOffset();
            float t = _age / duration;
            float envelope = Mathf.Clamp01(1f - t / .45f);
            _offset = new Vector3(Mathf.Sin(_age * 91f), Mathf.Sin(_age * 117f) * .6f, 0)
                * (shakeWorldDistance * envelope * _strength);
            _appliedCamera = camera;
            camera.transform.position += _offset;
        }
        void AfterCamera(ScriptableRenderContext context, Camera camera)
        { if (camera == _appliedCamera) RestoreOffset(); }
        void RestoreOffset()
        {
            if (_appliedCamera != null) _appliedCamera.transform.position -= _offset;
            _appliedCamera = null; _offset = Vector3.zero;
        }
        void Clear()
        {
            _pending = _boss = false; _age = -1f;
            RestoreOffset();
            if (_edge != null) _edge.color = Color.clear;
        }
        void OnDisable()
        {
            EventBus.Unsubscribe<ScoreChanged>(OnScore);
            EventBus.Unsubscribe<BossDrainApplied>(OnDrain);
            EventBus.Unsubscribe<GameStateChanged>(OnState);
            RenderPipelineManager.beginCameraRendering -= BeforeCamera;
            RenderPipelineManager.endCameraRendering -= AfterCamera;
            Clear();
            if (_active == this) _active = null;
            if (_canvas != null) Destroy(_canvas);
        }
    }

    public sealed class DamageEdgeGraphic : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect r = rectTransform.rect;
            Vector2[] corners = { new Vector2(-1,-1), new Vector2(-1,1), new Vector2(1,1), new Vector2(1,-1) };
            for (int ring = 0; ring < 3; ring++)
            {
                float size = ring == 0 ? 1f : ring == 1 ? .9f : .72f;
                Color c = color; c.a *= ring == 0 ? 1f : ring == 1 ? .3f : 0f;
                for (int i = 0; i < 4; i++)
                    mesh.AddVert(r.center + Vector2.Scale(corners[i], r.size * (.5f * size)), c, Vector2.zero);
            }
            for (int ring = 0; ring < 2; ring++) for (int i = 0; i < 4; i++)
            {
                int a = ring * 4 + i, b = ring * 4 + (i + 1) % 4, c = a + 4, d = b + 4;
                mesh.AddTriangle(a, b, c); mesh.AddTriangle(b, d, c);
            }
        }
    }
}
