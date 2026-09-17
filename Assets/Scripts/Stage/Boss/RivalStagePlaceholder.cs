using System.Collections;
using System.Collections.Generic;
using GameJamKit;
using UnityEngine;

namespace ContextStage
{
    /// <summary>
    /// 라이벌 무대 임시 표현 (전부 Square). 아트가 오면 스프라이트만 교체한다.
    ///
    /// - 단상 + 듀오 2명 + 전광판. 같은 오브젝트에 BossArenaLayout 이 있으면 라이벌 무대 구역(왼쪽 화면)에, 없으면 카메라 자리에
    /// - 라이벌 팬은 단상 앞에 줄지어 서 있고, 수는 룰(BossFanBalanceChanged)과 동기화된다
    /// - 팬 이동(BossFanMoved): 우리 → 라이벌이면 오른쪽에서 걸어 들어오고, 라이벌 → 우리면 오른쪽으로 걸어 나간다
    /// - 패턴 예고 중 듀오 점멸, REVENGE 진입 시 LED 진홍
    /// 룰 참조 없이 EventBus 만 구독한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RivalStagePlaceholder : MonoBehaviour
    {
        [Header("배치 (구역 중심 기준 월드 오프셋)")]
        [SerializeField] Vector2 platformOffset = new Vector2(0f, 0.6f);
        [SerializeField] Vector2 platformSize = new Vector2(6.5f, 1.2f);
        [SerializeField] float duoSpacing = 2.4f;
        [SerializeField] float duoSize = 0.9f;
        [SerializeField, Tooltip("팬 줄의 시작 y (구역 중심 기준)")] float fanRowY = -1.6f;
        [SerializeField, Min(1)] int fansPerRow = 7;
        [SerializeField] float fanSize = 0.45f;
        [SerializeField] float fanSpacingX = 0.8f;
        [SerializeField] float fanSpacingY = 0.6f;
        [SerializeField, Min(0.05f)] float fanWalkDuration = 0.9f;
        [SerializeField] int sortingOrder = 1;
        [SerializeField] string sortingLayer = "Default";

        [Header("색")]
        [SerializeField] Color platformColor = new Color32(0x3E, 0x35, 0x46, 0xFF);
        [SerializeField] Color ledColor = new Color32(0xB0, 0x3C, 0xFF, 0xFF);
        [SerializeField] Color revengeLedColor = new Color32(0xE0, 0x20, 0x40, 0xFF);
        [SerializeField] Color duoAColor = new Color32(0xF0, 0x4F, 0x78, 0xFF);
        [SerializeField] Color duoBColor = new Color32(0x30, 0xE1, 0xB9, 0xFF);
        [SerializeField] Color fanColor = new Color32(0xC7, 0xDC, 0xD0, 0xB0);

        Sprite _square;
        Transform _root;
        SpriteRenderer _platform;
        SpriteRenderer _led;
        SpriteRenderer _duoA;
        SpriteRenderer _duoB;
        readonly List<SpriteRenderer> _fans = new List<SpriteRenderer>();
        Vector3 _ourStageAnchor;
        Coroutine _blinkRoutine;
        bool _built;
        bool _revenge;

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
            EventBus.Unsubscribe<BossFanBalanceChanged>(OnBalance);
            EventBus.Unsubscribe<BossFanMoved>(OnFanMoved);
            EventBus.Unsubscribe<BossRevengeChanged>(OnRevenge);
            EventBus.Unsubscribe<BossPatternStarted>(OnPatternStarted);
            EventBus.Unsubscribe<BossPatternResolved>(OnPatternResolved);
            EventBus.Unsubscribe<GameStateChanged>(OnGameStateChanged);
            if (_root != null) _root.gameObject.SetActive(false);
        }

        // ---------------- 이벤트 ----------------

