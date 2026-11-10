# Audience Foundation

`AudienceRosterModel` is the single runtime authority for audience identity,
preference, engagement, and engagement stage.

## Runtime ownership

- `AudienceRosterModel`: pure roster and engagement calculations
- `AudienceRosterSystem`: Unity lifecycle, EventBus publication, and empty-roster game over
- `AudienceRosterPresenter`: one-to-one mapping between `AudienceId` and pooled actors
- `AudienceMemberActor`: per-member sprite, stage motion, warning bar, and transitions
- `AudienceReactionPopup`: pooled per-member card score or engagement-delta feedback
- `AudienceReactionVFX`: reusable per-member pixel particles, flash, ellipsis, and departure trail
- `AudienceReactionResolver`: pure preference + engagement-stage card calculation, plus grid-distance mode
- `AudienceEngagementConfig`: engagement range, low/high boundary, warning-bar threshold, decay, reaction multiplier
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
are signed values: positive results raise engagement and negative results lower
it. `AudienceCardReacted` is published before the corresponding
state/departure/summary events.

## Scene installation

`AudienceRuntime.prefab` is the portable scene root. Open the target scene, then
use `Tools/Audience/Install Individual Audience Runtime`. The installer places
or reuses the prefab, connects the scene `CardSystem`, and disables the former
Crowd/Hype/SpecialAudience runtime in that scene. It never saves the scene
automatically; review and save the active scene explicitly.

Use `Tools/Audience/Validate Active Scene` after installation. The command is
scene-name agnostic so the same prefab can be verified in `JWY.unity` now and
installed into `Main.unity` later without copying scene YAML.

## Engagement stages

Engagement has two stages: 하 (`Calm`) and 상 (`Excited`). Values at or below
`AudienceEngagementConfig.calmUpperBound` are 하; values above it are 상.
`Middle` stays in the enum only because stage values are serialized by number;
`ResolveStage` never returns it and `middleUpperBound` is unused.

The warning bar is independent of the stage boundary. It appears while
engagement is at or below `warningUpperBound`.

하 members move to outer positions and wander. They face their walking direction
and keep the last walking direction while standing, so they turn only when the
walking direction changes. `AudienceMemberActor.calmSpritesFaceLeft` states which
way the source 하 art faces (the 1101 calm sheets face left); the lost-interest
exit walk uses the same setting.

Crowd animation follows the same two stages. `Tools/Audience/Import Animated
Species Sheets` builds one 12-animal clip set per preference and stage from the
folders in `AudienceAnimationImportTool.Sources`. Each frame PNG is a 6x2 sheet
sliced into `_0`..`_11`, and a member plays clip `audienceId % 12` in every stage
so it keeps the same animal. Groups that are not in `Sources`, such as `Middle`,
are removed from `AudienceMember.prefab`. A `Sources` row can limit the clip to
its first N frames: the 1101 calm sheets use frames 0-5 only, because frame 6
repeats frame 0 and frames 7-11 are mirrored copies of 1-5. Run
`Tools/Audience/Rebuild Outline Regions` after changing crowd sprites.

## Card authoring

Audience reaction values live directly on each card prefab's `CardDefinition`:

- preference values: Chill, Singalong, Mosh
- engagement-stage values: Calm (하), Excited (상); Middle is unused
- reaction mode, and the target engagement for `GridDistance`
- engagement multiplier

### Grid-distance mode

The six performance cards use `AudienceReactionMode.GridDistance`. Each card has
one target cell: its `Target Preference` column (Chill, Singalong, Mosh) and its
`Target Engagement` row (상 or 하). For each member:

`(member preference score + member engagement score) x multiplier[distance]`

`distance` is the column difference plus 1 when the row differs (0..3). The
multipliers live in `CardDeckConfig.audienceDistanceMultipliers`. In this mode
the scores are magnitudes and must be zero or positive; the multiplier decides
the sign. Switching a card back to `PreferenceAndEngagement` restores the plain
`preference score + stage score` calculation.

Edit these values in Prefab Mode. There is no maximum-value clamp. The
`Card_Base.prefab` reaction is neutral, so performance-card variants own their
effective non-zero values. `Tools/Audience/Validate Audience Card Prefab Values`
checks the data but never writes or replaces authored values.

Performance cards calculate each member independently, using grid distance or
the plain `preference score + current engagement-stage score`.

The per-member results update engagement and their sum becomes the authoritative
card score. Results may be negative. Utility cards explicitly opt out of
audience reactions.

Special cards have two independent prefab-authored data sets:

- `AudienceReactionProfile`: general use when dropped anywhere other than a
  matching special audience, including a non-matching special audience
- `SpecialCardTargetEffect`: targeted use when dropped on the matching special
  audience

A successful targeted use does not also apply the general audience profile.
Target effects operate through `AudienceRosterSystem`; the former Crowd/Hype
calculation remains disabled.

`AudienceFlowTextUI` shows arrivals and departures as aggregated counts above the
crowd (`+1 관객` / `-3 관객`). It subscribes to `AudienceJoined` / `AudienceDeparted`
and ignores `Initialization` / `Reset` so filling the roster at showtime is not
announced. See `Scripts/Feedback/README.md`.

Each `AudienceMember` owns one reusable world-space reaction popup. It listens
through the presenter to `AudienceCardReacted`, displays the per-member reaction
score by default, and can be switched in the prefab Inspector to show the
actually applied engagement delta. Zero values are hidden by default so utility
cards do not create misleading feedback.

The same pooled member owns one `AudienceReactionVFX`. It creates its particle
renderers once and replays them without per-reaction Instantiate/Destroy:

- strong positive (`AudienceReactionPopup.strongReactionThreshold` or above):
  gold/orange `LOVE IT!` burst, one-frame white flash, and a short jump
- positive: restrained mint `INTERESTED` burst
- strong negative (`strongNegativeThreshold` or below): head-local muted square
  smoke, broken notes, vertical frustration lines, and a three-pixel ellipsis;
  gravity is disabled so the effect hangs around the head instead of falling
- Fever bonus: a dense white-to-gold burst of large voxel chunks and small
  gold-dust pixels, fired 0.05 seconds after the card impact
- departure: world-space dust every 0.1 seconds plus a 0.5-second direction arrow

Reaction colors are fixed feedback colors and never use the member preference
colors. Preference still answers what the member likes; reaction VFX only
answers how the last card landed.
