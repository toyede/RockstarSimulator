using UnityEngine;
using UnityEngine.UI;

namespace ContextStage
{
    /// <summary>
    /// 증강 선택창 후보 카드의 등급 연출. 골드는 대각선 빛줄기 + 금색 별 반짝임,
    /// 실버는 가장자리 픽셀 별 반짝임, 브론즈는 아무것도 그리지 않는다.
    /// 카드 앞면 스프라이트 모양의 마스크 안에서만 그려 찢어진 카드 외곽을 따른다.
    /// 그림 1픽셀 단위 사각형만 쓰고, 매 프레임 메시만 다시 채운다(할당 없음).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AugmentTierShineVFX : MaskableGraphic
    {
        [Header("Gold - 빛나는")]
        [SerializeField] Color goldSweepColor = new Color32(0xFF, 0xF6, 0xD2, 0xFF);
        [SerializeField, Min(0.5f)] float goldSweepPeriod = 2.5f;
        [SerializeField, Min(0.1f)] float goldSweepDuration = 0.7f;
        [SerializeField, Range(1, 16), Tooltip("빛줄기 폭(그림 픽셀)")] int goldSweepWidth = 10;
        [SerializeField, Range(0f, 1f)] float goldSweepAlpha = 0.9f;
        [SerializeField, Range(0.1f, 2f), Tooltip("빛줄기 기울기(가로 이동/세로 1)")] float goldSweepSlope = 0.55f;

        [SerializeField, Tooltip("골드 카드에도 함께 띄우는 금색 별 반짝임")] Color goldSparkleColor = new Color32(0xFF, 0xD2, 0x3F, 0xFF);
        [SerializeField, Range(0, 8)] int goldSparkleCount = 4;

        [Header("Silver - 반짝이는 (골드 반짝임도 같은 주기·크기 사용)")]
        [SerializeField] Color silverSparkleColor = new Color32(0xF2, 0xF7, 0xFF, 0xFF);
        [SerializeField, Range(1, 8)] int silverSparkleCount = 5;
        [SerializeField, Min(0.2f)] float silverSparkleLifetime = 1.0f;
        [SerializeField, Min(0.2f)] float silverSparklePeriod = 1.6f;
        [SerializeField, Range(2, 20), Tooltip("가장자리에서 별이 나타나는 깊이(그림 픽셀)")] int silverEdgeInset = 7;

        Image _frame;
        Image _mask;
        AugmentTier _tier;
        bool _visible;
        float _startTime;
        int _seed;

        /// <summary>카드 앞면 이미지 아래에 마스크와 연출 그래픽을 만든다. 이미 있으면 그대로 쓴다.</summary>
        public static AugmentTierShineVFX Ensure(Image frame)
        {
            if (frame == null) return null;
            Transform existing = frame.transform.Find("TierShineMask");
            if (existing != null)
            {
                var found = existing.GetComponentInChildren<AugmentTierShineVFX>(true);
                if (found != null) return found;
            }

            var maskGo = new GameObject("TierShineMask", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Mask));
            maskGo.transform.SetParent(frame.transform, false);
            Stretch((RectTransform)maskGo.transform);
            var mask = maskGo.GetComponent<Image>();
            mask.raycastTarget = false;
            maskGo.GetComponent<Mask>().showMaskGraphic = false;

            var go = new GameObject("TierShine", typeof(RectTransform), typeof(CanvasRenderer));
            go.transform.SetParent(maskGo.transform, false);
            Stretch((RectTransform)go.transform);
            var effect = go.AddComponent<AugmentTierShineVFX>();
            effect.raycastTarget = false;
            effect._frame = frame;
            effect._mask = mask;
            effect.SetVisibleInternal(false);
            return effect;
        }

        /// <summary>현재 카드 앞면과 등급으로 연출을 켠다. 브론즈는 끈다.</summary>
        public void Show(AugmentTier tier, int seed)
        {
            _tier = tier;
            _seed = seed;
            _startTime = Time.unscaledTime;
            bool hasArt = _frame != null && _frame.sprite != null && _frame.enabled;
            if (_mask != null && hasArt)
            {
                _mask.sprite = _frame.sprite;
                _mask.type = _frame.type;
                _mask.preserveAspect = _frame.preserveAspect;
            }
            SetVisibleInternal(hasArt && tier != AugmentTier.Bronze);
        }

        public void Hide() => SetVisibleInternal(false);

        void SetVisibleInternal(bool visible)
        {
            _visible = visible;
            if (_mask != null) _mask.gameObject.SetActive(visible);
            color = Color.white;
            SetVerticesDirty();
        }

        void Update()
        {
            if (_visible) SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (!_visible || _frame == null || _frame.sprite == null) return;
            Rect r = rectTransform.rect;
            if (r.width <= 0f || r.height <= 0f) return;

            // 그림 1픽셀이 화면에서 차지하는 크기. 모든 사각형을 이 격자에 맞춘다.
            float px = r.width / Mathf.Max(1f, _frame.sprite.rect.width);
            int cols = Mathf.Max(1, Mathf.RoundToInt(r.width / px));
            int rows = Mathf.Max(1, Mathf.RoundToInt(r.height / px));
            float t = Time.unscaledTime - _startTime;

            if (_tier == AugmentTier.Gold)
            {
                DrawGold(vh, r, px, cols, rows, t);
                DrawSparkles(vh, r, px, cols, rows, t, goldSparkleColor, goldSparkleCount, 31);
            }
            else if (_tier == AugmentTier.Silver)
            {
                DrawSparkles(vh, r, px, cols, rows, t, silverSparkleColor, silverSparkleCount, 0);
            }
        }

