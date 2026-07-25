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

## Scene installation

`AudienceRuntime.prefab` is the portable scene root. Open the target scene, then
use `Tools/Audience/Install Individual Audience Runtime`. The installer places
or reuses the prefab, connects the scene `CardSystem`, and disables the former
Crowd/Hype/SpecialAudience runtime in that scene. It never saves the scene
automatically; review and save the active scene explicitly.

Use `Tools/Audience/Validate Active Scene` after installation. The command is
scene-name agnostic so the same prefab can be verified in `JWY.unity` now and
installed into `Main.unity` later without copying scene YAML.

## Card authoring

Audience reaction values live directly on each card prefab's `CardDefinition`:

- preference values: Chill, Singalong, Mosh
- engagement-stage values: Calm, Middle, Excited
- engagement multiplier

Edit these values in Prefab Mode. There is no maximum-value clamp. The
`Card_Base.prefab` reaction is neutral, so performance-card variants own their
effective non-zero values. `Tools/Audience/Validate Audience Card Prefab Values`
checks the data but never writes or replaces authored values.

Performance cards calculate each member independently:

`max(0, preference score + current engagement-stage score)`

The per-member results update engagement and their sum becomes the authoritative
card score. Utility cards explicitly opt out of audience reactions.
