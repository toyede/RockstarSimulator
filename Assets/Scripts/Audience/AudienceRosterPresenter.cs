using System.Collections.Generic;
using GameJamKit;
using UnityEngine;

namespace ContextStage
{
    [DisallowMultipleComponent]
    public sealed class AudienceRosterPresenter : MonoBehaviour
    {
        [Header("Prefab")]
        [SerializeField] AudienceMemberActor memberPrefab;
        [SerializeField] Transform memberRoot;

        [Header("Layout")]
        [SerializeField, Min(1)] int maximumColumns = 5;
        [SerializeField, Min(0.1f)] float horizontalSpacing = 1.35f;
        [SerializeField, Min(0.1f)] float rowSpacing = 0.55f;
        [SerializeField, Min(0.01f)] float frontRowScale = 0.58f;
        [SerializeField, Range(0.5f, 1f)] float rowScaleFalloff = 0.86f;
        [SerializeField] int baseSortingOrder = 5;

        readonly Dictionary<AudienceId, AudienceMemberActor> _actors =
            new Dictionary<AudienceId, AudienceMemberActor>();
        readonly List<AudienceId> _order = new List<AudienceId>(10);

        bool _started;
        bool _deferRelayout;

        public AudienceMemberActor MemberPrefab => memberPrefab;
        public Transform MemberRoot => memberRoot;
        public int VisibleCount => _actors.Count;

        /// <summary>[튜토리얼용 조회] 특정 관객의 화면 액터를 돌려준다. 없으면 false.</summary>
        public bool TryGetActor(AudienceId id, out AudienceMemberActor actor)
            => _actors.TryGetValue(id, out actor) && actor != null;

        public bool TryGetRandomActor(out AudienceMemberActor actor)
        {
            actor = null;
            if (_order.Count == 0) return false;

            int start = Random.Range(0, _order.Count);
            for (int offset = 0; offset < _order.Count; offset++)
            {
                AudienceId id = _order[(start + offset) % _order.Count];
                if (_actors.TryGetValue(id, out actor) && actor != null)
                    return true;
            }

            actor = null;
            return false;
        }

        public bool TryGetPreferenceHoverTarget(
            Vector2 worldPosition,
            out AudienceMemberActor actor)
        {
            actor = null;
            int bestSortingOrder = int.MinValue;
            float bestDistanceSquared = float.PositiveInfinity;
            int bestId = int.MaxValue;

            foreach (AudienceMemberActor candidate in _actors.Values)
            {
                if (candidate == null ||
                    !candidate.ContainsPreferenceHoverPoint(worldPosition))
                    continue;

                int sortingOrder = candidate.SortingOrder;
                float distanceSquared =
                    (candidate.PreferenceHoverCenter - worldPosition).sqrMagnitude;
                int id = candidate.BoundId.Value;

                bool isBetter =
                    sortingOrder > bestSortingOrder ||
                    (sortingOrder == bestSortingOrder &&
                     (distanceSquared < bestDistanceSquared ||
                      (Mathf.Approximately(
                           distanceSquared,
                           bestDistanceSquared) &&
                       id < bestId)));
                if (!isBetter) continue;

                actor = candidate;
                bestSortingOrder = sortingOrder;
                bestDistanceSquared = distanceSquared;
                bestId = id;
            }

            return actor != null;
        }

        void OnEnable()
        {
            EventBus.Subscribe<AudienceJoined>(OnAudienceJoined);
            EventBus.Subscribe<AudienceStateChanged>(OnAudienceStateChanged);
            EventBus.Subscribe<AudienceCardReacted>(OnAudienceCardReacted);
            EventBus.Subscribe<AudienceDeparted>(OnAudienceDeparted);
            EventBus.Subscribe<AudienceCrisisTargetsChanged>(
                OnCrisisTargetsChanged);
            EventBus.Subscribe<AudienceCrisisDepartureStarted>(
                OnCrisisDepartureStarted);
            EventBus.Subscribe<AudienceCrisisDepartureEnded>(
                OnCrisisDepartureEnded);
        }

        void Start()
        {
            if (!ValidateDependencies())
            {
                enabled = false;
                return;
            }

            _started = true;
            PoolManager.Prewarm(memberPrefab.gameObject, 10);
            SynchronizeFromRoster();
        }

        void OnDisable()
        {
            EventBus.Unsubscribe<AudienceJoined>(OnAudienceJoined);
            EventBus.Unsubscribe<AudienceStateChanged>(OnAudienceStateChanged);
            EventBus.Unsubscribe<AudienceCardReacted>(OnAudienceCardReacted);
            EventBus.Unsubscribe<AudienceDeparted>(OnAudienceDeparted);
            EventBus.Unsubscribe<AudienceCrisisTargetsChanged>(
                OnCrisisTargetsChanged);
            EventBus.Unsubscribe<AudienceCrisisDepartureStarted>(
                OnCrisisDepartureStarted);
            EventBus.Unsubscribe<AudienceCrisisDepartureEnded>(
                OnCrisisDepartureEnded);
            ReleaseAllImmediate();
            _deferRelayout = false;
            _started = false;
        }

        public void SynchronizeFromRoster()
        {
            if (!_started || !AudienceRosterSystem.HasInstance) return;

            ReleaseAllImmediate();
            IReadOnlyList<AudienceSnapshot> members =
                AudienceRosterSystem.Instance.Members;
            for (int i = 0; i < members.Count; i++)
                SpawnActor(members[i]);
            Relayout();
        }

        void OnAudienceJoined(AudienceJoined e)
        {
            if (!_started || _actors.ContainsKey(e.Audience.Id)) return;
            SpawnActor(e.Audience);
            Relayout();
        }

