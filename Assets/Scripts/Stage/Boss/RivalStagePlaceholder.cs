using System.Collections;
using System.Collections.Generic;
using GameJamKit;
using UnityEngine;

namespace ContextStage
{
    /// <summary>
    /// 라이벌 무대 임시 표현 (전부 Square). 아트가 오면 스프라이트만 교체한다.
    ///
    /// - 단상 + 듀오 2명 + 전광판 (Sorting Order 1). 같은 오브젝트에 BossArenaLayout 이 있으면
    ///   라이벌 무대 구역(왼쪽 화면)에, 없으면 카메라 자리에 놓는다
    /// - 상대 대기 팬 사각형은 관객 스탠딩석 구역(가운데 화면)에. 패턴 성공 시 하나가 우리 쪽으로 이동, 실패 시 하나 추가
    /// - 패턴 예고 중 듀오 점멸, 격파 시 소등 + 딤 + 환호
    /// 룰 참조 없이 EventBus 만 구독한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RivalStagePlaceholder : MonoBehaviour
    {
        [Header("배치 (구역 중심 기준 월드 오프셋)")]
        [SerializeField] Vector2 platformOffset = new Vector2(0f, 3.2f);
        [SerializeField] Vector2 platformSize = new Vector2(6.5f, 1.2f);
        [SerializeField] float duoSpacing = 2.4f;
        [SerializeField] float duoSize = 0.9f;
        [SerializeField, Tooltip("대기 팬 기준점 오프셋. x 는 좌우 번갈아 ±")] Vector2 fanPoolOffset = new Vector2(8.2f, -0.6f);
        [SerializeField, Tooltip("3화면일 때 대기 팬 x 오프셋 (스탠딩석 중심 기준)")] float fanPoolOffsetInArena = 1.6f;
        [SerializeField, Tooltip("3화면일 때 단상 오프셋 (라이벌 무대 구역 중심 기준). 화면 가운데 조금 위")] Vector2 platformOffsetInArena = new Vector2(0f, 0.6f);
        [SerializeField] float fanSize = 0.45f;
        [SerializeField] float fanSpacing = 0.55f;
        [SerializeField] int sortingOrder = 1;
        [SerializeField] string sortingLayer = "Default";

        [Header("색")]
        [SerializeField] Color platformColor = new Color32(0x3E, 0x35, 0x46, 0xFF);
        [SerializeField] Color ledColor = new Color32(0xB0, 0x3C, 0xFF, 0xFF);
        [SerializeField] Color duoAColor = new Color32(0xF0, 0x4F, 0x78, 0xFF);
        [SerializeField] Color duoBColor = new Color32(0x30, 0xE1, 0xB9, 0xFF);
        [SerializeField] Color fanColor = new Color32(0xC7, 0xDC, 0xD0, 0xB0);
        [SerializeField, Range(0f, 1f)] float defeatDimAlpha = 0.55f;
        [SerializeField] string defeatSoundId = "crowd_high";

        [Header("초기 대기 팬 수 (룰 설정과 맞춘다)")]
        [SerializeField, Min(0)] int initialFanPool = 6;

        Sprite _square;
        Transform _root;
        SpriteRenderer _platform;
        SpriteRenderer _led;
        SpriteRenderer _duoA;
        SpriteRenderer _duoB;
        SpriteRenderer _dim;
        Transform _fanRoot;
        Vector3 _ourStageAnchor;
        float _fanSideOffset;
        readonly List<SpriteRenderer> _fans = new List<SpriteRenderer>();
        Coroutine _blinkRoutine;
        bool _built;

        void OnEnable()
        {
            EventBus.Subscribe<BossHealthChanged>(OnHealth);
            EventBus.Subscribe<BossPatternStarted>(OnPatternStarted);
            EventBus.Subscribe<BossPatternResolved>(OnPatternResolved);
            EventBus.Subscribe<BossDefeated>(OnDefeated);
            EventBus.Subscribe<GameStateChanged>(OnGameStateChanged);
        }

        void OnDisable()
        {
            EventBus.Unsubscribe<BossHealthChanged>(OnHealth);
            EventBus.Unsubscribe<BossPatternStarted>(OnPatternStarted);
            EventBus.Unsubscribe<BossPatternResolved>(OnPatternResolved);
            EventBus.Unsubscribe<BossDefeated>(OnDefeated);
            EventBus.Unsubscribe<GameStateChanged>(OnGameStateChanged);
            if (_root != null) _root.gameObject.SetActive(false);
            if (_fanRoot != null) _fanRoot.gameObject.SetActive(false);
        }

        // ---------------- 이벤트 ----------------

        void OnHealth(BossHealthChanged e)
        {
            EnsureBuilt();
            _root.gameObject.SetActive(true);
            _fanRoot.gameObject.SetActive(true);
            if (_led != null) _led.color = Color.Lerp(new Color(0.2f, 0.1f, 0.25f, 1f), ledColor, e.Normalized);
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

            if (e.Success)
            {
                for (int i = 0; i < e.FansMoved; i++) StartCoroutine(MoveFanToUs());
            }
            else
            {
                for (int i = 0; i < e.FansMoved; i++) AddFan();
            }
        }

        void OnDefeated(BossDefeated e)
        {
            EnsureBuilt();
            if (_blinkRoutine != null) StopCoroutine(_blinkRoutine);
            StartCoroutine(Blackout());
        }

        void OnGameStateChanged(GameStateChanged e)
        {
            if (e.Current != GameState.Ready || !_built) return;
            ResetVisual();
        }

        // ---------------- 연출 ----------------

        IEnumerator Blink(float duration)
        {
            float elapsed = 0f;
            bool on = false;
            while (elapsed < duration)
            {
                on = !on;
                _duoA.color = on ? Color.white : duoAColor;
                _duoB.color = on ? Color.white : duoBColor;
                _led.color = on ? Color.white : ledColor;
                yield return new WaitForSeconds(0.25f);
                elapsed += 0.25f;
            }
            _duoA.color = duoAColor;
            _duoB.color = duoBColor;
            _led.color = ledColor;
            _blinkRoutine = null;
        }

        IEnumerator MoveFanToUs()
        {
            if (_fans.Count == 0) yield break;
            SpriteRenderer fan = _fans[_fans.Count - 1];
            _fans.RemoveAt(_fans.Count - 1);

            Vector3 from = fan.transform.position;
            Vector3 to = _ourStageAnchor + new Vector3(0f, -1.5f, 0f);
            float elapsed = 0f;
            const float duration = 0.6f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                fan.transform.position = Vector3.Lerp(from, to, t);
                Color c = fan.color;
                c.a = 1f - t;
                fan.color = c;
                yield return null;
            }
            Destroy(fan.gameObject);
        }