        void OnBalance(BossFanBalanceChanged e)
        {
            EnsureBuilt();
            _root.gameObject.SetActive(true);
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
                    SpriteRenderer fan = _fans[_fans.Count - 1];
                    _fans.RemoveAt(_fans.Count - 1);
                    StartCoroutine(WalkOut(fan));
                }
            }
        }

        void OnRevenge(BossRevengeChanged e)
        {
            EnsureBuilt();
            _revenge = e.Active;
            _led.color = _revenge ? revengeLedColor : ledColor;
        }

        void OnPatternStarted(BossPatternStarted e)
        {
            EnsureBuilt();
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
            _duoA.color = duoAColor;
            _duoB.color = duoBColor;
            _led.color = _revenge ? revengeLedColor : ledColor;
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
                SpriteRenderer extra = _fans[_fans.Count - 1];
                _fans.RemoveAt(_fans.Count - 1);
                if (extra != null) Destroy(extra.gameObject);
            }
            while (_fans.Count < target) _fans.Add(CreateFan(_fans.Count));
        }

        Vector2 FanSlot(int index)
        {
            int row = index / fansPerRow;
            int col = index % fansPerRow;
            int rowCount = Mathf.Min(fansPerRow, Mathf.Max(1, _fans.Count - row * fansPerRow));
            float x = (col - (fansPerRow - 1) * 0.5f) * fanSpacingX;
            float y = fanRowY - row * fanSpacingY;
            return new Vector2(x, y);
        }

        IEnumerator WalkIn(int slotIndex)
        {
            SpriteRenderer fan = CreateFan(slotIndex);
            _fans.Add(fan);
            Vector3 to = fan.transform.position;
            Vector3 from = new Vector3(_ourStageAnchor.x - 8f, to.y, 0f);
            yield return Walk(fan, from, to, false);
        }

        IEnumerator WalkOut(SpriteRenderer fan)
        {
            if (fan == null) yield break;
            Vector3 from = fan.transform.position;
            Vector3 to = new Vector3(_ourStageAnchor.x - 8f, from.y, 0f);
            yield return Walk(fan, from, to, true);
            if (fan != null) Destroy(fan.gameObject);
        }

        IEnumerator Walk(SpriteRenderer fan, Vector3 from, Vector3 to, bool fadeOut)
        {
            float elapsed = 0f;
            Color baseColor = fanColor;
            while (elapsed < fanWalkDuration)
            {
                if (fan == null) yield break;
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / fanWalkDuration);
                float bob = Mathf.Abs(Mathf.Sin(t * Mathf.PI * 4f)) * 0.12f;
                fan.transform.position = Vector3.Lerp(from, to, t) + new Vector3(0f, bob, 0f);
                Color c = baseColor;
                c.a = fadeOut ? baseColor.a * (1f - t * 0.6f) : baseColor.a;
                fan.color = c;
                yield return null;
            }
            if (fan != null) fan.transform.position = to;
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
                _duoA.color = on ? Color.white : duoAColor;
                _duoB.color = on ? Color.white : duoBColor;
                _led.color = on ? Color.white : baseLed;
                yield return new WaitForSeconds(0.25f);
                elapsed += 0.25f;
            }
            _duoA.color = duoAColor;
            _duoB.color = duoBColor;
            _led.color = baseLed;
            _blinkRoutine = null;
        }

        void ResetVisual()
        {
            _revenge = false;
            _platform.color = platformColor;
            _led.color = ledColor;
            _duoA.color = duoAColor;
            _duoB.color = duoBColor;
            for (int i = 0; i < _fans.Count; i++) if (_fans[i] != null) Destroy(_fans[i].gameObject);
            _fans.Clear();
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

            _platform = CreateSquareRenderer("Platform", platformOffset, platformSize, platformColor, sortingOrder);
            _led = CreateSquareRenderer("LED", platformOffset + new Vector2(0f, platformSize.y * 0.5f + 0.9f), new Vector2(3.2f, 0.5f), ledColor, sortingOrder);
            _duoA = CreateSquareRenderer("Duo_Owl", platformOffset + new Vector2(-duoSpacing * 0.5f, platformSize.y * 0.5f + duoSize * 0.5f), Vector2.one * duoSize, duoAColor, sortingOrder + 1);
            _duoB = CreateSquareRenderer("Duo_Leopard", platformOffset + new Vector2(duoSpacing * 0.5f, platformSize.y * 0.5f + duoSize * 0.5f), Vector2.one * duoSize, duoBColor, sortingOrder + 1);
        }

        SpriteRenderer CreateFan(int index)
        {
            SpriteRenderer fan = CreateSquareRenderer($"RivalFan_{index}", Vector2.zero, Vector2.one * fanSize, fanColor, sortingOrder + 2);
            fan.transform.localPosition = FanSlotFor(index);
            return fan;
        }

        Vector3 FanSlotFor(int index)
        {
            int row = index / fansPerRow;
            int col = index % fansPerRow;
            float x = (col - (fansPerRow - 1) * 0.5f) * fanSpacingX;
            float y = fanRowY - row * fanSpacingY;
            return new Vector3(x, y, 0f);
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
