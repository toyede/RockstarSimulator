using UnityEngine;

namespace ContextStage
{
    /// <summary>
    /// 관객 제자리 움직임의 <b>단일 공식</b>.
    ///
    /// CrowdMemberView(배경 군중) · SpecialAudienceCrowdActor(특별 관객) · AudienceMemberActor(주 관객)가
    /// 모두 이 함수를 쓴다. 예전에는 같은 식이 세 곳에 복사돼 있어서 한쪽만 고치면 움직임이 서로 어긋났다.
    ///
    /// 구성:
    ///   - bob    : 항상 적용되는 제자리 반동 (점프 중에는 약해진다)
    ///   - sway   : 좌우로 기우는 회전
    ///   - jump   : jumpHeight 가 0보다 클 때만. 사인 아치로 떠올랐다가
    ///   - squash : 착지 구간에서 눌렸다 펴진다 (무게감)
    ///
    /// 관객마다 <see cref="MakeVariance"/> 로 뽑은 위상·속도 배율·좌우 반전을 곱하면
    /// 같은 공식을 써도 군무처럼 보이지 않는다.
    /// </summary>
    public static class CrowdMotionEvaluator
    {
        /// <summary>개체별 고유 편차. 같은 seed 면 항상 같은 값이 나온다 (재시작해도 배치가 튀지 않음).</summary>
        public readonly struct Variance
        {
            public Variance(float phase, float speedMultiplier, float flip)
            {
                Phase = phase;
                SpeedMultiplier = speedMultiplier;
                Flip = flip;
            }

            /// <summary>시작 위상. 관객마다 다른 타이밍에 움직이게 한다.</summary>
            public float Phase { get; }

            /// <summary>속도 배율. 1 근처에서 흔들린다.</summary>
            public float SpeedMultiplier { get; }

            /// <summary>+1 또는 -1. 스프라이트 좌우 반전과 기울기 방향.</summary>
            public float Flip { get; }

            public static Variance Identity => new Variance(0f, 1f, 1f);
        }

        /// <summary>seed 로부터 결정적으로 편차를 만든다. speedVariance 는 프로필에서 가져온다.</summary>
        public static Variance MakeVariance(int seed, float speedVariance)
        {
            var rng = new System.Random(seed * 7919 + 13);
            float phase = (float)rng.NextDouble() * 100f;
            float speed = 1f + ((float)rng.NextDouble() * 2f - 1f) * Mathf.Max(0f, speedVariance);
            float flip = rng.Next(2) == 0 ? 1f : -1f;
            return new Variance(phase, speed, flip);
        }

        /// <summary>
        /// 프로필 하나를 시각 t 에서 평가한다.
        /// height 는 위로 올라가는 양(유닛), sway 는 회전 각도(도), squash 는 0~0.5 눌림 비율.
        /// </summary>
        public static void Evaluate(
            CrowdMotionProfile profile,
            float t,
            out float height,
            out float sway,
            out float squash)
        {
            height = 0f;
            sway = 0f;
            squash = 0f;
            if (profile == null) return;

            // --- 점프: jumpHeight 가 0보다 큰 상태에서만 발생 ---
            if (profile.jumpHeight > 0.001f && profile.jumpsPerSecond > 0.001f)
            {
                float air = Mathf.Clamp(profile.airTimeRatio, 0.1f, 1f);
                float cycle = Mathf.Repeat(t * profile.jumpsPerSecond, 1f);

                if (cycle < air)
                {
                    // 공중 구간: 사인 아치로 뜬다 (포물선보다 정점이 부드러워 실루엣에 잘 맞는다)
                    height = Mathf.Sin(Mathf.PI * (cycle / air)) * profile.jumpHeight;
                }
                else
                {
                    // 착지 구간: 무릎을 굽혔다 펴는 느낌으로 눌렸다 돌아온다
                    float k = (cycle - air) / (1f - air);
                    squash = Mathf.Sin(Mathf.PI * k) * profile.squash;
                }
            }

            // --- 제자리 반동 (점프 중에는 약해진다) ---
            float bob = Mathf.Abs(Mathf.Sin(t * Mathf.PI * profile.bobSpeed)) * profile.bobHeight;
            height += bob * (1f - Mathf.Clamp01(profile.jumpHeight * 2f));

            // --- 좌우 흔들림 ---
            sway = Mathf.Sin(t * Mathf.PI * profile.swaySpeed) * profile.swayAngle;
        }

        /// <summary>
        /// 두 프로필을 blend(0~1) 로 섞어 평가한다. 상태가 바뀔 때 값이 툭 튀지 않게 한다.
        /// blend 가 1 이면 to 만 쓴다.
        /// </summary>
        public static void EvaluateBlended(
            CrowdMotionProfile from,
            CrowdMotionProfile to,
            float blend,
            float t,
            out float height,
            out float sway,
            out float squash)
        {
            if (from == null || blend >= 1f)
            {
                Evaluate(to, t, out height, out sway, out squash);
                return;
            }
            if (to == null)
            {
                Evaluate(from, t, out height, out sway, out squash);
                return;
            }

            Evaluate(from, t, out float h0, out float s0, out float q0);
            Evaluate(to, t, out float h1, out float s1, out float q1);

            float k = Mathf.Clamp01(blend);
            height = Mathf.Lerp(h0, h1, k);
            sway = Mathf.Lerp(s0, s1, k);
            squash = Mathf.Lerp(q0, q1, k);
        }
    }
}