        void AddFan()
        {
            int index = _fans.Count;
            _fans.Add(CreateFan(index));
        }

        IEnumerator Blackout()
        {
            Color dark = new Color(0.05f, 0.04f, 0.06f, 1f);
            _platform.color = dark;
            _led.color = dark;
            _duoA.color = dark;
            _duoB.color = dark;
            for (int i = 0; i < _fans.Count; i++) if (_fans[i] != null) _fans[i].color = dark;

            if (!string.IsNullOrEmpty(defeatSoundId)) Sound.Play(defeatSoundId);

            float elapsed = 0f;
            const float duration = 0.5f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                _dim.color = new Color(0f, 0f, 0f, defeatDimAlpha * Mathf.Clamp01(elapsed / duration));
                yield return null;
            }
        }

        void ResetVisual()
        {
            _platform.color = platformColor;
            _led.color = ledColor;
            _duoA.color = duoAColor;
            _duoB.color = duoBColor;
            _dim.color = new Color(0f, 0f, 0f, 0f);
            for (int i = 0; i < _fans.Count; i++) if (_fans[i] != null) Destroy(_fans[i].gameObject);
            _fans.Clear();
            for (int i = 0; i < initialFanPool; i++) _fans.Add(CreateFan(i));
            _root.gameObject.SetActive(false);
            _fanRoot.gameObject.SetActive(false);
        }

        // ---------------- 생성 ----------------

        void EnsureBuilt()
        {
            if (_built) return;
            _built = true;

            _square = CreateSquare();
            Camera camera = Camera.main;
            Vector3 origin = camera != null ? new Vector3(camera.transform.position.x, camera.transform.position.y, 0f) : Vector3.zero;
            Vector3 fanOrigin = origin;
            _ourStageAnchor = origin;
            _fanSideOffset = fanPoolOffset.x;
            Vector2 platformAt = platformOffset;

            // 3화면 배치가 있으면 라이벌 무대는 왼쪽 화면, 대기 팬은 가운데 화면(스탠딩석)에 둔다
            BossArenaLayout arena = GetComponent<BossArenaLayout>();
            if (arena != null)
            {
                arena.Show();
                origin = arena.AnchorOf(BossZone.RivalStage);
                fanOrigin = arena.AnchorOf(BossZone.Standing);
                _ourStageAnchor = arena.AnchorOf(BossZone.OurStage);
                _fanSideOffset = fanPoolOffsetInArena;
                platformAt = platformOffsetInArena;
            }

            var rootObject = new GameObject("RivalStage");
            rootObject.transform.SetParent(transform, false);
            rootObject.transform.position = origin;
            _root = rootObject.transform;

            var fanRootObject = new GameObject("RivalFans");
            fanRootObject.transform.SetParent(transform, false);
            fanRootObject.transform.position = fanOrigin;
            _fanRoot = fanRootObject.transform;

            _platform = CreateSquareRenderer("Platform", platformAt, platformSize, platformColor, sortingOrder);
            _led = CreateSquareRenderer("LED", platformAt + new Vector2(0f, platformSize.y * 0.5f + 0.9f), new Vector2(3.2f, 0.5f), ledColor, sortingOrder);
            _duoA = CreateSquareRenderer("Duo_Owl", platformAt + new Vector2(-duoSpacing * 0.5f, platformSize.y * 0.5f + duoSize * 0.5f), Vector2.one * duoSize, duoAColor, sortingOrder + 1);
            _duoB = CreateSquareRenderer("Duo_Leopard", platformAt + new Vector2(duoSpacing * 0.5f, platformSize.y * 0.5f + duoSize * 0.5f), Vector2.one * duoSize, duoBColor, sortingOrder + 1);

            _dim = CreateSquareRenderer("DefeatDim", Vector2.zero, new Vector2(40f, 24f), new Color(0f, 0f, 0f, 0f), 60);

            for (int i = 0; i < initialFanPool; i++) _fans.Add(CreateFan(i));
        }

        SpriteRenderer CreateFan(int index)
        {
            // 좌·우 번갈아, 위로 쌓는다
            float side = index % 2 == 0 ? -1f : 1f;
            int row = index / 2;
            Vector2 offset = new Vector2(_fanSideOffset * side, fanPoolOffset.y + row * fanSpacing);
            return CreateSquareRenderer($"RivalFan_{index}", offset, Vector2.one * fanSize, fanColor, sortingOrder + 1, _fanRoot);
        }

        SpriteRenderer CreateSquareRenderer(string name, Vector2 offset, Vector2 size, Color color, int order, Transform parent = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent != null ? parent : _root, false);
            go.transform.localPosition = new Vector3(offset.x, offset.y, 0f);
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = _square;
            renderer.color = color;
            renderer.sortingLayerName = sortingLayer;
            renderer.sortingOrder = order;
            return renderer;
        }

        static Sprite CreateSquare()
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            tex.SetPixels32(new[] { new Color32(255, 255, 255, 255), new Color32(255, 255, 255, 255), new Color32(255, 255, 255, 255), new Color32(255, 255, 255, 255) });
            tex.Apply();
            var sprite = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 2f);
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

#if UNITY_EDITOR
        /// <summary>[에디터 셋업 전용]</summary>
        public void EditorSetFanPool(int count) => initialFanPool = Mathf.Max(0, count);
#endif
    }
}
