using System.Collections.Generic;
using GameJamKit;
using UnityEngine;

namespace ContextStage
{

    public class CrowdSpawner : MonoBehaviour
    {
        [Header("배치")]
        [SerializeField, Tooltip("줄 수 (뒤로 갈수록 작아진다)")]
        int rows = 3;

        [SerializeField, Tooltip("한 줄에 세울 관객 수")]
        int membersPerRow = 7;

        [SerializeField, Tooltip("좌우 간격(월드 유닛)")]
        float horizontalSpacing = 1.5f;

        [SerializeField, Tooltip("줄 사이 세로 간격(월드 유닛)")]
        float rowSpacing = 0.45f;

        [SerializeField, Tooltip("위치를 흐트러뜨리는 정도. 0 이면 완전히 격자로 선다")]
        float positionJitter = 0.22f;

        [Header("크기")]
        [SerializeField, Tooltip("앞줄 관객의 크기 배율 (스프라이트가 커서 보통 1보다 작다)")]
        float frontRowScale = 0.55f;

        [SerializeField, Tooltip("한 줄 뒤로 갈 때마다 곱해지는 크기 비율")]
        [Range(0.5f, 1f)] float rowScaleFalloff = 0.88f;

        [Header("정렬")]
        [SerializeField, Tooltip("SpriteRenderer 정렬 레이어")]
        string sortingLayer = "Default";

        [SerializeField, Tooltip("가장 뒷줄의 sortingOrder. 앞줄로 올수록 +1 씩 커진다")]
        int baseSortingOrder = 0;

        [Header("기타")]
        [SerializeField, Tooltip("배치 난수 시드. 바꾸면 군중 구성이 통째로 달라진다")]
        int seed = 12345;

        [SerializeField, Tooltip("Play 시작과 동시에 배치할지")]
        bool spawnOnStart = true;

        [Header("Audience Preference Prefabs")]
        [SerializeField] CrowdCompositionManager compositionManager;
        [SerializeField] CrowdMemberView chillPrefab;
        [SerializeField] CrowdMemberView singalongPrefab;
        [SerializeField] CrowdMemberView moshPrefab;

        readonly List<CrowdMemberView> _members = new List<CrowdMemberView>();
        readonly List<CrowdPreference> _preferenceOrder = new List<CrowdPreference>();

        public IReadOnlyList<CrowdMemberView> Members => _members;

        void OnEnable() => EventBus.Subscribe<CrowdCompositionChanged>(OnCompositionChanged);

        void OnDisable() => EventBus.Unsubscribe<CrowdCompositionChanged>(OnCompositionChanged);

        void Start()
        {
            if (compositionManager == null)
                compositionManager = FindFirstObjectByType<CrowdCompositionManager>();
            if (compositionManager == null)
                compositionManager = gameObject.AddComponent<CrowdCompositionManager>();
            if (GetComponent<CrowdCompositionDebugView>() == null)
                gameObject.AddComponent<CrowdCompositionDebugView>();
            if (spawnOnStart && _members.Count == 0) Spawn();
        }

        /// <summary>관객을 새로 배치한다. 이미 있으면 지우고 다시 만든다.</summary>
        public void Spawn()
        {
            Clear();

            var rng = new System.Random(seed);
            BuildPreferenceOrder(rng);
            int index = 0;

            for (int row = 0; row < rows; row++)
            {
                // 0 = 맨 앞줄. 뒤로 갈수록 작고 위에 있고 먼저 그려진다
                float scale = frontRowScale * Mathf.Pow(rowScaleFalloff, row);
                float spacing = horizontalSpacing * scale / Mathf.Max(frontRowScale, 0.0001f);
                float rowWidth = spacing * (membersPerRow - 1);
                int sortingOrder = baseSortingOrder + (rows - row);

                // 줄마다 반 칸씩 어긋나게 해서 앞뒤 관객이 정확히 겹치지 않게 한다
                float rowOffset = (row % 2 == 0 ? 0f : spacing * 0.5f);

                for (int i = 0; i < membersPerRow; i++)
                {
                    float x = -rowWidth * 0.5f + spacing * i + rowOffset;
                    float y = row * rowSpacing;

                    x += ((float)rng.NextDouble() * 2f - 1f) * positionJitter;
                    y += ((float)rng.NextDouble() * 2f - 1f) * positionJitter * 0.4f;

                    CrowdPreference preference = index < _preferenceOrder.Count
                        ? _preferenceOrder[index]
                        : CrowdPreference.Mosh;
                    _members.Add(CreateMember(
                        index++,
                        new Vector3(x, y, 0f),
                        scale,
                        sortingOrder,
                        preference));
                }
            }
        }

