# Audience Foundation

`AudienceRosterModel` is the single runtime authority for audience identity,
preference, engagement, and engagement stage.

## Runtime ownership

- `AudienceRosterModel`: pure roster and engagement calculations
- `AudienceRosterSystem`: Unity lifecycle, EventBus publication, and empty-roster game over
- `AudienceRosterPresenter`: one-to-one mapping between `AudienceId` and pooled actors
- `AudienceMemberActor`: per-member sprite, stage motion, warning bar, and transitions
- `AudienceReactionResolver`: pure preference + engagement-stage card calculation
- `AudienceEngagementConfig`: engagement range, stage boundaries, decay, reaction multiplier
- `AudienceFlowConfig`: initial/max count, preference weights, deterministic seed,
  natural-arrival check interval and chance

Consumers must treat `AudienceSnapshot` as read-only and identify members by
`AudienceId`, not by list or visual slot index.

## Events

- `AudienceJoined` — `Reason` is `Initialization` (roster reset), `NaturalArrival`
  (probabilistic inflow), or `RuntimeCommand` (debug/manual add)
- `AudienceStateChanged`
- `AudienceDeparted` — `Reason` is `EngagementDepleted`, `RuntimeRemoval`
  (debug/manual remove), or `Reset`
- `AudienceCardReacted`
- `AudienceSummaryChanged`

Natural decay, natural arrival, and the empty-roster game-over check are all
processed once per frame by `AudienceRosterSystem.Update()`, in that order:
decay/departure first, then game-over evaluation, then the arrival check. This
guarantees that if the last member departs and the roster becomes empty, game
over is requested before any new arrival can be evaluated in the same frame —
an arrival never revives an already-ended performance. A member that reaches
zero is removed before `AudienceDeparted` is published. Card reactions are
clamped to non-negative values and publish `AudienceCardReacted` before the
corresponding state/summary events.

Natural arrival is disabled when `AudienceFlowConfig.arrivalCheckInterval` or
`arrivalChance` is `0`. While the roster is full, the arrival timer does not
accumulate, so at most one check interval elapses after a slot opens before the
next arrival is possible.

## Main scene authority

`Main.unity` uses this system as its audience gameplay authority. It starts with
three roster members and creates exactly one `AudienceMember.prefab` actor for
each member. `CrowdSpawner`, `CrowdCompositionManager`, `CrowdMoodDirector`,
`HypeSystem`, and the former special-audience runtime are disabled in Main.

Performance cards calculate each member independently:

`max(0, preference score + current engagement-stage score)`

The per-member results update engagement and their sum becomes the authoritative
card score. Utility cards explicitly opt out of audience reactions.
