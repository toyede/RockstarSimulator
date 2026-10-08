using System.Collections;
using System.Collections.Generic;
using GameJamKit;
using UnityEngine;

namespace ContextStage
{
    /// <summary>
    /// 라이벌 무대 (화면 기획 2026-09-18 "보스 스테이지(보스팀)"). 우리 무대 **위쪽** 구역에 놓인다.
    ///
    /// - 위쪽 가운데 단상 + LUX//FAUNA 듀오(duoFrames 프레임 교대, 없으면 Square 두 개)
    /// - 그 아래로 라이벌 팬이 흩어져 서 있다. 관객 프리팹(AudienceMember)과 같은 성향별 대기 애니메이션을 쓰되
    ///   어둡게 틴트한다. 뒷줄일수록 작고, 가장 뒷줄은 우리 화면 위쪽 가장자리에 걸쳐 "보스 팀 관객이 어둡고 작게" 보인다
    /// - 팬 수는 룰(BossFanBalanceChanged)과 동기화. 이동(BossFanMoved)은 우리 → 라이벌이면 아래(우리 무대)에서 걸어 올라오고,
    ///   라이벌 → 우리면 아래로 걸어 내려간다
    /// - 패턴 예고 중 듀오 점멸, REVENGE 진입 시 LED 진홍
    /// 룰 참조 없이 EventBus 만 구독한다. 같은 오브젝트에 BossArenaLayout 이 있으면 그 구역 좌표를, 없으면 카메라 자리를 쓴다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RivalStagePlaceholder : MonoBehaviour
    {
        [Header("단상·듀오 (구역 중심 기준 로컬)")]
        [SerializeField] Vector2 platformCenter = new Vector2(0f, 2.3f);
        [SerializeField] Vector2 platformSize = new Vector2(6.5f, 1.2f);
        [SerializeField] float duoSpacing = 2.4f;
        [SerializeField] float duoSize = 0.9f;
        [SerializeField] Sprite[] duoFrames;
        [SerializeField, Min(0.05f)] float duoFrameInterval = 0.45f;
        [SerializeField, Tooltip("듀오 일러스트 배율 (원본 3유닛 높이). 단상 위에서 화면 위쪽 끝에 닿지 않는 최대가 0.65")] float duoSpriteScale = 0.65f;
        [SerializeField, Tooltip("단상 위 기본 자리에서의 추가 오프셋")] Vector2 duoSpriteOffset;
        [SerializeField, Tooltip("패턴 예고 점멸 때 스프라이트에 입힐 색")] Color duoFlashColor = new Color32(0xFF, 0xB0, 0xC8, 0xFF);

        [Header("라이벌 팬 (구역 중심 기준 로컬)")]
        [SerializeField, Tooltip("앞줄(가장 아래, 0번 줄) y. 구역 간격이 8 이면 -4.2 는 우리 화면 위쪽(+3.8)에 들어와 우리 관객 뒤에 어둡게 보인다")] float fanFrontY = -4.2f;
        [SerializeField, Tooltip("뒷줄(단상 바로 앞) y. 앞줄부터 이 높이까지 3줄 간격으로 채우고, 그 뒤 줄은 여기에 모인다")] float fanBackY = 1.0f;
        [SerializeField, Min(1), Tooltip("한 줄 인원. 5면 시작 팬 14명이 3줄로 퍼진다")] int fansPerRow = 5;
        [SerializeField, Tooltip("가로 퍼짐 폭")] float fanSpreadWidth = 13f;
        [SerializeField, Tooltip("가장 뒷줄(단상 앞) 크기 — 관객 프리팹 기준 배율")] float fanScaleBack = 0.55f;
        [SerializeField, Tooltip("가장 앞줄(우리 화면에 걸치는 줄) 크기")] float fanScaleFront = 0.4f;
        [SerializeField, Range(0f, 1f), Tooltip("가로·세로 흐트러짐")] float fanJitter = 0.45f;
        [SerializeField] int fanSeed = 1207;
        [SerializeField, Tooltip("보스 팀 관객은 어둡게")] Color fanTint = new Color32(0x4E, 0x46, 0x5C, 0xFF);
        [SerializeField, Min(0.05f)] float fanWalkDuration = 0.9f;
        [SerializeField, Tooltip("우리 무대 관객(5)보다 뒤, 배경·조명(0·1)보다 앞. 줄마다 +1")] int fanSortingOrder = 2;
        [SerializeField, Tooltip("관객 프리팹이 없을 때(Square) 크기")] float fanSquareSize = 0.45f;

        [Header("정렬")]
        [SerializeField] int sortingOrder = 1;
        [SerializeField] string sortingLayer = "Default";

        [Header("색")]
        [SerializeField] Color platformColor = new Color32(0x3E, 0x35, 0x46, 0xFF);
        [SerializeField] Color ledColor = new Color32(0xB0, 0x3C, 0xFF, 0xFF);
        [SerializeField] Color revengeLedColor = new Color32(0xE0, 0x20, 0x40, 0xFF);
        [SerializeField] Color duoAColor = new Color32(0xF0, 0x4F, 0x78, 0xFF);
        [SerializeField] Color duoBColor = new Color32(0x30, 0xE1, 0xB9, 0xFF);
        [SerializeField] Color fanColor = new Color32(0xC7, 0xDC, 0xD0, 0xB0);
        [Header("보스 HP 공통 관객 상태 (표시 전용)")]
        [SerializeField, Range(0, 1)] float calmHpThreshold = 0.33f;
        [SerializeField, Range(0, 1)] float hypeHpThreshold = 0.66f;

        sealed class Fan
        {
            public SpriteRenderer Renderer;
            public SpriteAnimationPlayer Player;
            public Vector3 Slot;
            public float Scale;
            public Color BaseColor;
            public CrowdPreference Preference;
            public int Seed;
            public float Fade = 1f;
            public bool Walking;
            public CrowdMotionEvaluator.Variance Variance;
        }

        Sprite _square;
        Transform _root;
        SpriteRenderer _platform;
        SpriteRenderer _led;
        SpriteRenderer _duoA;
        SpriteRenderer _duoB;
        SpriteRenderer _duoSprite;
        float _duoFrameTimer;
        int _duoFrame;
        readonly List<Fan> _fans = new List<Fan>();
        readonly List<Fan> _outgoing = new List<Fan>();
        AudienceEngagementStage _audienceStage;
        CrowdMotionProfile _motionFrom, _motionTo;
        float _motionBlend = 1f;
        public AudienceEngagementStage AudienceStage => _audienceStage;
        AudienceMemberActor _fanVisualSource;
        Vector3 _ourStageAnchor;
        Coroutine _blinkRoutine;
        bool _built;
        bool _revenge;
        RivalArrivalVFX _arrivalVfx;
        [SerializeField, Tooltip("StageShowDirector가 LED/조명 큐를 맡는다. 팬·듀오 애니메이션은 유지")]
        bool externalLightingShow;
        [SerializeField] StageShowDirector lightingShow;
        bool UsesExternalShow => externalLightingShow && lightingShow != null && lightingShow.isActiveAndEnabled && lightingShow.HasRivalShow;

        bool UseDuoSprite => duoFrames != null && duoFrames.Length > 0 && duoFrames[0] != null;
        Color DuoBaseA => UseDuoSprite ? Color.white : duoAColor;
        Color DuoBaseB => UseDuoSprite ? Color.white : duoBColor;
        Color DuoFlash => UseDuoSprite ? duoFlashColor : Color.white;

        void OnEnable()
        {
            EventBus.Subscribe<BossFanBalanceChanged>(OnBalance);
            EventBus.Subscribe<BossFanMoved>(OnFanMoved);
            EventBus.Subscribe<BossRevengeChanged>(OnRevenge);
            EventBus.Subscribe<BossPatternStarted>(OnPatternStarted);
            EventBus.Subscribe<BossPatternResolved>(OnPatternResolved);
            EventBus.Subscribe<GameStateChanged>(OnGameStateChanged);
        }

        void OnDisable()
        {
            StopAllCoroutines();
            _blinkRoutine = null;
            EventBus.Unsubscribe<BossFanBalanceChanged>(OnBalance);
            EventBus.Unsubscribe<BossFanMoved>(OnFanMoved);
            EventBus.Unsubscribe<BossRevengeChanged>(OnRevenge);
            EventBus.Unsubscribe<BossPatternStarted>(OnPatternStarted);
            EventBus.Unsubscribe<BossPatternResolved>(OnPatternResolved);
            EventBus.Unsubscribe<GameStateChanged>(OnGameStateChanged);
            if (_built) ResetVisual();
        }

        void Update()
        {
            if (_root == null || !_root.gameObject.activeInHierarchy) return;
            if (_led != null) _led.enabled = !UsesExternalShow;
            if (_platform != null) _platform.enabled = !UsesExternalShow;

            // 듀오 프레임 교대
            if (_duoSprite != null && duoFrames.Length > 1)
            {
                _duoFrameTimer += Time.deltaTime;
                if (_duoFrameTimer >= duoFrameInterval)
                {
                    _duoFrameTimer = 0f;
                    _duoFrame = (_duoFrame + 1) % duoFrames.Length;
                    if (duoFrames[_duoFrame] != null) _duoSprite.sprite = duoFrames[_duoFrame];
                }
            }

            // 팬 대기 애니메이션
            for (int i = 0; i < _fans.Count; i++)
                _fans[i].Player?.Tick(Time.deltaTime);
            for (int i = 0; i < _outgoing.Count; i++)
                _outgoing[i].Player?.Tick(Time.deltaTime);
            _motionBlend = Mathf.Min(1, _motionBlend + Time.deltaTime / 0.25f);
        }

        void LateUpdate()
        {
            for (int i = 0; i < _fans.Count; i++) ApplyFan(_fans[i]);
            for (int i = 0; i < _outgoing.Count; i++) ApplyFan(_outgoing[i]);
        }

        void ApplyFan(Fan fan)
        {
            if (fan.Renderer == null) return;
            Color color = Color.Lerp(fan.BaseColor, Color.black, 1f - BossCameraDirector.RivalViewBlend);
            color = Color.Lerp(color, AudienceMemberActor.SilhouetteColor, AudienceMemberActor.SilhouetteBlend);
            color.a = fan.BaseColor.a * fan.Fade;
            fan.Renderer.color = color;
            if (fan.Walking || _motionTo == null) return;
            float clock = (Time.time + fan.Seed * 0.173f) * fan.Variance.SpeedMultiplier;
            CrowdMotionEvaluator.EvaluateBlended(_motionFrom, _motionTo, _motionBlend, clock,
                out float bob, out float sway, out float squash);
            var pose = fan.Renderer.transform;
            pose.localPosition = fan.Slot + Vector3.up * bob;
            pose.localRotation = Quaternion.Euler(0, 0, sway * fan.Variance.Flip);
            pose.localScale = new Vector3(fan.Scale * (1 + squash * 0.5f), fan.Scale * (1 - squash), 1);
        }

        void SetAudienceStage(float hpRatio)
        {
            AudienceEngagementStage stage = hpRatio > hypeHpThreshold ? AudienceEngagementStage.Excited
                : hpRatio > calmHpThreshold ? AudienceEngagementStage.Middle : AudienceEngagementStage.Calm;
            var source = FanVisualSource();
            if (stage == _audienceStage && _motionTo != null) return;
            _audienceStage = stage;
            _motionFrom = _motionTo ?? source?.PresentationMotion(stage);
            _motionTo = source?.PresentationMotion(stage);
            _motionBlend = _motionFrom == null ? 1 : 0;
            foreach (var fan in _fans) SetFanVisual(fan, source);
            foreach (var fan in _outgoing) SetFanVisual(fan, source);
        }

        void SetFanVisual(Fan fan, AudienceMemberActor source)
        {
            if (source == null || fan.Renderer == null ||
                !source.TryGetPresentationVisual(fan.Preference, _audienceStage, fan.Seed,
                    out SpriteAnimationClip clip, out Sprite still, out Color original)) return;
            fan.BaseColor = original;
            fan.Renderer.sharedMaterial = source.CharacterRenderer.sharedMaterial;
            if (clip != null && clip.IsValid)
            {
                if (fan.Player == null) fan.Player = new SpriteAnimationPlayer(fan.Renderer);
                fan.Player.Play(clip, restart: true);
            }
            else
            {
                fan.Player?.Stop();
                if (still != null) fan.Renderer.sprite = still;
            }
        }

        // ---------------- 이벤트 ----------------

        void OnBalance(BossFanBalanceChanged e)
        {
            EnsureBuilt();
            _root.gameObject.SetActive(true);
            SetAudienceStage(e.RivalRatio);
            SyncFanCount(e.Rival);
        }

        void OnFanMoved(BossFanMoved e)
        {
            EnsureBuilt();
            if (e.ToRival)
            {
                for (int i = 0; i < e.Count; i++) StartCoroutine(WalkIn(_fans.Count));
            }
            else
            {
                for (int i = 0; i < e.Count && _fans.Count > 0; i++)
                {
                    Fan fan = _fans[_fans.Count - 1];
                    _fans.RemoveAt(_fans.Count - 1);
                    _outgoing.Add(fan);
                    StartCoroutine(WalkOut(fan));
                }
            }
        }

        void OnRevenge(BossRevengeChanged e)
        {
            EnsureBuilt();
            _revenge = e.Active;
            if (!UsesExternalShow) _led.color = _revenge ? revengeLedColor : ledColor;
        }

        void OnPatternStarted(BossPatternStarted e)
        {
            EnsureBuilt();
            if (UsesExternalShow) { _led.enabled = false; return; }
            if (_blinkRoutine != null) StopCoroutine(_blinkRoutine);
            _blinkRoutine = StartCoroutine(Blink(e.Duration));
        }

        void OnPatternResolved(BossPatternResolved e)
        {
            EnsureBuilt();
            if (_blinkRoutine != null)
            {
                StopCoroutine(_blinkRoutine);
                _blinkRoutine = null;
            }
            _duoA.color = DuoBaseA;
            _duoB.color = DuoBaseB;
            if (!UsesExternalShow) _led.color = _revenge ? revengeLedColor : ledColor;
        }

        void OnGameStateChanged(GameStateChanged e)
        {
            if (!_built) return;
            if (e.Current == GameState.Ready) ResetVisual();
        }

        // ---------------- 팬 ----------------

        /// <summary>이동 연출이 끝난 뒤에도 수가 맞도록 룰 값에 맞춘다 (연출 중인 팬은 다음 동기화에서 정리).</summary>
        void SyncFanCount(int target)
        {
            while (_fans.Count > target)
            {
                Fan extra = _fans[_fans.Count - 1];
                _fans.RemoveAt(_fans.Count - 1);
                if (extra?.Renderer != null) Destroy(extra.Renderer.gameObject);
            }
            while (_fans.Count < target) _fans.Add(CreateFan(_fans.Count));
        }

        /// <summary>줄 순서: 0번 줄이 앞줄(가장 아래, 우리 무대 쪽), 줄이 늘수록 위(단상 쪽)로 올라간다. 앞줄부터 채워야 우리 화면에 먼저 걸친다.</summary>
        int RowOf(int index) => index / Mathf.Max(1, fansPerRow);

        const int FanRowSteps = 3; // 앞줄 → 뒷줄까지 3칸

        Vector3 FanSlotFor(int index, out float scale, out int order)
        {
            int row = RowOf(index);
            int col = index % fansPerRow;
            int rowCount = Mathf.Max(1, fansPerRow);
            float rowT = Mathf.Clamp01(row / (float)FanRowSteps); // 0 = 앞줄(우리 화면 쪽), 1 = 뒷줄(단상 앞)

            var random = new System.Random(fanSeed + index * 7919);
            float jx = ((float)random.NextDouble() - 0.5f) * fanJitter * (fanSpreadWidth / rowCount);
            float jy = ((float)random.NextDouble() - 0.5f) * fanJitter * 0.6f;
            float stagger = (row % 2 == 1) ? (fanSpreadWidth / rowCount) * 0.5f : 0f;

            float x = (col - (rowCount - 1) * 0.5f) * (fanSpreadWidth / rowCount) + stagger + jx;
            float y = Mathf.Lerp(fanFrontY, fanBackY, rowT) + jy;
            scale = Mathf.Lerp(fanScaleFront, fanScaleBack, rowT);
            order = fanSortingOrder + Mathf.Clamp(2 - row, 0, 2); // 앞줄(아래)이 뒷줄을 가린다, 관객(5) 아래로만
            return new Vector3(x, y, 0f);
        }

        Fan CreateFan(int index)
        {
            Vector3 slot = FanSlotFor(index, out float scale, out int order);
            var fan = new Fan { Slot = slot, Scale = scale, Seed = index,
                Preference = (CrowdPreference)(index % 3), Variance = CrowdMotionEvaluator.MakeVariance(index + fanSeed, 0.2f) };

            var go = new GameObject($"RivalFan_{index}");
            go.transform.SetParent(_root, false);
            go.transform.localPosition = slot;
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sortingLayerName = sortingLayer;
            renderer.sortingOrder = order;
            fan.Renderer = renderer;

            var preference = (CrowdPreference)(index % 3);
            AudienceMemberActor source = FanVisualSource();
            if (source != null && source.TryGetPresentationVisual(preference, _audienceStage, index,
                out SpriteAnimationClip clip, out Sprite still, out Color original))
            {
                go.transform.localScale = Vector3.one * scale;
                if (index % 2 == 1) renderer.flipX = true;
                fan.BaseColor = original;
                renderer.sharedMaterial = source.CharacterRenderer.sharedMaterial;
                if (clip != null && clip.IsValid)
                {
                    fan.Player = new SpriteAnimationPlayer(renderer);
                    fan.Player.Play(clip, restart: true);
                }
                else
                {
                    renderer.sprite = still;
                }
            }
            else
            {
                renderer.sprite = _square;
                go.transform.localScale = Vector3.one * fanSquareSize;
                fan.BaseColor = fanColor;
            }
            ApplyFan(fan);
            return fan;
        }

        /// <summary>관객 프리팹(AudienceRosterPresenter.MemberPrefab)의 대기 애니메이션을 빌려 쓴다. 없으면 Square.</summary>
        AudienceMemberActor FanVisualSource()
        {
            if (_fanVisualSource != null) return _fanVisualSource;
            AudienceRosterPresenter presenter = FindFirstObjectByType<AudienceRosterPresenter>(FindObjectsInactive.Include);
            _fanVisualSource = presenter != null ? presenter.MemberPrefab : null;
            return _fanVisualSource;
        }

        IEnumerator WalkIn(int slotIndex)
        {
            Fan fan = CreateFan(slotIndex);
            _fans.Add(fan);
            Vector3 to = fan.Renderer.transform.position;
            Vector3 from = new Vector3(to.x, _ourStageAnchor.y + 3.5f, 0f); // 우리 무대 위쪽에서 올라온다
            yield return Walk(fan, from, to, false);
            if (fan.Renderer != null)
            {
                if (_arrivalVfx == null) _arrivalVfx = gameObject.AddComponent<RivalArrivalVFX>();
                _arrivalVfx.Emit(fan.Renderer.bounds.center + Vector3.down * fan.Renderer.bounds.extents.y, fan.Scale);
            }
        }

        IEnumerator WalkOut(Fan fan)
        {
            if (fan?.Renderer == null) yield break;
            Vector3 from = fan.Renderer.transform.position;
            Vector3 to = new Vector3(from.x, _ourStageAnchor.y + 3.5f, 0f);
            yield return Walk(fan, from, to, true);
            if (fan.Renderer != null) Destroy(fan.Renderer.gameObject);
            _outgoing.Remove(fan);
        }

        IEnumerator Walk(Fan fan, Vector3 from, Vector3 to, bool fadeOut)
        {
            float elapsed = 0f;
            fan.Walking = true;
            float duration = Mathf.Max(0.05f, fanWalkDuration * (0.9f + Mathf.Abs(fan.Slot.x % 1f) * 0.2f));
            if (fan.Renderer != null) fan.Renderer.transform.position = from;
            while (elapsed < duration)
            {
                if (fan.Renderer == null) yield break;
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                float bob = Mathf.Abs(Mathf.Sin(t * Mathf.PI * 4f)) * 0.12f;
                fan.Renderer.transform.position = Vector3.Lerp(from, to, t) + new Vector3(0f, bob, 0f);
                fan.Fade = fadeOut ? 1f - t * 0.6f : 1f;
                yield return null;
            }
            if (fan.Renderer != null) fan.Renderer.transform.position = to;
            fan.Walking = false;
        }

        // ---------------- 연출 ----------------

        IEnumerator Blink(float duration)
        {
            float elapsed = 0f;
            bool on = false;
            Color baseLed = _revenge ? revengeLedColor : ledColor;
            while (elapsed < duration)
            {
                on = !on;
                _duoA.color = on ? DuoFlash : DuoBaseA;
                _duoB.color = on ? DuoFlash : DuoBaseB;
                _led.color = on ? Color.white : baseLed;
                yield return new WaitForSeconds(0.25f);
                elapsed += 0.25f;
            }
            _duoA.color = DuoBaseA;
            _duoB.color = DuoBaseB;
            _led.color = baseLed;
            _blinkRoutine = null;
        }

        void ResetVisual()
        {
            if (_arrivalVfx != null) _arrivalVfx.Clear();
            StopAllCoroutines();
            _blinkRoutine = null;
            if (_root != null)
                foreach (Transform child in _root)
                    if (child.name.StartsWith("RivalFan_")) Destroy(child.gameObject);
            _revenge = false;
            _platform.color = platformColor;
            _led.color = ledColor;
            _led.enabled = !UsesExternalShow;
            _duoA.color = DuoBaseA;
            _duoB.color = DuoBaseB;
            _fans.Clear();
            _outgoing.Clear();
            _audienceStage = AudienceEngagementStage.Calm;
            _motionFrom = _motionTo = null;
            _root.gameObject.SetActive(false);
        }

        // ---------------- 생성 ----------------

        void EnsureBuilt()
        {
            if (_built) return;
            _built = true;

            _square = SquareSprite.Get();
            Camera camera = Camera.main;
            Vector3 origin = camera != null ? new Vector3(camera.transform.position.x, camera.transform.position.y, 0f) : Vector3.zero;
            _ourStageAnchor = origin;

            BossArenaLayout arena = GetComponent<BossArenaLayout>();
            if (arena != null)
            {
                arena.Show();
                origin = arena.AnchorOf(BossZone.RivalStage);
                _ourStageAnchor = arena.AnchorOf(BossZone.OurStage);
            }

            var rootObject = new GameObject("RivalStage");
            rootObject.transform.SetParent(transform, false);
            rootObject.transform.position = origin;
            _root = rootObject.transform;

            float platformTop = platformCenter.y + platformSize.y * 0.5f;
            _platform = CreateSquareRenderer("Platform", platformCenter, platformSize, platformColor, sortingOrder);

            if (UseDuoSprite)
            {
                // 듀오 일러스트 한 장 (프레임 교대). 발끝이 단상 위에 오도록 스프라이트 높이의 절반만큼 올린다
                Sprite first = duoFrames[0];
                float scale = Mathf.Max(0.05f, duoSpriteScale);
                float duoHeight = first.bounds.size.y * scale;
                Vector2 pos = new Vector2(platformCenter.x, platformTop + duoHeight * 0.5f) + duoSpriteOffset;
                _duoSprite = CreateSquareRenderer("Duo", pos, Vector2.one * scale, Color.white, sortingOrder + 2);
                _duoSprite.sprite = first;
                _duoA = _duoSprite;
                _duoB = _duoSprite;
                _duoFrame = 0;
                _duoFrameTimer = 0f;
                // LED 는 듀오 뒤, 머리 높이
                _led = CreateSquareRenderer("LED", new Vector2(platformCenter.x, platformTop + duoHeight * 0.75f), new Vector2(3.2f, 0.5f), ledColor, sortingOrder + 1);
            }
            else
            {
                _duoA = CreateSquareRenderer("Duo_Owl", platformCenter + new Vector2(-duoSpacing * 0.5f, platformSize.y * 0.5f + duoSize * 0.5f), Vector2.one * duoSize, duoAColor, sortingOrder + 1);
                _duoB = CreateSquareRenderer("Duo_Leopard", platformCenter + new Vector2(duoSpacing * 0.5f, platformSize.y * 0.5f + duoSize * 0.5f), Vector2.one * duoSize, duoBColor, sortingOrder + 1);
                _led = CreateSquareRenderer("LED", new Vector2(platformCenter.x, platformTop + duoSize + 0.5f), new Vector2(3.2f, 0.5f), ledColor, sortingOrder);
            }
        }

        SpriteRenderer CreateSquareRenderer(string name, Vector2 offset, Vector2 size, Color color, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root, false);
            go.transform.localPosition = new Vector3(offset.x, offset.y, 0f);
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = _square;
            renderer.color = color;
            renderer.sortingLayerName = sortingLayer;
            renderer.sortingOrder = order;
            return renderer;
        }

#if UNITY_EDITOR
        /// <summary>[에디터 셋업 전용] (팬 수는 룰과 동기화되므로 더는 쓰지 않는다)</summary>
        public void EditorSetFanPool(int count) { }
#endif
    }
}
