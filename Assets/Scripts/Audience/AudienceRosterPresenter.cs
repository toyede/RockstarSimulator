using System.Collections;
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
        [SerializeField, Min(0.1f)] float horizontalSpacing = 2.6f;
        [SerializeField, Min(0.1f)] float rowSpacing = 1.0f;
        [SerializeField, Min(0f)] float layoutJitter = .14f;
        [SerializeField, Tooltip("관객 발 기준 높이 보정")] float floorOffsetY = -0.4f;
        [SerializeField, Tooltip("관객 루트 기준 뒷줄 중심의 최대 Y. 점프와 머리가 HUD 위로 벗어나지 않게 한다")]
        float rearRowMaximumY = .65f;
        [SerializeField, Min(1f)] float layoutHalfWidth = 8.4f;
        [SerializeField, Min(0.01f)] float frontRowScale = 0.58f;
        [SerializeField, Range(0.5f, 1f)] float rowScaleFalloff = 0.86f;
        [SerializeField] int baseSortingOrder = 5;
        [SerializeField, Min(0f), Tooltip(
            "카드 중앙 임팩트 뒤에 관객별 반응이 터질 때까지의 실시간 지연")]
        float cardReactionPresentationDelay = 0.05f;

        readonly Dictionary<AudienceId, AudienceMemberActor> _actors =
            new Dictionary<AudienceId, AudienceMemberActor>();
        readonly List<AudienceId> _order = new List<AudienceId>(10);
        readonly Dictionary<AudienceId, int> _slots = new Dictionary<AudienceId, int>();
        readonly HashSet<AudienceMemberActor> _departing = new HashSet<AudienceMemberActor>();

        bool _started;
        bool _tutorialLayout;
        Camera _layoutCamera;
        float _lastAspect, _lastCameraSize;
        bool _deferRelayout;
        bool _revealAllPreferences;
        AudiencePreferenceHoverConfig _preferenceRevealConfig;

        public AudienceMemberActor MemberPrefab => memberPrefab;
        public Transform MemberRoot => memberRoot;
        public int VisibleCount => _actors.Count;
        sealed class ReactionGroup
        {
            public float Sum, Min = float.PositiveInfinity, Max = float.NegativeInfinity;
            public int Count;
            public float StartedAt;
        }
        ReactionGroup _reactionGroup = new ReactionGroup();

        public void PlayRainProtection()
        {
            foreach (var actor in _actors.Values)
                if (actor != null) actor.ReactionVFX.PlayProtection();
        }
        // CardResolved는 개별 AudienceCardReacted가 모두 발행된 뒤 온다.
        void OnGestureCardResolved(CardResolved e) => _reactionGroup = new ReactionGroup();
        void OnBossFanArrival(BossFanMoved e)
        {
            if (e.ToRival || e.Count <= 0) return;
            // Recruit는 Join을 먼저 발행한다. 가장 최근 생성된 ID만 도착 파장을 예약한다.
            int below = int.MaxValue;
            for (int i = 0; i < e.Count; i++)
            {
                AudienceMemberActor newest = null;
                foreach (var actor in _actors.Values)
                    if (actor != null && actor.BoundId.Value < below &&
                        (newest == null || actor.BoundId.Value > newest.BoundId.Value)) newest = actor;
                if (newest == null) break;
                below = newest.BoundId.Value; newest.MarkBossArrivalCue();
            }
        }

        /// <summary>
        /// 모든 현재/신규 일반 관객의 취향 테두리를 상시 표시한다.
        /// 실제 호버 여부와 말풍선은 AudiencePreferenceHoverController가 별도로 관리한다.
        /// </summary>
        public void SetAllPreferencesRevealed(
            bool revealed,
            AudiencePreferenceHoverConfig revealConfig)
        {
            _revealAllPreferences = revealed && revealConfig != null;
            _preferenceRevealConfig = _revealAllPreferences ? revealConfig : null;

            foreach (AudienceMemberActor actor in _actors.Values)
                ApplyPersistentPreferenceReveal(actor);
        }

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
            _tutorialLayout = TutorialFlow.IsRunning;
            EventBus.Subscribe<TutorialPlaybackChanged>(OnTutorialPlaybackChanged);
            EventBus.Subscribe<GameStateChanged>(OnPresentationState);
            EventBus.Subscribe<AudienceJoined>(OnAudienceJoined);
            EventBus.Subscribe<CardResolved>(OnGestureCardResolved);
            EventBus.Subscribe<BossFanMoved>(OnBossFanArrival);
            EventBus.Subscribe<AudienceStateChanged>(OnAudienceStateChanged);
            EventBus.Subscribe<AudienceCardReacted>(OnAudienceCardReacted);
            EventBus.Subscribe<FeverBonusAwarded>(OnFeverBonusAwarded);
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
            _layoutCamera = Camera.main;
            PoolManager.Prewarm(memberPrefab.gameObject, 10);
            SynchronizeFromRoster();
        }

        void OnDisable()
        {
            EventBus.Unsubscribe<TutorialPlaybackChanged>(OnTutorialPlaybackChanged);
            _tutorialLayout = false;
            EventBus.Unsubscribe<GameStateChanged>(OnPresentationState);
            StopAllCoroutines();
            EventBus.Unsubscribe<AudienceJoined>(OnAudienceJoined);
            EventBus.Unsubscribe<CardResolved>(OnGestureCardResolved);
            EventBus.Unsubscribe<BossFanMoved>(OnBossFanArrival);
            EventBus.Unsubscribe<AudienceStateChanged>(OnAudienceStateChanged);
            EventBus.Unsubscribe<AudienceCardReacted>(OnAudienceCardReacted);
            EventBus.Unsubscribe<FeverBonusAwarded>(OnFeverBonusAwarded);
            EventBus.Unsubscribe<AudienceDeparted>(OnAudienceDeparted);
            EventBus.Unsubscribe<AudienceCrisisTargetsChanged>(
                OnCrisisTargetsChanged);
            EventBus.Unsubscribe<AudienceCrisisDepartureStarted>(
                OnCrisisDepartureStarted);
            EventBus.Unsubscribe<AudienceCrisisDepartureEnded>(
                OnCrisisDepartureEnded);
            ReleaseAllImmediate();
            _deferRelayout = false;
            _revealAllPreferences = false;
            _preferenceRevealConfig = null;
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
            if (e.Reason == AudienceJoinReason.Initialization) ReleaseDeparting();
            if (!_started || _actors.ContainsKey(e.Audience.Id)) return;
            SpawnActor(e.Audience);
            Relayout();
        }

        void OnAudienceStateChanged(AudienceStateChanged e)
        {
            if (_actors.TryGetValue(e.Current.Id, out AudienceMemberActor actor))
            {
                actor.ApplySnapshot(e.Current);
                // 상태 갱신이 호버로 켠 테두리까지 끄지 않도록 상시 표시만 갱신한다.
                if (_revealAllPreferences) ApplyPersistentPreferenceReveal(actor);
            }
        }

        void OnAudienceCardReacted(AudienceCardReacted e)
        {
            if (!_actors.TryGetValue(e.Current.Id, out AudienceMemberActor actor)) return;
            actor.ApplySnapshot(e.Current);

            // CardResolved will show the finalized per-audience Fever award.
            // Keep raw positive and negative reaction values hidden during Fever.
            if (FeverSystem.HasInstance && FeverSystem.Instance.IsActive)
                return;

            ReactionGroup group = _reactionGroup;
            if (group.Count == 0) group.StartedAt = Time.time;
            if (e.ReactionValue > 0)
            {
                float x = actor.LayoutLocalPosition.x;
                group.Sum += x; group.Count++;
                group.Min = Mathf.Min(group.Min, x); group.Max = Mathf.Max(group.Max, x);
            }
            StartCoroutine(PlayReactionAfterDelay(e, group));
        }

        IEnumerator PlayReactionAfterDelay(AudienceCardReacted reaction, ReactionGroup group)
        {
            if (cardReactionPresentationDelay > 0f)
                yield return new WaitForSecondsRealtime(
                    cardReactionPresentationDelay);

            if (FeverSystem.HasInstance && FeverSystem.Instance.IsActive)
                yield break;
            if (_actors.TryGetValue(
                    reaction.Current.Id,
                    out AudienceMemberActor actor) &&
                actor != null)
            {
                actor.PlayReaction(
                    reaction.ReactionValue,
                    reaction.EngagementDelta);
                actor.PlayCardMotion(reaction.CardId, reaction.ReactionValue,
                    group.Count > 0 ? group.Sum / group.Count : actor.LayoutLocalPosition.x,
                    group.Min, group.Max, group.StartedAt);
            }
        }

        void OnFeverBonusAwarded(FeverBonusAwarded e)
        {
            if (e.AudienceCount <= 0 || e.BonusScore <= 0) return;

            int scorePerAudience = e.BonusScore / e.AudienceCount;
            if (scorePerAudience <= 0) return;

            StartCoroutine(PlayFeverBonusAfterDelay(scorePerAudience));
        }

        IEnumerator PlayFeverBonusAfterDelay(int scorePerAudience)
        {
            if (cardReactionPresentationDelay > 0f)
                yield return new WaitForSecondsRealtime(
                    cardReactionPresentationDelay);

            foreach (AudienceMemberActor actor in _actors.Values)
            {
                if (actor != null && actor.IsBound)
                    actor.PlayFeverBonus(scorePerAudience);
            }
        }

        void OnAudienceDeparted(AudienceDeparted e)
        {
            AudienceId id = e.Audience.Id;
            if (!_actors.TryGetValue(id, out AudienceMemberActor actor)) return;

            _actors.Remove(id);
            _order.Remove(id);
            _slots.Remove(id);
            if (!_deferRelayout) Relayout();
            if (e.Reason == AudienceDepartureReason.Reset)
            {
                ReleaseDeparting();
                if (actor != null && !IsTearingDown) PoolManager.Despawn(actor);
                return;
            }
            // 이탈 텍스트(PlayDeparture)는 단일 결과 UI 원칙으로 제거됨 — 퇴장 애니메이션(PlayExit)만 남긴다
            AudienceExitStyle exitStyle =
                e.Reason == AudienceDepartureReason.NearbyConcert
                    ? StageRuntimeDirector.CurrentStage != null && StageRuntimeDirector.CurrentStage.IsBoss
                        ? AudienceExitStyle.RivalStage : AudienceExitStyle.NearbyConcert
                    : e.Reason == AudienceDepartureReason.EngagementDepleted
                        ? AudienceExitStyle.LostInterest : AudienceExitStyle.Default;
            _departing.Add(actor);
            actor.PlayExit(exitStyle, () =>
            {
                _departing.Remove(actor);
                if (actor != null && !IsTearingDown)
                    PoolManager.Despawn(actor);
            });
        }

        /// <summary>
        /// 씬이 언로드되는 중인가. 이때 Despawn 을 부르면 파괴 중인 부모에 리페어런트를 시도해
        /// "Cannot set the parent ... while deactivating" 경고가 쏟아진다.
        /// </summary>
        bool IsTearingDown =>
            SingletonRuntime.IsQuitting ||
            !PoolManager.HasInstance ||
            !gameObject.scene.isLoaded;

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
                AudienceRosterSystem.Instance.EngagementConfig.WarningUpperBound);
            ApplyPersistentPreferenceReveal(actor);
            _actors.Add(audience.Id, actor);
            _order.Add(audience.Id);
            int slot = 0;
            while (_slots.ContainsValue(slot)) slot++;
            _slots.Add(audience.Id, slot);
        }

        void ApplyPersistentPreferenceReveal(AudienceMemberActor actor)
        {
            if (actor == null) return;

            if (!_revealAllPreferences || _preferenceRevealConfig == null)
            {
                actor.SetPreferenceReveal(false, default, 0f);
                return;
            }

            actor.SetPreferenceReveal(
                true,
                _preferenceRevealConfig.GetColor(actor.Snapshot.Preference),
                _preferenceRevealConfig.OutlineThickness,
                AudienceOutlineMode.Occluded);
        }

        /// <summary>밴드와 가까운 화면 아래쪽을 전경으로 보고, 발의 Y에 따라 크기를 정한다.</summary>
        public float GetDepthScaleAtY(float localGroundY)
        {
            if (_tutorialLayout) return frontRowScale;
            float nearY = floorOffsetY - .15f;
            float depth = Mathf.Clamp(localGroundY - nearY, 0f,
                Mathf.Max(0f, rearRowMaximumY + .25f - nearY));
            return frontRowScale * Mathf.Pow(rowScaleFalloff, depth / Mathf.Max(.1f, rowSpacing));
        }

        void Relayout()
        {
            int count = _order.Count;
            if (count == 0) return;

            int columns = _tutorialLayout ? Mathf.Min(count, 5) : Mathf.Max(1, maximumColumns);
            int capacity = AudienceRosterSystem.HasInstance ? AudienceRosterSystem.Instance.Capacity : count;
            int maximumRows = Mathf.CeilToInt(Mathf.Max(count, capacity) / (float)columns);
            // 현재 인원이 아닌 정원으로 간격을 정해 입퇴장 시 기존 자리를 유지한다.
            float verticalSpacing = maximumRows <= 1 ? rowSpacing : Mathf.Min(rowSpacing,
                Mathf.Max(.1f, rearRowMaximumY - floorOffsetY - layoutJitter) / (maximumRows - 1));
            float halfWidth = layoutHalfWidth;
            if (_layoutCamera != null && _layoutCamera.orthographic)
            {
                _lastAspect = _layoutCamera.aspect; _lastCameraSize = _layoutCamera.orthographicSize;
                halfWidth = Mathf.Min(halfWidth, Mathf.Max(1f, _lastCameraSize * _lastAspect - 1f));
            }
            for (int index = 0; index < count; index++)
            {
                AudienceId id = _order[index];
                if (!_actors.TryGetValue(id, out AudienceMemberActor actor)) continue;

                int slot = _tutorialLayout ? index : _slots[id];
                int row = slot / columns;
                int column = slot % columns;
                // 뒷줄을 반 칸 비우고, ID 기반 오프셋으로 게임 난수를 소비하지 않는다.
                uint hash = unchecked((uint)id.Value * 2654435761u);
                float jitterX = ((hash & 255) / 255f * 2f - 1f) * layoutJitter;
                float jitterY = (((hash >> 8) & 255) / 255f * 2f - 1f) * layoutJitter;
                // 좁은 화면에서는 간격부터 줄여 clamp로 여러 관객이 같은 끝점에 쌓이지 않게 한다.
                float spacing = Mathf.Min(horizontalSpacing,
                    Mathf.Max(.1f, (halfWidth - layoutJitter - .4f) * 2f / Mathf.Max(1f, columns - .5f)));
                float stagger = ((row & 1) == 1 ? .25f : -.25f) * spacing;
                float x = (column - (columns - 1) * .5f) * spacing + stagger + jitterX;
                float y = floorOffsetY + row * verticalSpacing + jitterY;
                float scale = frontRowScale * Mathf.Pow(rowScaleFalloff, row);
                actor.SetTutorialPlacement(_tutorialLayout);
                if (_tutorialLayout)
                {
                    float lessonSpacing = Mathf.Min(1.7f, spacing);
                    x = (column - (columns - 1) * .5f) * lessonSpacing;
                    // 정중앙 한 명만 남아도 앞쪽 너구리의 왕관에 가리지 않게 한다.
                    y = floorOffsetY + row * verticalSpacing + .4f;
                }
                int sortingOrder = baseSortingOrder + 10 - Mathf.RoundToInt(y * 2f);
                actor.SetWalkArea(Rect.MinMaxRect(-halfWidth, floorOffsetY - .15f,
                    halfWidth, rearRowMaximumY + .25f), slot);
                actor.SetLayout(new Vector3(x, y, 0f), scale, sortingOrder, this);
            }
        }

        void LateUpdate()
        {
            // 뷰 크기가 바뀔 때만 재배치한다. 매 프레임 관객/씬 검색은 하지 않는다.
            if (_started && _layoutCamera != null &&
                (!Mathf.Approximately(_lastAspect, _layoutCamera.aspect) || !Mathf.Approximately(_lastCameraSize, _layoutCamera.orthographicSize))) Relayout();
        }

        void OnTutorialPlaybackChanged(TutorialPlaybackChanged e)
        {
            _tutorialLayout = e.IsRunning;
            Relayout();
        }

        void OnPresentationState(GameStateChanged e)
        {
            if (e.Current == GameState.Ready || e.Current == GameState.GameOver) ReleaseDeparting();
        }

        void ReleaseDeparting()
        {
            foreach (AudienceMemberActor actor in _departing)
                if (actor != null && !IsTearingDown) PoolManager.Despawn(actor);
            _departing.Clear();
        }

        void ReleaseAllImmediate()
        {
            ReleaseDeparting();
            foreach (AudienceMemberActor actor in _actors.Values)
            {
                if (actor == null) continue;
                // 씬 언로드 중에는 Unity 가 알아서 정리한다 (리페어런트 시도 시 경고가 쏟아짐)
                if (!IsTearingDown)
                    PoolManager.Despawn(actor);
            }
            _actors.Clear();
            _order.Clear();
            _slots.Clear();
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