        void OnAudienceStateChanged(AudienceStateChanged e)
        {
            if (_actors.TryGetValue(e.Current.Id, out AudienceMemberActor actor))
                actor.ApplySnapshot(e.Current);
        }

        void OnAudienceCardReacted(AudienceCardReacted e)
        {
            if (!_actors.TryGetValue(e.Current.Id, out AudienceMemberActor actor)) return;
            actor.ApplySnapshot(e.Current);
            actor.PlayReaction(e.ReactionValue, e.EngagementDelta);
        }

        void OnAudienceDeparted(AudienceDeparted e)
        {
            AudienceId id = e.Audience.Id;
            if (!_actors.TryGetValue(id, out AudienceMemberActor actor)) return;

            _actors.Remove(id);
            _order.Remove(id);
            if (!_deferRelayout) Relayout();
            // 이탈 텍스트(PlayDeparture)는 단일 결과 UI 원칙으로 제거됨 — 퇴장 애니메이션(PlayExit)만 남긴다
            AudienceExitStyle exitStyle =
                e.Reason == AudienceDepartureReason.NearbyConcert
                    ? AudienceExitStyle.NearbyConcert
                    : AudienceExitStyle.Default;
            actor.PlayExit(exitStyle, () =>
            {
                if (actor != null && !SingletonRuntime.IsQuitting)
                    PoolManager.Despawn(actor);
            });
        }

        public bool TryGetRandomVisualAnchor(
            out Vector3 localPosition,
            out float scale,
            out int sortingOrder)
        {
            localPosition = default;
            scale = 1f;
            sortingOrder = baseSortingOrder;
            if (_order.Count == 0) return false;

            int start = Random.Range(0, _order.Count);
            for (int offset = 0; offset < _order.Count; offset++)
            {
                AudienceId id = _order[(start + offset) % _order.Count];
                if (!_actors.TryGetValue(id, out AudienceMemberActor actor) ||
                    actor == null ||
                    !actor.IsBound)
                    continue;

                localPosition = actor.LayoutLocalPosition;
                scale = actor.LayoutScale;
                sortingOrder = actor.SortingOrder;
                return true;
            }
            return false;
        }

        void OnCrisisTargetsChanged(AudienceCrisisTargetsChanged e)
        {
            foreach (AudienceMemberActor actor in _actors.Values)
            {
                if (actor != null) actor.SetCrisisThreatened(false);
            }
            if (!e.Active || e.Targets == null) return;

            for (int i = 0; i < e.Targets.Length; i++)
            {
                if (_actors.TryGetValue(
                        e.Targets[i],
                        out AudienceMemberActor actor))
                    actor.SetCrisisThreatened(true);
            }
        }

        void OnCrisisDepartureStarted(
            AudienceCrisisDepartureStarted e)
        {
            _deferRelayout = true;
        }

        void OnCrisisDepartureEnded(
            AudienceCrisisDepartureEnded e)
        {
            _deferRelayout = false;
            Relayout();
        }

        void SpawnActor(AudienceSnapshot audience)
        {
            AudienceMemberActor actor = PoolManager.Spawn(
                memberPrefab,
                memberRoot.position,
                Quaternion.identity,
                memberRoot);
            if (actor == null)
            {
                Debug.LogError("[AudiencePresenter] Failed to spawn AudienceMember.", this);
                return;
            }

            actor.name = $"AudienceMember_{audience.Id.Value:00}_{audience.Preference}";
            actor.Bind(
                audience,
                AudienceRosterSystem.Instance.EngagementConfig.CalmUpperBound);
            _actors.Add(audience.Id, actor);
            _order.Add(audience.Id);
        }

        void Relayout()
        {
            int count = _order.Count;
            if (count == 0) return;

            int columns = Mathf.Max(1, maximumColumns);
            int rows = Mathf.CeilToInt(count / (float)columns);
            for (int index = 0; index < count; index++)
            {
                AudienceId id = _order[index];
                if (!_actors.TryGetValue(id, out AudienceMemberActor actor)) continue;

                int row = index / columns;
                int rowStart = row * columns;
                int rowCount = Mathf.Min(columns, count - rowStart);
                int column = index - rowStart;
                float x = (column - (rowCount - 1) * 0.5f) * horizontalSpacing;
                float y = row * rowSpacing;
                float scale = frontRowScale * Mathf.Pow(rowScaleFalloff, row);
                int sortingOrder = baseSortingOrder + (rows - row);
                actor.SetLayout(new Vector3(x, y, 0f), scale, sortingOrder);
            }
        }

        void ReleaseAllImmediate()
        {
            foreach (AudienceMemberActor actor in _actors.Values)
            {
                if (actor == null) continue;
                if (!SingletonRuntime.IsQuitting && PoolManager.HasInstance)
                    PoolManager.Despawn(actor);
                else
                    Destroy(actor.gameObject);
            }
            _actors.Clear();
            _order.Clear();
        }

        bool ValidateDependencies()
        {
            if (memberPrefab == null)
            {
                Debug.LogError("[AudiencePresenter] AudienceMember prefab is required.", this);
                return false;
            }
            if (memberRoot == null)
            {
                Debug.LogError("[AudiencePresenter] Member root is required.", this);
                return false;
            }
            if (!AudienceRosterSystem.HasInstance ||
                !AudienceRosterSystem.Instance.IsConfigured)
            {
                Debug.LogError(
                    "[AudiencePresenter] An active configured AudienceRosterSystem is required.",
                    this);
                return false;
            }
            return true;
        }
    }
}
