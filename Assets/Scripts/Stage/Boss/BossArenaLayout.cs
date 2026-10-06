using TMPro;
using UnityEngine;

namespace ContextStage
{
    /// <summary>보스전 2화면의 구역. 구역 중심 y = 홈 y + 구역 간격 × 값.</summary>
    public enum BossZone
    {
        /// <summary>너구리 밴드 무대. 현재 플레이 화면 그대로 (y = 0).</summary>
        OurStage = 0,
        /// <summary>라이벌 무대 (바로 위 화면). 위로 스크롤하면 보인다. 라이벌 팬이 그 앞에 서 있다.</summary>
        RivalStage = 1,
    }

    /// <summary>
    /// 보스전 2화면 세로 배치 (화면 기획 2026-09-18). 현재 플레이 화면(y = 0)은 건드리지 않고 **위쪽**에 라이벌 무대 구역을 덧붙인다.
    ///
    ///   y = +H  라이벌 무대 (위로 스크롤)
    ///   y =  0  너구리 밴드 무대 (조작)
    ///
    /// 구역 간격 H 는 한 화면(카메라 세로 10유닛)보다 조금 짧게 둔다. 그러면 라이벌 팬의 뒷줄이 우리 화면 위쪽 가장자리에
    /// 어둡고 작게 걸치고, 라이벌 화면 아래쪽에는 우리 관객의 머리가 걸친다 — 두 무대가 이어진 한 공간으로 읽힌다.
    /// 라이벌 구역 배경은 우리 배경(1920×1080, 위쪽 끝 y ≈ +5.4) 바로 위부터 라이벌 화면 위쪽 끝까지 채운다.
    /// 아트가 오면 rivalBackground 에 넣기만 하면 Square 대신 그것을 쓴다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BossArenaLayout : MonoBehaviour
    {
        [Header("구역")]
        [SerializeField, Min(1f), Tooltip("구역 폭(유닛). 배경 아트 1920px / PPU 100")] float zoneWidth = 19.2f;
        [SerializeField, Min(1f), Tooltip("우리 무대 중심에서 라이벌 무대 중심까지의 세로 거리(유닛). 카메라 세로 10 보다 짧게")] float zoneSpacing = 8f;
        [SerializeField, Min(1f), Tooltip("카메라 세로 크기(유닛). 라이벌 배경을 화면 위쪽 끝까지 채우는 데 쓴다")] float viewHeight = 10f;
        [SerializeField, Tooltip("우리 배경 위쪽 끝 (홈 기준 y). 여기서부터 라이벌 배경이 시작된다")] float ourBackgroundTop = 5f;
        [SerializeField, Tooltip("백지 배경 Sorting Order (우리 무대 배경 0 보다 아래)")] int backgroundSortingOrder = -1;
        [SerializeField] string sortingLayer = "Default";

        [Header("백지 색 (아트 오기 전)")]
        [SerializeField] Color rivalColor = new Color32(0x22, 0x12, 0x30, 0xFF);
        [SerializeField] Color rivalFloorColor = new Color32(0x36, 0x1E, 0x4C, 0xFF);
        [SerializeField] Color labelColor = new Color32(0xFF, 0xFF, 0xFF, 0x50);

        [Header("아트 (비우면 Square)")]
        [SerializeField] Sprite rivalBackground;
        [SerializeField, Tooltip("한글 라벨 폰트. 비우면 DialogueCatalog 스타일 폰트")] TMP_FontAsset labelFont;

        Transform _root;
        Vector3 _home;
        bool _built;

        public float ZoneWidth => zoneWidth;
        public float ZoneSpacing => zoneSpacing;
        public float ViewHeight => viewHeight;
        public bool IsBuilt => _built;

        /// <summary>우리 무대(구역 0)의 월드 중심. 카메라가 처음 있던 자리.</summary>
        public Vector3 Home => _home;

        /// <summary>구역 중심 월드 좌표 (라이벌 무대는 위).</summary>
        public Vector3 AnchorOf(BossZone zone) => _home + new Vector3(0f, zoneSpacing * (int)zone, 0f);

        /// <summary>구역을 만들고 보인다. 기준점은 첫 호출 때 카메라 위치.</summary>
        public void Show()
        {
            EnsureBuilt();
            _root.gameObject.SetActive(true);
        }

        public void Hide()
        {
            if (_root != null) _root.gameObject.SetActive(false);
        }

        void OnDisable() => Hide();

        void EnsureBuilt()
        {
            if (_built) return;
            _built = true;

            Camera camera = Camera.main;
            _home = camera != null ? new Vector3(camera.transform.position.x, camera.transform.position.y, 0f) : Vector3.zero;

            var rootObject = new GameObject("BossArena");
            rootObject.transform.SetParent(transform, false);
            rootObject.transform.position = Vector3.zero;
            _root = rootObject.transform;

            BuildRivalZone();
        }

        void BuildRivalZone()
        {
            var zoneObject = new GameObject("Zone_RivalStage");
            zoneObject.transform.SetParent(_root, false);
            Vector3 anchor = AnchorOf(BossZone.RivalStage);
            zoneObject.transform.position = anchor;

            // 우리 배경 위쪽 끝부터 라이벌 화면 위쪽 끝까지 (구역 로컬 y)
            float bottom = ourBackgroundTop - zoneSpacing;
            float top = viewHeight * 0.5f;
            float height = Mathf.Max(1f, top - bottom);
            float centerY = (top + bottom) * 0.5f;

            if (rivalBackground != null)
            {
                CreateRenderer(zoneObject.transform, "Background", rivalBackground, new Vector3(0f, centerY, 0f), Vector3.one, Color.white, backgroundSortingOrder);
            }
            else
            {
                Sprite square = SquareSprite.Get();
                CreateRenderer(zoneObject.transform, "Background", square, new Vector3(0f, centerY, 0f), new Vector3(zoneWidth, height, 1f), rivalColor, backgroundSortingOrder);
                // 바닥 띠: 라이벌 팬이 서는 아래쪽
                CreateRenderer(zoneObject.transform, "Floor", square, new Vector3(0f, bottom + 1.6f, 0f), new Vector3(zoneWidth, 3.2f, 1f), rivalFloorColor, backgroundSortingOrder);
                CreateRenderer(zoneObject.transform, "FloorLine", square, new Vector3(0f, bottom + 3.2f, 0f), new Vector3(zoneWidth, 0.06f, 1f), new Color(1f, 1f, 1f, 0.15f), backgroundSortingOrder);
            }

            CreateLabel(zoneObject.transform, "라이벌 무대 (임시)", new Vector3(-zoneWidth * 0.5f + 3.2f, top - 0.7f, 0f));
        }

        void CreateLabel(Transform parent, string text, Vector3 localPosition)
        {
            TMP_FontAsset font = labelFont;
            if (font == null)
            {
                DialogueCatalog catalog = DialogueCatalog.LoadDefault();
                if (catalog != null && catalog.Style != null) font = catalog.Style.Font;
            }

            var go = new GameObject("Label", typeof(TextMeshPro));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            var tmp = go.GetComponent<TextMeshPro>();
            if (font != null) tmp.font = font;
            tmp.text = text;
            tmp.fontSize = 4f;
            tmp.color = labelColor;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.rectTransform.sizeDelta = new Vector2(6f, 1.2f);
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sortingLayerName = sortingLayer;
            renderer.sortingOrder = backgroundSortingOrder + 1;
        }

        SpriteRenderer CreateRenderer(Transform parent, string name, Sprite sprite, Vector3 localPosition, Vector3 scale, Color color, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = scale;
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingLayerName = sortingLayer;
            renderer.sortingOrder = order;
            return renderer;
        }
    }

    /// <summary>1×1 유닛 흰색 Square 스프라이트 (임시 아트 공용).</summary>
    public static class SquareSprite
    {
        static Sprite _sprite;

        public static Sprite Get()
        {
            if (_sprite != null) return _sprite;
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            tex.SetPixels32(new[] { new Color32(255, 255, 255, 255), new Color32(255, 255, 255, 255), new Color32(255, 255, 255, 255), new Color32(255, 255, 255, 255) });
            tex.Apply();
            _sprite = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 2f);
            _sprite.hideFlags = HideFlags.HideAndDontSave;
            return _sprite;
        }
    }
}