        void BuildPreferenceOrder(System.Random rng)
        {
            _preferenceOrder.Clear();
            if (compositionManager == null) return;

            AddPreference(CrowdPreference.Chill, compositionManager.GetCount(CrowdPreference.Chill));
            AddPreference(CrowdPreference.Singalong, compositionManager.GetCount(CrowdPreference.Singalong));
            AddPreference(CrowdPreference.Mosh, compositionManager.GetCount(CrowdPreference.Mosh));

            for (int i = _preferenceOrder.Count - 1; i > 0; i--)
            {
                int swapIndex = rng.Next(i + 1);
                CrowdPreference temp = _preferenceOrder[i];
                _preferenceOrder[i] = _preferenceOrder[swapIndex];
                _preferenceOrder[swapIndex] = temp;
            }
        }

        void AddPreference(CrowdPreference preference, int count)
        {
            for (int i = 0; i < count; i++)
                _preferenceOrder.Add(preference);
        }

        CrowdMemberView CreateMember(
            int index,
            Vector3 localPosition,
            float scale,
            int sortingOrder,
            CrowdPreference preference)
        {
            CrowdMemberView prefab = PrefabFor(preference);
            GameObject go;
            CrowdMemberView view;
            CrowdPreferencePlaceholderView placeholder = null;

            if (prefab != null)
            {
                view = Instantiate(prefab, transform);
                go = view.gameObject;
            }
            else
            {
                go = new GameObject();
                go.AddComponent<SpriteRenderer>();
                view = go.AddComponent<CrowdMemberView>();

                // The existing low/middle/high sheets are actual Mosh artwork.
                // Chill and Singalong keep placeholders until their art arrives.
                bool useExistingMoshSprites = preference == CrowdPreference.Mosh;
                view.ConfigureIdentity(
                    preference,
                    preservePrefabArtwork: !useExistingMoshSprites);
                if (!useExistingMoshSprites)
                    placeholder = go.AddComponent<CrowdPreferencePlaceholderView>();
            }

            go.name = $"CrowdMember_{index:00}_{preference}";
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localPosition;

            var sr = go.GetComponent<SpriteRenderer>();
            sr.sortingLayerName = sortingLayer;
            view.Setup(index, scale, sortingOrder, preference);
            if (placeholder != null)
                placeholder.Configure(preference);
            return view;
        }

        CrowdMemberView PrefabFor(CrowdPreference preference)
        {
            switch (preference)
            {
                case CrowdPreference.Chill: return chillPrefab;
                case CrowdPreference.Singalong: return singalongPrefab;
                case CrowdPreference.Mosh: return moshPrefab;
                default: return null;
            }
        }

        void OnCompositionChanged(CrowdCompositionChanged _)
        {
            if (!isActiveAndEnabled || _members.Count == 0) return;
            Spawn();
        }

        /// <summary>배치된 관객을 모두 제거한다.</summary>
        public void Clear()
        {
            for (int i = 0; i < _members.Count; i++)
            {
                if (_members[i] == null) continue;
                if (Application.isPlaying) Destroy(_members[i].gameObject);
                else DestroyImmediate(_members[i].gameObject);
            }
            _members.Clear();
        }
    }
}
