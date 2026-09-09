using TMPro;
using UnityEngine;

namespace ContextStage
{
    /// <summary>보스전 3화면의 구역. 왼쪽으로 갈수록 값이 크다 (구역 중심 x = 홈 x − 구역 폭 × 값).</summary>
    public enum BossZone
    {
        /// <summary>너구리 밴드 무대. 현재 플레이 화면 그대로 (x = 0).</summary>
        OurStage = 0,
        /// <summary>관객 스탠딩석. 상대 대기 팬이 서 있다.</summary>
        Standing = 1,
        /// <summary>라이벌 무대.</summary>
        RivalStage = 2,
    }

    /// <summary>
    /// 보스전 3화면 배치 (백지). 현재 플레이 화면(x = 0)은 건드리지 않고 왼쪽에 구역 두 개를 덧붙인다.
    ///
    ///   x = -2W  라이벌 무대     x = -W  관객 스탠딩석     x = 0  너구리 밴드 무대(조작)
    ///
    /// 구역 폭 W 는 배경 아트 규격(1920px, PPU 100 = 19.2 유닛)과 같다.
    /// 아트가 오면 구역별 스프라이트를 넣기만 하면 Square 대신 그것을 쓴다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BossArenaLayout : MonoBehaviour
    {
        [Header("구역")]
        [SerializeField, Min(1f), Tooltip("구역 한 칸 폭(유닛). 배경 아트 1920px / PPU 100")] float zoneWidth = 19.2f;
        [SerializeField, Min(1f), Tooltip("구역 높이(유닛). 백지 사각형 크기")] float zoneHeight = 10.8f;
        [SerializeField, Tooltip("백지 배경 Sorting Order (우리 무대 배경 0 보다 아래)")] int backgroundSortingOrder = -1;
        [SerializeField] string sortingLayer = "Default";

        [Header("백지 색 (아트 오기 전)")]
        [SerializeField] Color standingColor = new Color32(0x14, 0x2A, 0x2E, 0xFF);
        [SerializeField] Color standingFloorColor = new Color32(0x1F, 0x3E, 0x42, 0xFF);
        [SerializeField] Color rivalColor = new Color32(0x22, 0x12, 0x30, 0xFF);
        [SerializeField] Color rivalFloorColor = new Color32(0x36, 0x1E, 0x4C, 0xFF);
        [SerializeField] Color labelColor = new Color32(0xFF, 0xFF, 0xFF, 0x50);

        [Header("아트 (비우면 Square)")]
        [SerializeField] Sprite standingBackground;
        [SerializeField] Sprite rivalBackground;
        [SerializeField, Tooltip("한글 라벨 폰트. 비우면 DialogueCatalog 스타일 폰트")] TMP_FontAsset labelFont;

        Transform _root;
        Vector3 _home;
        bool _built;

        public float ZoneWidth => zoneWidth;
        public bool IsBuilt => _built;

        /// <summary>우리 무대(구역 0)의 월드 중심. 카메라가 처음 있던 자리.</summary>
        public Vector3 Home => _home;

        /// <summary>구역 중심 월드 좌표.</summary>
        public Vector3 AnchorOf(BossZone zone) => _home + new Vector3(-zoneWidth * (int)zone, 0f, 0f);

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

            BuildZone(BossZone.Standing, "Zone_Standing", "관객 스탠딩석 (임시)", standingBackground, standingColor, standingFloorColor);
            BuildZone(BossZone.RivalStage, "Zone_RivalStage", "라이벌 무대 (임시)", rivalBackground, rivalColor, rivalFloorColor);
        }

        void BuildZone(BossZone zone, string name, string label, Sprite art, Color color, Color floorColor)
        {
            var zoneObject = new GameObject(name);
            zoneObject.transform.SetParent(_root, false);
            zoneObject.transform.position = AnchorOf(zone);

            if (art != null)
            {
                CreateRenderer(zoneObject.transform, "Background", art, Vector3.zero, Vector3.one, Color.white, backgroundSortingOrder);
            }
            else
            {
                Sprite square = SquareSprite.Get();
                CreateRenderer(zoneObject.transform, "Background", square, Vector3.zero, new Vector3(zoneWidth, zoneHeight, 1f), color, backgroundSortingOrder);
                CreateRenderer(zoneObject.transform, "Floor", square, new Vector3(0f, -zoneHeight * 0.5f + 1.1f, 0f), new Vector3(zoneWidth, 2.2f, 1f), floorColor, backgroundSortingOrder);
                CreateRenderer(zoneObject.transform, "FloorLine", square, new Vector3(0f, -zoneHeight * 0.5f + 2.2f, 0f), new Vector3(zoneWidth, 0.06f, 1f), new Color(1f, 1f, 1f, 0.15f), backgroundSortingOrder);
            }

            CreateLabel(zoneObject.transform, label);
        }

        void CreateLabel(Transform parent, string text)
        {
            TMP_FontAsset font = labelFont;
            if (font == null)
            {
                DialogueCatalog catalog = DialogueCatalog.LoadDefault();
                if (catalog != null && catalog.Style != null) font = catalog.Style.Font;
            }

            var go = new GameObject("Label", typeof(TextMeshPro));
            go.transform.SetParent(parent, false);
            // 바닥 띠 안쪽 (단상·듀오가 있는 위쪽과 겹치지 않게)
            go.transform.localPosition = new Vector3(0f, -zoneHeight * 0.5f + 1.1f, 0f);
            var tmp = go.GetComponent<TextMeshPro>();
            if (font != null) tmp.font = font;
            tmp.text = text;
            tmp.fontSize = 6f;
            tmp.color = labelColor;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.rectTransform.sizeDelta = new Vector2(zoneWidth, 2f);
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