        void DrawGold(VertexHelper vh, Rect r, float px, int cols, int rows, float t)
        {
            // 대각선 빛줄기: 주기마다 왼쪽 아래 밖에서 오른쪽 위 밖으로 한 번 지나간다.
            float cycle = Mathf.Repeat(t, goldSweepPeriod);
            if (cycle > goldSweepDuration) return;
            float progress = cycle / goldSweepDuration;
            float travel = cols + rows * goldSweepSlope + goldSweepWidth * 2f;
            float start = -rows * goldSweepSlope - goldSweepWidth;
            float head = start + travel * progress;
            float fade = Mathf.Sin(progress * Mathf.PI);
            for (int y = 0; y < rows; y++)
            {
                float center = head + y * goldSweepSlope;
                for (int band = 0; band < 3; band++)
                {
                    // 가운데 줄은 밝고 양옆은 반투명하게 겹쳐 부드럽게 보이게 한다.
                    float half = goldSweepWidth * (band == 0 ? 0.5f : band == 1 ? 0.3f : 0.15f);
                    int x0 = Mathf.Max(0, Mathf.RoundToInt(center - half));
                    int x1 = Mathf.Min(cols, Mathf.RoundToInt(center + half));
                    if (x1 <= x0) continue;
                    float a = goldSweepAlpha * fade * (band == 0 ? 0.25f : band == 1 ? 0.35f : 0.45f);
                    AddCell(vh, r, px, x0, y, x1 - x0, 1, WithAlpha(goldSweepColor, a));
                }
            }
        }

        void DrawSparkles(VertexHelper vh, Rect r, float px, int cols, int rows, float t,
            Color color, int count, int salt)
        {
            for (int i = 0; i < count; i++)
            {
                // 별마다 시작 시점을 엇갈리고, 주기가 돌 때마다 가장자리 근처 새 위치로 옮긴다.
                float offset = silverSparklePeriod * i / count;
                float local = t + offset;
                int cycleIndex = Mathf.FloorToInt(local / silverSparklePeriod);
                float age = local - cycleIndex * silverSparklePeriod;
                if (age > silverSparkleLifetime) continue;

                uint h = Hash((uint)(_seed * 7919 + salt * 2654435 + i * 104729 + cycleIndex * 15485863));
                EdgePoint(h, cols, rows, out int cx, out int cy);

                float life = age / silverSparkleLifetime;
                float bright = Mathf.Sin(life * Mathf.PI);
                // 가장 밝을 때 팔이 3칸까지 늘어나는 픽셀 십자 별.
                int armLength = bright > 0.8f ? 3 : bright > 0.5f ? 2 : bright > 0.2f ? 1 : 0;
                AddCell(vh, r, px, cx, cy, 1, 1, WithAlpha(color, bright));
                for (int d = 1; d <= armLength; d++)
                {
                    Color32 c = WithAlpha(color, bright * (d == 1 ? 0.8f : d == 2 ? 0.5f : 0.25f));
                    AddCell(vh, r, px, cx - d, cy, 1, 1, c);
                    AddCell(vh, r, px, cx + d, cy, 1, 1, c);
                    AddCell(vh, r, px, cx, cy - d, 1, 1, c);
                    AddCell(vh, r, px, cx, cy + d, 1, 1, c);
                }
            }
        }

        // 네 변 중 하나를 골라 가장자리에서 일정 깊이 안쪽의 점을 고른다. 별 팔이 잘리지 않게 여유를 둔다.
        void EdgePoint(uint h, int cols, int rows, out int x, out int y)
        {
            int inset = Mathf.Clamp(silverEdgeInset, 3, Mathf.Max(3, Mathf.Min(cols, rows) / 2 - 1));
            int depth = 3 + (int)((h >> 8) % (uint)Mathf.Max(1, inset - 2));
            int side = (int)(h % 4u);
            int alongX = 3 + (int)((h >> 16) % (uint)Mathf.Max(1, cols - 6));
            int alongY = 3 + (int)((h >> 16) % (uint)Mathf.Max(1, rows - 6));
            switch (side)
            {
                case 0: x = alongX; y = depth; break;
                case 1: x = alongX; y = rows - 1 - depth; break;
                case 2: x = depth; y = alongY; break;
                default: x = cols - 1 - depth; y = alongY; break;
            }
        }

        static uint Hash(uint v)
        {
            v ^= v >> 16; v *= 0x7feb352d;
            v ^= v >> 15; v *= 0x846ca68b;
            v ^= v >> 16;
            return v;
        }

        // 그림 픽셀 격자 기준 (x, y)에서 w×h 칸 사각형. y=0 은 아래쪽.
        static void AddCell(VertexHelper vh, Rect r, float px, int x, int y, int w, int h, Color32 c)
        {
            if (w <= 0 || h <= 0 || c.a == 0) return;
            float x0 = Mathf.Max(r.xMin, r.xMin + x * px);
            float y0 = Mathf.Max(r.yMin, r.yMin + y * px);
            float x1 = Mathf.Min(r.xMax, r.xMin + (x + w) * px);
            float y1 = Mathf.Min(r.yMax, r.yMin + (y + h) * px);
            if (x1 <= x0 || y1 <= y0) return;
            int i = vh.currentVertCount;
            vh.AddVert(new Vector3(x0, y0), c, Vector2.zero);
            vh.AddVert(new Vector3(x0, y1), c, Vector2.zero);
            vh.AddVert(new Vector3(x1, y1), c, Vector2.zero);
            vh.AddVert(new Vector3(x1, y0), c, Vector2.zero);
            vh.AddTriangle(i, i + 1, i + 2);
            vh.AddTriangle(i + 2, i + 3, i);
        }

        static Color32 WithAlpha(Color c, float alpha)
        {
            c.a *= Mathf.Clamp01(alpha);
            return c;
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
        }
    }
}
