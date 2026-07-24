using UnityEngine;

namespace ContextStage
{
    /// <summary>
    /// 관객 한 명. 상태(mood)가 바뀌면 스프라이트를 갈아끼우고, 매 프레임 움직임을 계산한다.
    ///
    /// 움직임은 애니메이터 없이 전부 절차적으로 만든다 (스프라이트 1장으로 3가지 상태를 표현하기 위함):
    ///   - 제자리 반동(bob) : 항상
    ///   - 좌우 흔들림(sway) : 항상
    ///   - 점프(jump)       : jumpHeight 가 0보다 큰 상태(High)에서만 → "방방 뛰는" 연출
    ///   - 착지 스쿼시      : 점프 무게감
    ///
    /// 관객마다 위상(phase)과 속도가 조금씩 달라 군무처럼 보이지 않는다.
    /// 상태 전환 시에는 수치를 moodBlendDuration 동안 섞어 툭 끊기지 않게 한다.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class CrowdMemberView : MonoBehaviour, ICrowdMoodReactor
    {
        [SerializeField, Tooltip("이 관객의 고유 번호. 상태별 스프라이트 세트에서 몇 번째를 쓸지 결정한다")]
        int variantSeed;

        [SerializeField, Tooltip("기본 크기 (스프라이트가 커서 보통 1보다 작다). 상태별 배율이 여기에 곱해진다")]
        float baseScale = 1f;

        SpriteRenderer _renderer;
        Vector3 _basePosition;

        // 상태 블렌드: from → to 로 _blend(0~1) 만큼 섞어서 평가한다
        CrowdMotionProfile _from, _to;
        CrowdMoodTier _fromTier, _toTier;
        float _blend = 1f, _blendSpeed;

        float _phase;        // 개체별 시작 위상 (군무 방지)
        float _speedMul = 1f;// 개체별 속도 배율
        float _flip = 1f;    // 좌우 반전

        /// <summary>프레임 애니메이션 재생기. 상태에 클립이 없으면 놀고 있는다.</summary>
        readonly SpriteAnimationPlayer _player = new SpriteAnimationPlayer();

        // ---------------- 외부에서 읽는 배치 정보 ----------------
        // 특별 관객이 "이 줄에 섞여 서려면" 어느 높이·크기·정렬 순서를 따라가야 하는지 알아야 한다.

        /// <summary>움직임을 빼고 원래 서 있는 자리 (부모 기준 로컬 좌표).</summary>
        public Vector3 HomeLocalPosition => _basePosition;

        /// <summary>이 관객이 속한 줄의 기본 크기 배율.</summary>
        public float BaseScale => baseScale;

        /// <summary>이 관객이 속한 줄의 정렬 순서.</summary>
        public int SortingOrder => _renderer != null ? _renderer.sortingOrder : 0;

        void Awake() => Initialize();

        void Initialize()
        {
            if (_renderer == null) _renderer = GetComponent<SpriteRenderer>();
            _player.Bind(_renderer);
            _basePosition = transform.localPosition;

            // 같은 seed 면 항상 같은 개성이 나오도록 결정적으로 계산한다 (재시작해도 배치가 튀지 않음)
            var rng = new System.Random(variantSeed * 7919 + 13);
            _phase = (float)rng.NextDouble() * 100f;
            _flip = rng.Next(2) == 0 ? 1f : -1f;
        }

        void OnEnable() => CrowdMoodDirector.Instance.Register(this);

        void OnDisable()
        {
            if (CrowdMoodDirector.HasInstance) CrowdMoodDirector.Instance.Unregister(this);
        }

        /// <summary>
        /// 스포너가 배치 직후 호출한다. (인스펙터로 직접 배치할 때는 필요 없다)
        /// AddComponent 시점에 이미 Awake/OnEnable 이 돌았으므로 개성과 현재 상태를 여기서 다시 잡아준다.
        /// </summary>
        public void Setup(int seed, float scale, int sortingOrder)
        {
            variantSeed = seed;
            baseScale = scale;
            Initialize();
            _renderer.sortingOrder = sortingOrder;

            if (CrowdMoodDirector.HasInstance)
            {
                var director = CrowdMoodDirector.Instance;
                OnCrowdMoodChanged(director.CurrentTier, director.CurrentIndex, instant: true);
            }
        }

        // ---------------- 상태 반응 ----------------

        public void OnCrowdMoodChanged(CrowdMoodTier tier, int index, bool instant)
        {
            if (tier == null) return;

            _fromTier = instant || _toTier == null ? tier : _toTier;
            _from = instant || _to == null ? tier.motion : _to;
            _toTier = tier;
            _to = tier.motion;

            float duration = instant ? 0f : BlendDuration();
            _blend = duration > 0f ? 0f : 1f;
            _blendSpeed = duration > 0f ? 1f / duration : 0f;

            // 개체별 속도 편차는 상태마다 다시 뽑는다 (상태가 바뀌면 리듬도 새로 섞이는 편이 자연스럽다)
            var rng = new System.Random(variantSeed * 31 + index);
            _speedMul = 1f + ((float)rng.NextDouble() * 2f - 1f) * _to.speedVariance;

            ApplySprite(tier, index);
        }

        float BlendDuration()
        {
            var config = CrowdMoodDirector.HasInstance ? CrowdMoodDirector.Instance.Config : null;
            return config != null ? config.moodBlendDuration : 0f;
        }

        void ApplySprite(CrowdMoodTier tier, int index)
        {
            _renderer.color = tier.tint;

            // (1) 애니메이션 클립이 있으면 그걸 재생한다 (관객마다 다른 variant 를 배정받는다)
            var clip = tier.GetAnimationVariant(variantSeed);
            if (clip != null && clip.IsValid)
            {
                _player.Play(clip, restart: true);
                return;
            }

            // (2) 없으면 기존처럼 정지 이미지 한 장
            _player.Stop();
            if (tier.sprites == null || tier.sprites.Length == 0) return;

            // 프레임 순환 모드가 아니면 개체마다 세트의 한 장을 고정으로 나눠 갖는다
            if (tier.motion.spriteCycleFps <= 0f)
                _renderer.sprite = tier.sprites[Mod(variantSeed, tier.sprites.Length)];
        }

        // ---------------- 움직임 ----------------

        void Update()
        {
            // 프레임 애니메이션은 개체별 속도 배율을 그대로 받아 리듬이 서로 어긋난다
            _player.Tick(Time.deltaTime * _speedMul);

            if (_to == null) return;

            if (_blend < 1f) _blend = Mathf.Min(1f, _blend + Time.deltaTime * _blendSpeed);

            float t = Time.time * _speedMul + _phase;

            float height = 0f;
            float squashAmount = 0f;

            // --- 점프: jumpHeight 가 0보다 큰 상태에서만 발생 (High) ---
            float jumpHeight = Blend(_from.jumpHeight, _to.jumpHeight);
            float jumpsPerSecond = Blend(_from.jumpsPerSecond, _to.jumpsPerSecond);
            if (jumpHeight > 0.001f && jumpsPerSecond > 0.001f)
            {
                float air = Mathf.Clamp(Blend(_from.airTimeRatio, _to.airTimeRatio), 0.1f, 1f);
                float cycle = Mathf.Repeat(t * jumpsPerSecond, 1f);

                if (cycle < air)
                {
                    // 공중 구간: 사인 아치로 뜬다 (포물선보다 정점이 부드러워 실루엣에 잘 맞는다)
                    height = Mathf.Sin(Mathf.PI * (cycle / air)) * jumpHeight;
                }
                else
                {
                    // 착지 구간: 무릎을 굽혔다 펴는 느낌으로 눌렸다 돌아온다
                    float k = (cycle - air) / (1f - air);
                    squashAmount = Mathf.Sin(Mathf.PI * k) * Blend(_from.squash, _to.squash);
                }
            }

            // --- 제자리 반동 (점프 중에는 약해진다) ---
            float bob = Mathf.Abs(Mathf.Sin(t * Mathf.PI * Blend(_from.bobSpeed, _to.bobSpeed)))
                        * Blend(_from.bobHeight, _to.bobHeight);
            height += bob * (1f - Mathf.Clamp01(jumpHeight * 2f));

            // --- 좌우 흔들림 ---
            float sway = Mathf.Sin(t * Mathf.PI * Blend(_from.swaySpeed, _to.swaySpeed))
                         * Blend(_from.swayAngle, _to.swayAngle);

            // --- 적용 ---
            float scale = baseScale * Blend(TierScale(_fromTier), TierScale(_toTier));
            transform.localPosition = _basePosition + new Vector3(0f, height, 0f);
            transform.localRotation = Quaternion.Euler(0f, 0f, sway * _flip);
            transform.localScale = new Vector3(
                _flip * scale * (1f + squashAmount * 0.5f),   // 눌리면 옆으로 퍼진다
                scale * (1f - squashAmount),
                1f);

            UpdateSpriteCycle(t);
        }

        /// <summary>
        /// [레거시] sprites 세트를 프레임처럼 순환시키는 간이 모드(spriteCycleFps > 0).
        /// animationVariants 를 쓰면 이 경로는 타지 않는다.
        /// </summary>
        void UpdateSpriteCycle(float t)
        {
            if (_player.IsPlaying) return; // 정식 클립이 재생 중이면 손대지 않는다

            float fps = _to.spriteCycleFps;
            if (fps <= 0f || _toTier?.sprites == null || _toTier.sprites.Length == 0) return;

            int frame = Mod(Mathf.FloorToInt(t * fps) + variantSeed, _toTier.sprites.Length);
            _renderer.sprite = _toTier.sprites[frame];
        }

        float Blend(float a, float b) => _blend >= 1f ? b : Mathf.Lerp(a, b, _blend);

        static float TierScale(CrowdMoodTier tier) => tier != null ? tier.scaleMultiplier : 1f;

        /// <summary>음수 seed 에서도 안전한 나머지 연산.</summary>
        static int Mod(int value, int length) => length <= 0 ? 0 : ((value % length) + length) % length;
    }
}
