using GameJamKit;
using UnityEngine;

namespace ContextStage
{
    /// <summary>사건의 시작/실제 결과만 구독하는 픽셀 버스트. 룰·보상·시간은 소유하지 않는다.</summary>
    [DisallowMultipleComponent]
    public sealed class StageEventPresentationVFX : MonoBehaviour
    {
        [SerializeField] Sprite restoreSmokeSprite;
        const int PieceCapacity = 76;
        ParticleSystem _pieces, _notes, _smoke;
        Material _material, _smokeMaterial;
        Texture2D _atlas;
        Sprite _square, _note;
        Camera _camera;
        BlackoutPresentation _blackoutPresentation;
        readonly System.Random _random = new System.Random(19371);
        string _eventId;
        bool _blackout, _restorePending, _paused;
        static readonly Color Teal = new Color32(74, 222, 225, 255);
        static readonly Color Purple = new Color32(176, 120, 241, 255);
        static readonly Color Gold = new Color32(255, 206, 106, 255);
        static readonly Color Warning = new Color32(255, 135, 78, 255);
        public int ParticleCount => (_pieces != null ? _pieces.particleCount : 0) + (_notes != null ? _notes.particleCount : 0) + (_smoke != null ? _smoke.particleCount : 0);
        public int BurstCount { get; private set; }

