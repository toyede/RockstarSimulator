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
- `AudienceFlowConfig`: initial/max count, preference weights, deterministic seed

Consumers must treat `AudienceSnapshot` as read-only and identify members by
`AudienceId`, not by list or visual slot index.

## Events

- `AudienceJoined`
- `AudienceStateChanged`
- `AudienceDeparted`
- `AudienceCardReacted`
- `AudienceSummaryChanged`

Natural decay is processed centrally by `AudienceRosterSystem`. A member that
reaches zero is removed before `AudienceDeparted` is published. Card reactions
are clamped to non-negative values and publish `AudienceCardReacted` before the
corresponding state/summary events.

## Main scene authority

`Main.unity` uses this system as its audience gameplay authority. It starts with
three roster members and creates exactly one `AudienceMember.prefab` actor for
each member. `CrowdSpawner`, `CrowdCompositionManager`, `CrowdMoodDirector`,
`HypeSystem`, and the former special-audience runtime are disabled in Main.

Performance cards calculate each member independently:

`max(0, preference score + current engagement-stage score)`

The per-member results update engagement and their sum becomes the authoritative
card score. Utility cards explicitly opt out of audience reactions.
