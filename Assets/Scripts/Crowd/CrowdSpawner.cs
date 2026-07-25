using System.Collections;
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

        [Header("Crowd Shift Transition")]
        [SerializeField] bool animateCompositionChanges = true;
        [SerializeField, Min(0.05f)] float exitDuration = 0.4f;
        [SerializeField, Min(0.05f)] float enterDuration = 0.55f;
        [SerializeField, Min(0f)] float memberStagger = 0.04f;
        [SerializeField, Min(0f)] float horizontalTravel = 1.5f;
        [SerializeField, Min(0f)] float verticalTravel = 1.1f;

        readonly List<CrowdMemberView> _members = new List<CrowdMemberView>();
        readonly List<CrowdPreference> _preferenceOrder = new List<CrowdPreference>();
        Coroutine _transitionRoutine;
        CrowdCompositionSnapshot _queuedComposition;
        bool _hasQueuedComposition;

        // AudienceInflowSystem(관객 유입/이탈)이 스폰한 관객. 프리셋 그리드(_members)와는 별개 목록으로 둬서
        // CrowdCompositionManager 기반 프리셋 교체(BuildReplacements 등) 인덱스 계산에 영향을 주지 않는다.
        readonly List<CrowdMemberView> _additionalMembers = new List<CrowdMemberView>();

        public IReadOnlyList<CrowdMemberView> Members => _members;

        void OnEnable() => EventBus.Subscribe<CrowdCompositionChanged>(OnCompositionChanged);

        void OnDisable()
        {
            EventBus.Unsubscribe<CrowdCompositionChanged>(OnCompositionChanged);
            StopAllCoroutines();
            _transitionRoutine = null;
            _hasQueuedComposition = false;
            for (int i = 0; i < _members.Count; i++)
                if (_members[i] != null) _members[i].ClearTransitionState();
        }

        void Start()
        {
            if (!ValidateDependencies())
            {
                enabled = false;
                return;
            }

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
            if (prefab == null)
            {
                Debug.LogError($"[CrowdSpawner] Missing prefab for {preference}.", this);
                return null;
            }

            CrowdMemberView view = Instantiate(prefab, transform);
            GameObject go = view.gameObject;
            go.name = $"CrowdMember_{index:00}_{preference}";
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localPosition;

            var sr = go.GetComponent<SpriteRenderer>();
            sr.sortingLayerName = sortingLayer;
            view.Setup(index, scale, sortingOrder, preference);
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

        void OnCompositionChanged(CrowdCompositionChanged e)
        {
            if (!isActiveAndEnabled || _members.Count == 0) return;
            if (!Application.isPlaying || !animateCompositionChanges)
            {
                Spawn();
                return;
            }

            if (_transitionRoutine != null)
            {
                _queuedComposition = e.Current;
                _hasQueuedComposition = true;
                return;
            }

            _transitionRoutine = StartCoroutine(TransitionComposition(e.Current));
        }

        IEnumerator TransitionComposition(CrowdCompositionSnapshot target)
        {
            List<Replacement> replacements = BuildReplacements(target);
            if (replacements.Count == 0)
            {
                FinishTransition();
                yield break;
            }

            for (int i = 0; i < replacements.Count; i++)
            {
                StartCoroutine(AnimateExit(
                    replacements[i].OldMember,
                    replacements[i].Index,
                    i * memberStagger));
            }

            yield return new WaitForSeconds(exitDuration + memberStagger * (replacements.Count - 1));

            for (int i = 0; i < replacements.Count; i++)
            {
                Replacement replacement = replacements[i];
                CrowdMemberView member = CreateMember(
                    replacement.Index,
                    replacement.Home,
                    replacement.Scale,
                    replacement.SortingOrder,
                    replacement.NewPreference);

                Vector3 startOffset = TravelOffset(replacement.Home, replacement.Index);
                member.SetTransitionState(startOffset, 0f);
                _members[replacement.Index] = member;
                StartCoroutine(AnimateEnter(member, replacement.Index, i * memberStagger));
            }

            yield return new WaitForSeconds(enterDuration + memberStagger * (replacements.Count - 1));
            FinishTransition();
        }

        List<Replacement> BuildReplacements(CrowdCompositionSnapshot target)
        {
            int[] current =
            {
                CountMembers(CrowdPreference.Chill),
                CountMembers(CrowdPreference.Singalong),
                CountMembers(CrowdPreference.Mosh)
            };
            int[] desired =
            {
                target.ChillCount,
                target.SingalongCount,
                target.MoshCount
            };

            var incoming = new List<CrowdPreference>();
            for (int p = 0; p < desired.Length; p++)
            {
                int deficit = desired[p] - current[p];
                for (int i = 0; i < deficit; i++)
                    incoming.Add((CrowdPreference)p);
            }

            var outgoingIndices = new List<int>();
            for (int i = _members.Count - 1; i >= 0; i--)
            {
                CrowdMemberView member = _members[i];
                if (member == null) continue;
                int p = (int)member.Preference;
                if (p < 0 || p >= current.Length || current[p] <= desired[p]) continue;
                outgoingIndices.Add(i);
                current[p]--;
            }

            int replacementCount = Mathf.Min(outgoingIndices.Count, incoming.Count);
            var replacements = new List<Replacement>(replacementCount);
            for (int i = 0; i < replacementCount; i++)
            {
                int index = outgoingIndices[i];
                CrowdMemberView oldMember = _members[index];
                replacements.Add(new Replacement
                {
                    Index = index,
                    Home = oldMember.HomeLocalPosition,
                    Scale = oldMember.BaseScale,
                    SortingOrder = oldMember.SortingOrder,
                    NewPreference = incoming[i],
                    OldMember = oldMember
                });
            }

            return replacements;
        }

        int CountMembers(CrowdPreference preference)
        {
            int count = 0;
            for (int i = 0; i < _members.Count; i++)
                if (_members[i] != null && _members[i].Preference == preference) count++;
            return count;
        }

        IEnumerator AnimateExit(CrowdMemberView member, int slotIndex, float delay)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            if (member == null) yield break;

            Vector3 targetOffset = TravelOffset(member.HomeLocalPosition, slotIndex);
            float elapsed = 0f;
            while (elapsed < exitDuration && member != null)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / exitDuration));
                member.SetTransitionState(Vector3.LerpUnclamped(Vector3.zero, targetOffset, t), 1f - t);
                yield return null;
            }

            if (member != null) Destroy(member.gameObject);
        }

        IEnumerator AnimateEnter(CrowdMemberView member, int slotIndex, float delay)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            if (member == null) yield break;

            Vector3 startOffset = TravelOffset(member.HomeLocalPosition, slotIndex);
            float elapsed = 0f;
            while (elapsed < enterDuration && member != null)
            {
                elapsed += Time.deltaTime;
                float normalized = Mathf.Clamp01(elapsed / enterDuration);
                float positionT = 1f - Mathf.Pow(1f - normalized, 3f);
                member.SetTransitionState(
                    Vector3.LerpUnclamped(startOffset, Vector3.zero, positionT),
                    Mathf.SmoothStep(0f, 1f, normalized));
                yield return null;
            }

            if (member != null) member.ClearTransitionState();
        }

        Vector3 TravelOffset(Vector3 home, int fallback)
        {
            float direction = Mathf.Abs(home.x) > 0.05f
                ? Mathf.Sign(home.x)
                : (fallback & 1) == 0 ? -1f : 1f;
            return new Vector3(direction * horizontalTravel, -verticalTravel, 0f);
        }

        void FinishTransition()
        {
            _transitionRoutine = null;
            if (!_hasQueuedComposition || !isActiveAndEnabled) return;

            CrowdCompositionSnapshot queued = _queuedComposition;
            _hasQueuedComposition = false;
            _transitionRoutine = StartCoroutine(TransitionComposition(queued));
        }

        struct Replacement
        {
            public int Index;
            public Vector3 Home;
            public float Scale;
            public int SortingOrder;
            public CrowdPreference NewPreference;
            public CrowdMemberView OldMember;
        }

        /// <summary>배치된 관객을 모두 제거한다.</summary>
        public void Clear()
        {
            StopAllCoroutines();
            _transitionRoutine = null;
            _hasQueuedComposition = false;
            for (int i = 0; i < _members.Count; i++)
            {
                if (_members[i] == null) continue;
                if (Application.isPlaying) Destroy(_members[i].gameObject);
                else DestroyImmediate(_members[i].gameObject);
            }
            _members.Clear();

            for (int i = 0; i < _additionalMembers.Count; i++)
            {
                if (_additionalMembers[i] == null) continue;
                if (Application.isPlaying) Destroy(_additionalMembers[i].gameObject);
                else DestroyImmediate(_additionalMembers[i].gameObject);
            }
            _additionalMembers.Clear();
        }

        // ------------------------------------------------------------------
        // 관객 유입/이탈 연동 (AudienceInflowSystem 전용, 추가 전용 API)
        // CrowdCompositionManager 프리셋(_members, 21명 고정 그리드)과는 완전히 별개로 동작한다.
        // 프리셋 전환 로직(BuildReplacements/TransitionComposition)은 건드리지 않는다.
        // ------------------------------------------------------------------

        /// <summary>관객 1명을 프리셋 그리드 밖에 추가로 스폰한다. 신규 유입 시 AudienceInflowSystem 이 호출한다.</summary>
        public CrowdMemberView SpawnAdditional(CrowdPreference preference)
        {
            int slot = _additionalMembers.Count;
            CrowdMemberView view = CreateMember(
                _members.Count + slot,
                AdditionalSlotPosition(slot),
                frontRowScale,
                baseSortingOrder + rows + 1,
                preference);

            if (view != null) _additionalMembers.Add(view);
            return view;
        }

        /// <summary>SpawnAdditional 로 추가된 관객을 퇴장 연출과 함께 제거한다. 이탈 시 AudienceInflowSystem 이 호출한다.</summary>
        public void DespawnAdditional(CrowdMemberView member)
        {
            if (member == null) return;
            int slot = _additionalMembers.IndexOf(member);
            if (slot < 0) return;

            _additionalMembers.RemoveAt(slot);
            StartCoroutine(AnimateExit(member, slot, 0f));
        }

        // 프리셋 그리드보다 앞쪽(음의 y)에 별도 줄로 배치해 기존 21명 그리드와 겹치지 않게 한다.
        // 정식 배치는 프리셋 시스템 자체가 가변 인원 모델로 개편되면 다시 설계될 잠정 값이다.
        Vector3 AdditionalSlotPosition(int slot)
        {
            int perRow = Mathf.Max(1, membersPerRow);
            int column = slot % perRow;
            int extraRow = slot / perRow;
            float rowWidth = horizontalSpacing * (perRow - 1);
            float x = -rowWidth * 0.5f + horizontalSpacing * column;
            float y = -rowSpacing * (extraRow + 1);
            return new Vector3(x, y, 0f);
        }

        bool ValidateDependencies()
        {
            if (compositionManager == null || !compositionManager.IsConfigured)
            {
                Debug.LogError(
                    "[CrowdSpawner] A configured CrowdCompositionManager reference is required.",
                    this);
                return false;
            }

            int slotCount = Mathf.Max(1, rows) * Mathf.Max(1, membersPerRow);
            if (slotCount != compositionManager.ExpectedCrowdSize)
            {
                Debug.LogError(
                    $"[CrowdSpawner] Layout has {slotCount} slots but composition expects " +
                    $"{compositionManager.ExpectedCrowdSize}.",
                    this);
                return false;
            }

            if (chillPrefab == null || singalongPrefab == null || moshPrefab == null)
            {
                Debug.LogError(
                    "[CrowdSpawner] Chill, Singalong, and Mosh prefabs must all be assigned.",
                    this);
                return false;
            }

            return true;
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            rows = Mathf.Max(1, rows);
            membersPerRow = Mathf.Max(1, membersPerRow);
        }
#endif
    }
}