        void OnEnable()
        {
            _camera = Camera.main;
            _blackoutPresentation = GetComponentInChildren<BlackoutPresentation>(true);
            EventBus.Subscribe<StageEventStarted>(OnStart);
            EventBus.Subscribe<StageEventResolved>(OnResult);
            EventBus.Subscribe<AudienceCrisisWarningStarted>(OnCrisis);
            EventBus.Subscribe<AudienceCrisisResolved>(OnCrisisResult);
            EventBus.Subscribe<AudienceCrisisCancelled>(OnCancel);
            EventBus.Subscribe<AudienceComebackStarted>(OnSupport);
            EventBus.Subscribe<AudienceComebackResolved>(OnSupportResult);
            EventBus.Subscribe<BossPatternResolved>(OnBossResult);
            EventBus.Subscribe<GameStateChanged>(OnState);
            EventBus.Subscribe<StageRuntimeApplied>(OnStage);
        }
        void OnDisable()
        {
            EventBus.Unsubscribe<StageEventStarted>(OnStart);
            EventBus.Unsubscribe<StageEventResolved>(OnResult);
            EventBus.Unsubscribe<AudienceCrisisWarningStarted>(OnCrisis);
            EventBus.Unsubscribe<AudienceCrisisResolved>(OnCrisisResult);
            EventBus.Unsubscribe<AudienceCrisisCancelled>(OnCancel);
            EventBus.Unsubscribe<AudienceComebackStarted>(OnSupport);
            EventBus.Unsubscribe<AudienceComebackResolved>(OnSupportResult);
            EventBus.Unsubscribe<BossPatternResolved>(OnBossResult);
            EventBus.Unsubscribe<GameStateChanged>(OnState);
            EventBus.Unsubscribe<StageRuntimeApplied>(OnStage);
            Clear();
        }
        void OnDestroy()
        {
            if (_material != null) Destroy(_material);
            if (_smokeMaterial != null) Destroy(_smokeMaterial);
            if (_square != null) Destroy(_square);
            if (_note != null) Destroy(_note);
            if (_atlas != null) Destroy(_atlas);
        }
        void OnStart(StageEventStarted e)
        {
            if (_eventId == e.EventId) return;
            Clear(); _eventId = e.EventId;
            if (e.EventId == "arena_blackout") { _blackout = true; return; }
            if (e.EventId == "festival_rain_shower") return; // 기존 비 시스템에서 버스트
            if (e.EventId == "arena_big_screen") Burst(32, Gold, false, true);
            else Burst(48, e.EventId == "stadium_contested_fan" ? Teal : Purple);
        }
        void OnResult(StageEventResolved e)
        {
            if (_eventId != e.EventId) return;
            _eventId = null;
            if (e.EventId == "arena_blackout") { _blackout = false; _restorePending = true; return; }
            if (e.EventId == "festival_rain_shower") return;
            Burst(e.Success ? 96 : 32, e.Success ? Gold : Warning, !e.Success);
        }
        bool IsBoss => StageRuntimeDirector.CurrentStage != null && StageRuntimeDirector.CurrentStage.IsBoss;
        void OnCrisis(AudienceCrisisWarningStarted e) { if (!IsBoss) Burst(48, Warning, true, true); }
        void OnCrisisResult(AudienceCrisisResolved e) { if (!IsBoss && e.RetainedCount > 0) RecoveryWave(); }
        void OnCancel(AudienceCrisisCancelled e) => Clear();
        void OnSupport(AudienceComebackStarted e) { if (!IsBoss) Burst(48, Teal, false, false, true); }
        void OnSupportResult(AudienceComebackResolved e) { if (e.JoinedCount > 0 && !IsBoss) RecoveryWave(); }
        void OnBossResult(BossPatternResolved e) { if (IsBoss) Burst(e.Success ? 48 : 24, e.Success ? Teal : Warning, !e.Success); }
        void OnStage(StageRuntimeApplied e) { Clear(); _camera = Camera.main; }
        void OnState(GameStateChanged e)
        {
            if (e.Current == GameState.Ready || e.Current == GameState.GameOver) { Clear(); return; }
            _paused = e.Current == GameState.Paused;
            SetPause(_pieces); SetPause(_notes); SetPause(_smoke);
        }
        void SetPause(ParticleSystem system)
        {
            if (system == null) return;
            if (_paused) system.Pause(); else if (system.particleCount > 0) system.Play();
        }
        void Update()
        {
            if (_restorePending && !_paused &&
                (_blackoutPresentation == null || !_blackoutPresentation.IsRestoring) &&
                AudienceMemberActor.SilhouetteBlend < .02f)
            {
                _restorePending = false;
                Burst(32, new Color(.65f, .85f, 1f), false, true);
                RestoreSmoke();
            }
        }
        float R(float low, float high) => Mathf.Lerp(low, high, (float)_random.NextDouble());
        Vector3 Point(float x, float y)
        {
            var p = _camera.ViewportToWorldPoint(new Vector3(x, y, -_camera.transform.position.z));
            p.z = 0; return p;
        }
        void Burst(int count, Color accent, bool falling = false, bool streaks = false, bool notes = false)
        {
            if (_paused || _blackout || AudienceMemberActor.SilhouetteBlend > .05f) return;
            if (_camera == null) _camera = Camera.main;
            if (_camera == null) return;
            EnsureSystems();
            if (_pieces == null) return;
            BurstCount++;
            var system = notes ? _notes : _pieces;
            int available = Mathf.Max(0, (notes ? 32 : PieceCapacity) - system.particleCount);
            count = Mathf.Min(count, available);
            system.Play();
            for (int i = 0; i < count; i++)
            {
                bool left = (i & 1) == 0;
                // 중앙 관객·미션·손패를 가로지르지 않는 가장자리 캐논.
                var origin = Point(left ? R(.07f, .16f) : R(.84f, .93f), falling ? R(.72f, .85f) : R(.65f, .77f));
                float outward = left ? -1f : 1f;
                var velocity = new Vector3(outward * R(.6f, streaks ? 5f : 2.5f), falling ? -R(1f, 3f) : R(2.4f, 5.5f), 0);
                float size = R(.12f, notes ? .4f : .23f);
                Color color = i % 4 == 0 ? new Color(1f, .96f, .83f, .9f) : accent;
                system.Emit(new ParticleSystem.EmitParams {
                    position = origin, velocity = velocity, startLifetime = R(.4f, .85f),
                    startSize3D = notes ? Vector3.one * size : new Vector3(size, size * (streaks ? 4f : i % 3 == 0 ? 2.5f : 1f), 1),
                    startColor = color, rotation = R(-60f, 60f), angularVelocity = R(-220f, 220f)
                }, 1);
            }
        }
        bool PrepareEffect()
        {
            if (_paused || _blackout || AudienceMemberActor.SilhouetteBlend > .05f) return false;
            if (_camera == null) _camera = Camera.main;
            if (_camera == null) return false;
            EnsureSystems();
            return _pieces != null;
        }
        void RecoveryWave()
        {
            if (!PrepareEffect()) return;
            int count = Mathf.Min(32, PieceCapacity - _pieces.particleCount);
            if (count <= 0) return;
            BurstCount++;
            _pieces.Play();
            for (int i = 0; i < count; i++)
            {
                bool left = (i & 1) == 0;
                float arc = (i / 2) / 15f;
                _pieces.Emit(new ParticleSystem.EmitParams {
                    position = Point(left ? .06f + arc * .12f : .94f - arc * .12f, .40f + Mathf.Sin(arc * Mathf.PI) * .045f),
                    velocity = new Vector3(left ? 5f : -5f, .6f, 0),
                    startLifetime = .55f, startSize3D = new Vector3(.28f, .055f, 1),
                    startColor = new Color(Teal.r, Teal.g, Teal.b, .65f), rotation = left ? -12f : 12f
                }, 1);
            }
        }
        void RestoreSmoke()
        {
            if (!PrepareEffect() || _smoke == null) return;
            int count = Mathf.Min(6, 8 - _smoke.particleCount);
            _smoke.Play();
            for (int i = 0; i < count; i++)
            {
                bool left = (i & 1) == 0;
                _smoke.Emit(new ParticleSystem.EmitParams {
                    position = Point(left ? R(.10f,.18f) : R(.82f,.90f), R(.78f,.85f)),
                    velocity = new Vector3(left ? -.3f : .3f, .55f, 0),
                    startLifetime = R(.65f,.85f), startSize3D = new Vector3(1.2f, .45f, 1),
                    startColor = new Color(.72f,.84f,.95f,.18f), rotation = R(-10f,10f)
                }, 1);
            }
        }
        void EnsureSystems()
        {
            if (_pieces != null) return;
            var source = Resources.Load<Material>("Effects/AudienceWarningUnlit");
            if (source == null) return;
            _atlas = new Texture2D(16, 8, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, hideFlags = HideFlags.HideAndDontSave };
            var pixels = new Color32[128];
            string[] note = { "....##..", "....###.", "....#.##", "....#...", "....#...", "..###...", ".####...", "..##...." };
            for (int y = 0; y < 8; y++) for (int x = 0; x < 16; x++)
                pixels[y * 16 + x] = x < 8 || note[7-y][x-8] == '#' ? new Color32(255,255,255,255) : new Color32(0,0,0,0);
            _atlas.SetPixels32(pixels); _atlas.Apply();
            _square = Sprite.Create(_atlas, new Rect(0,0,8,8), Vector2.one * .5f, 8);
            _note = Sprite.Create(_atlas, new Rect(8,0,8,8), Vector2.one * .5f, 8);
            _material = new Material(source) { hideFlags = HideFlags.HideAndDontSave, mainTexture = _atlas };
            _pieces = Create("EventConfetti", PieceCapacity, _square); // 76 + 음표32 + 연기8 + UI12 = 128
            _notes = Create("EventNotes", 32, _note);
            if (restoreSmokeSprite != null)
            {
                _smokeMaterial = new Material(source) { hideFlags = HideFlags.HideAndDontSave, mainTexture = restoreSmokeSprite.texture };
                _smoke = Create("EventRestoreSmoke", 8, restoreSmokeSprite);
                _smoke.GetComponent<ParticleSystemRenderer>().sharedMaterial = _smokeMaterial;
                var main = _smoke.main; main.gravityModifier = 0;
            }
        }
        ParticleSystem Create(string label, int capacity, Sprite sprite)
        {
            var go = new GameObject(label); go.transform.SetParent(transform, false);
            var p = go.AddComponent<ParticleSystem>(); p.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = p.main; main.playOnAwake = false; main.loop = false; main.maxParticles = capacity;
            main.startSize3D = true; main.startSpeed = 0; main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = .35f;
            var emission = p.emission; emission.enabled = false;
            var shape = p.shape; shape.enabled = false;
            var sheet = p.textureSheetAnimation; sheet.enabled = true; sheet.mode = ParticleSystemAnimationMode.Sprites; sheet.AddSprite(sprite); sheet.frameOverTime = 0;
            var fade = p.colorOverLifetime; fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},
                new[]{new GradientAlphaKey(1,0),new GradientAlphaKey(.8f,.5f),new GradientAlphaKey(0,1)});
            fade.color = gradient;
            var renderer = go.GetComponent<ParticleSystemRenderer>(); renderer.sharedMaterial = _material;
            renderer.sortingLayerName = "Effects"; renderer.sortingOrder = 4;
            return p;
        }
        public void Clear()
        {
            _eventId = null; _blackout = _restorePending = _paused = false;
            if (_pieces != null) _pieces.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (_notes != null) _notes.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (_smoke != null) _smoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }
}
