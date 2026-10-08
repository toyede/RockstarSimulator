using GameJamKit;
using UnityEngine;

namespace ContextStage
{
    /// <summary>소나기 이벤트의 표시만 담당. 비 2층·바닥 물방울을 재사용하며 카드/UI에는 비를 그리지 않는다.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(RainShowerRule))]
    public sealed class RainShowerVFX : MonoBehaviour
    {
        const string EventId = "festival_rain_shower";
        ParticleSystem _back, _front, _splashes;
        RainShowerRule _rule;
        AudienceRosterPresenter _presenter;
        float _alpha, _emission, _splashEmission;
        bool _raining;
        Vector3 _center;
        float _width = 16;
        public int ParticleCount => (_back != null ? _back.particleCount : 0) + (_front != null ? _front.particleCount : 0) + (_splashes != null ? _splashes.particleCount : 0);

        void OnEnable()
        {
            _rule = GetComponent<RainShowerRule>();
            _presenter = FindFirstObjectByType<AudienceRosterPresenter>();
            EventBus.Subscribe<StageEventStarted>(OnStart);
            EventBus.Subscribe<StageEventResolved>(OnEnd);
            EventBus.Subscribe<RainAudienceProtected>(OnProtected);
            EventBus.Subscribe<GameStateChanged>(OnState);
            EventBus.Subscribe<StageRuntimeApplied>(OnStage);
        }
        void OnDisable()
        {
            EventBus.Unsubscribe<StageEventStarted>(OnStart);
            EventBus.Unsubscribe<StageEventResolved>(OnEnd);
            EventBus.Unsubscribe<RainAudienceProtected>(OnProtected);
            EventBus.Unsubscribe<GameStateChanged>(OnState);
            EventBus.Unsubscribe<StageRuntimeApplied>(OnStage);
            Clear();
        }
        void OnStart(StageEventStarted e)
        {
            if (e.EventId != EventId) return;
            Clear();
            var cam = Camera.main;
            _center = cam != null ? new Vector3(cam.transform.position.x, cam.transform.position.y, 0) : Vector3.zero;
            _width = cam != null && cam.orthographic ? cam.orthographicSize * cam.aspect * 2 : 16;
            if (_back == null) _back = Create("Rain_Back", 64, "Default", 1);
            if (_front == null) _front = Create("Rain_Front", 32, "Effects", 1);
            if (_splashes == null) _splashes = Create("Rain_Floor", 12, "Effects", 0);
            _raining = true;
        }
        void OnEnd(StageEventResolved e) { if (e.EventId == EventId) _raining = false; }
        void OnProtected(RainAudienceProtected e) { if (_raining) _presenter?.PlayRainProtection(); }
        void OnStage(StageRuntimeApplied e) => Clear();
        void OnState(GameStateChanged e)
        {
            if (e.Current == GameState.Ready || e.Current == GameState.GameOver) Clear();
            else
            {
                bool pause = e.Current == GameState.Paused;
                SetPaused(_back, pause); SetPaused(_front, pause); SetPaused(_splashes, pause);
            }
        }
        void SetPaused(ParticleSystem p, bool pause)
        {
            if (p == null || (p.particleCount == 0 && !_raining)) return;
            if (pause) p.Pause(); else p.Play();
        }
        void Update()
        {
            if (GameManager.HasInstance && GameManager.Instance.State == GameState.Paused) return;
            if (_raining && (_rule == null || !_rule.IsEventActive)) { Clear(); return; }
            _alpha = Mathf.MoveTowards(_alpha, _raining ? 1 : 0, Time.deltaTime / (_raining ? 0.3f : 0.4f));
            if (_alpha <= 0) return;
            _emission += Time.deltaTime * 42 * _alpha;
            while (_emission >= 1)
            {
                _emission--;
                Emit(_back, false);
                if (Random.value < 0.4f) Emit(_front, true);
            }
            _splashEmission += Time.deltaTime * 5 * _alpha;
            while (_splashEmission >= 1)
            {
                _splashEmission--;
                if (!_splashes.isPlaying) _splashes.Play();
                var floor = _center + new Vector3(Random.Range(-_width * 0.42f, _width * 0.42f), Random.Range(-2f, 0.5f), 0);
                for (int side=-1; side<=1; side+=2)
                    _splashes.Emit(new ParticleSystem.EmitParams { position=floor+Vector3.right*side*0.04f,
                        velocity=new Vector3(side*0.35f,0.25f,0),startLifetime=0.23f,startSize3D=new Vector3(0.035f,0.08f,1),
                        startColor=new Color(0.8f,0.85f,0.9f,0.28f*_alpha),rotation=side*35 },1);
            }
        }
        ParticleSystem Create(string label, int capacity, string layer, int order)
        {
            var go = new GameObject(label);
            go.transform.SetParent(transform, false);
            var p = go.AddComponent<ParticleSystem>();
            p.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = p.main;
            main.playOnAwake = false; main.loop = false; main.duration = 2;
            main.maxParticles = capacity; main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startSpeed = 0; main.startSize3D = true;
            var emission = p.emission; emission.enabled = false;
            var shape = p.shape; shape.enabled = false;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0.7f, 0.65f), new GradientAlphaKey(0, 1) });
            var fade = p.colorOverLifetime; fade.enabled = true; fade.color = gradient;
            var renderer = p.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = Resources.Load<Material>("Effects/AudienceWarningUnlit");
            renderer.sortingLayerName = layer; renderer.sortingOrder = order;
            p.Play();
            return p;
        }
        void Emit(ParticleSystem p, bool front)
        {
            if (p == null) return;
            if (!p.isPlaying) p.Play();
            float speed = front ? 8 : 6;
            var e = new ParticleSystem.EmitParams {
                position = _center + new Vector3(Random.Range(-_width * 0.55f, _width * 0.55f), Random.Range(3, 5), 0),
                velocity = new Vector3(-0.5f, -speed, 0), startLifetime = 1.4f,
                startSize3D = new Vector3(front ? 0.025f : 0.018f, front ? 0.23f : 0.16f, 1),
                startColor = new Color(0.8f, 0.85f, 0.9f, (front ? 0.3f : 0.2f) * _alpha), rotation = -4
            };
            p.Emit(e, 1);
        }
        public void Clear()
        {
            _raining = false; _alpha = _emission = _splashEmission = 0;
            if (_back != null) _back.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (_front != null) _front.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (_splashes != null) _splashes.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }
}
