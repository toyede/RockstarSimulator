# Stage — 스테이지 런타임 (기획서 §10)

투어의 `StageDefinition` 하나를 Main 씬에 적용하는 계층. 점수·카드 판정·증강 효과는 건드리지 않는다.

```
StageDefinition (Settings/Tour/Stage0x.asset)
  ├ audiencePresetId ─→ StageContentCatalog (Resources/Stages) ─→ AudienceStagePreset
  ├ venueRuleIds     ─→ [StageRuntime] 자식의 StageRuleBehaviour (ruleId 일치)
  └ stageId          ─→ StageVisualCatalog (Resources/Stages) ─→ 배경·조명·소품·룰 카드
```

| 파일 | 역할 |
|---|---|
| `StageRuntimeDirector` | Main 씬 `[StageRuntime]`. Awake(실행 순서 100)에서 현재 스테이지를 읽어 관객 프리셋 적용 → 특별 관객·위기 OFF 기준 상태 → `venueRuleIds` 룰만 활성화. GameOver 에 전부 해제, Ready 에 재적용. `StageRuntimeApplied` 발행 |
| `AudienceStagePreset` | 스테이지별 관객 수·최대·성향 가중치·시작 호응도·자연 감소·유입. `AudienceRosterSystem.ConfigureForStage` 에 넘긴다 (전역 Config 는 수정하지 않음) |
| `StageContentCatalog` | presetId → 프리셋. 빈/중복 ID 검증 |
| `StageVisualCatalog` | stageId → Base 배경, 조명 레이어, 좌우 전경 소품, 장식 관객 변형, 맵 썸네일, 룰 아이콘, 룰 카드 제목/본문 |
| `StageBackgroundView` | `Background` 오브젝트. Base 스프라이트 교체(Order 0). 씬에 `[StageSets]/<stageId>` 소품 세트가 있으면 그것을 켜고, 없으면 카탈로그로 조명 레이어(Order 1)·전경 소품(Order 46)을 런타임 생성. 장식 관객 교체 |
| `StageSet` | 스테이지별 소품·조명 묶음 (씬 오브젝트). `ST01_FG_AmpLeft` 같은 소품이 각자 GameObject 라 **위치·크기·회전을 씬에서 바로 조절**하고, 나중에 Animator·스쿼시/바운스 컴포넌트를 붙일 수 있다. 코드에서는 `StageBackgroundView.ActiveSet.Props` / `FindProp("ST01_FG_AmpLeft")` 로 접근 |
| `IStageRule` / `StageRuleBehaviour` | 룰 인터페이스와 공통 뼈대. `Activate(StageRuleContext)` / `Deactivate()` 중복 호출 안전 |
| `Rules/BuskingWalkInRule` | `busking_walk_in` — Stage 1. 기본 상태(특별 관객·위기 OFF) 그대로, 자연 유입 수만 센다 |
| `Rules/SpecialAudienceRequestsRule` | `special_audience_requests` — Stage 2~. 기존 `SpecialAudienceManager` 를 켜고 스테이지별 Config(Stage 2 = 18/24/8초)를 주입 |
| `Rules/CrisisEventRule` | `festival_nearby_concert`(확정 1회) / `arena_adaptive_event`(적응형) — 기존 `NearbyConcertCrisisDirector` 에 Config 주입 후 활성화 |
| `Rules/RivalCrowdChallengeRule` | `rival_crowd_challenge` — Boss. 30/65/100초에 성향을 예고(7초)하고 최대 2명 위협. 호응 60+ 또는 해당 성향 Special Hit 로 방어. 기존 AudienceCrisis* 이벤트를 재발행해 외곽선·경고 UI 를 재사용 |
| `Rules/RivalAttackNoticeUI` | 보스 공격 문구 (라이벌 이름·성향). 런타임에 캔버스를 스스로 만든다 |

## 기믹 이벤트 (`Events/`) — Stage 3·4 에 집중, Stage 5 는 하나

공통 뼈대 `StageEventRule`: 한 번에 하나만 진행(전역 게이트), 특별 관객 요청 중에는 미룸, `StageEventStarted/Progress/Resolved` 로 알림·기록. UI 는 `StageEventNoticeUI`(상단, 룰 참조 없음). 수치는 `[StageRuntime]/Rule_*` 인스펙터. `F5` 로 아직 안 돌린 이벤트를 즉시 시작.

| 스테이지 | 룰 ID | 발동 | 할 일 | 성공 / 실패 |
|---|---|---|---|---|
| 3 | `festival_rain_shower` 소나기 | 45초, 12초 창 | 몰입도 감소 2배. CHILL 카드 좋은 반응 = 우산(전 관객 +6) | 관객 수 유지 / 자연 이탈이 벌칙 |
| 3 | `festival_flag_wave` 깃발 웨이브 | 콤보 10 도달(쿨다운 25초, 최대 3회), 4초 창 | Good 이상 카드 2장 | 전 관객 +8, 관객 +2 / 없음 |
| 4 생방송 사고 | `arena_big_screen` 큐시트 | 40·80초, 12초 창 | 카메라 큐 순서대로 성향 카드 (1회차 3개, 2회차 4개, 틀리면 처음부터) | 목표 10% 점수 + 전 관객 +5 / 관객 1명 이탈 |
| 4 | `arena_blackout` 정전 | 58·100초, 8초 창 | 불이 꺼지고 관객·밴드가 검은 실루엣, 호버 성향 표시 차단, BGM 정지. 관객을 기억해 카드 1장 이상, Miss 0 | 목표 15% 점수 + 몰입 +5 / 벌칙 없음 (보너스만 놓침) |
| 4 | (보류) `arena_vip_entrance` | 컴포넌트만 남김, 룰 목록에서 제외 | | |

Stage 4 목표 점수는 11,000 으로 올려 두었다 — 정전 보너스(1,650 × 2) 없이는 카드만으로 닿기 어렵다. 정전 연출은 `BlackoutPresentation`(실루엣 틴트·조명 `StageLightController.SetBlackout`·비상등 스윕·스캔라인 오버레이·BGM 일시정지·호버 차단)이 맡고, 카드 판정 텍스트는 그대로 보인다.
| 5 | `stadium_contested_fan` 팬 쟁탈 | 35·85초(보스 패턴·예고 중이면 대기), 6초 창 | 외친 성향 카드로 좋은 반응 | 팬 합류(만석이면 환호만) / 없음 |

난이도: 3은 보상형·약한 벌칙, 4는 판단(순서·시간)과 이탈 벌칙, 5는 보스전이 이미 바쁘므로 잃는 것 없는 이벤트 하나. 결과는 `PerformanceReport.stageEvents*` 로 신문 기사에 "기믹 이벤트 n / m회 성공" 으로 붙는다.

## 보스전 (`Boss/`, 룰 `boss_battle`)

| 파일 | 역할 |
|---|---|
기획: `Docs/BOSS_STAGE_REDESIGN_KO.md` (v2, 관객 쟁탈전). **체력 없음.** 라이벌 팬 수만큼 주기적으로 점수가 깎이고, 시간 종료 시 점수 ≥ 목표면 승리(타이머 판정 그대로, 랭크도 그대로).

| 파일 | 역할 |
|---|---|
| `BossBattleConfig` | 수치 전부. `Settings/Tour/RuleConfigs/BossBattle.asset` — 라이벌 팬 14, 드레인 4초마다 팬당 12점(8초부터), 패턴 첫 10초·간격 10초(REVENGE 7초)·창 8초, 성공 팬 +2(강화 +3, DROP +3)·보너스 목표 6%(DROP 10%), 실패 팬 −1, REVENGE 진입 ≤6 / 해제 ≥9 |
| `BossBattleRule` | 팬 = 우리 관객(로스터) + 라이벌 팬. 드레인 틱(`PerformanceTimer.Elapsed` 기준이라 예고 연출 중엔 멈추고 엿보기 중엔 흐름) · 패턴 스케줄 · 성공/실패 팬 이동(`StealFans`/`LoseOurFans`) · 자연 이탈은 라이벌로 건너감 · REVENGE 진입/해제. 승패 판정은 하지 않는다 |
| `BossPattern` | B2B(덱의 Normal/Special 지목 카드 사용) · GUEST LIST(손패 Special 로 요청 성공) · BEATMATCH(콤보 유지 + 카드 N장) · KILL SWITCH(성향 봉인, 양수 반응 N번) · DROP(피버 진입, REVENGE 전용) |
| `BossBattleUI` | 상단 **팬 쟁탈 바**(라이벌 | 우리, 인원) · 다음 감소 카운트다운 · 감소 팝업(−N) · 패턴 예고/카운트다운/진행도 · "REVENGE TIME!" 자막. 랭크·점수·시간 HUD 는 그대로 보인다 |
| `RivalStagePlaceholder` | 라이벌 무대(단상·듀오·LED)와 그 앞에 줄지어 선 라이벌 팬을 Square 로. 팬 수는 룰과 동기화, 이동은 걸어서(우리 ↔ 라이벌) |
| `BossArenaLayout` | 2화면 배치(백지): 라이벌 무대(x=−19.2) | 우리 무대(x=0). `rivalBackground` 에 스프라이트를 넣으면 Square 대신 사용 |
| `BossCameraDirector` | Main Camera x 만 구역 사이로 팬(SmoothStep). 해제·종료 시 즉시 홈 |
| `BossStagePresentation` | 연출 담당. **예고**: 카드 잠금 + 타이머·호응·몰입도 감소 정지 → 틴트 + 손패 하강 → 라이벌 무대 왕복 → 해제. **엿보기 버튼**(화면 왼쪽 "< 라이벌 무대"): 같은 연출이지만 타이머·드레인은 멈추지 않는다, 버튼을 다시 눌러 복귀, 패턴 중엔 비활성 |
| `Editor/BossBattleSetup` | `Tools/Tour/Setup Boss Battle`: 설정 에셋 · Stage05Boss 룰 ID · Main 씬 `[StageRuntime]/Rule_BossBattle` |

패턴 흐름: 예고(`BossPatternAnnounced`, 연출 약 3.8초, 타이머 정지) → 복귀 후 창 시작(`BossPatternStarted`) → 판정(`BossPatternResolved`, 팬 이동 `BossFanMoved`). 팬 분포는 `BossFanBalanceChanged`, 드레인은 `BossDrainCountdown`/`BossDrainApplied`.

디버그: `F4` 드레인 즉시, `F5` 기믹 이벤트 즉시 시작, 보스 룰 활성 중 `F6` 라이벌 무대 왕복 미리보기, `F7` 라이벌 팬 −2, `F8` 다음 패턴 즉시, `F9` 현재 패턴 성공.

노드 5개 유지: `Tools/Tour/Restore Five Nodes` (Title 런처의 스테이지 목록만 Stage01~05 로 되돌린다).

배경: 스테이지 1~3 은 `Sprites/0910_art/배경` 의 v2 아트 (`Tools/Tour/Apply 0910 Art` 가 카탈로그·[StageSets] 를 갱신). 파일명 `stage4_background_v2` 가 스테이지 3 이다. 스테이지 4·5 는 아직 이전 배경 — 아트가 오면 `StageVisualCatalog` 의 해당 배경만 교체하고 `Setup Stage Sets` 를 다시 돌린다.

## 셋업

```
Tools/Tour/Setup Stage Runtime      프리셋·룰 설정·카탈로그 생성 + StageDefinition ID 입력 + Main 씬 배치 (실행 후 Ctrl+S)
Tools/Tour/Setup Stage Sets         카탈로그의 조명·전경 소품을 Main 씬 Background/[StageSets]/<stageId> 아래 씬 오브젝트로 배치 (있는 세트는 유지)
Tools/Tour/Fix Stage Sprite Import  Sprites/Stages 를 Single · Point · Mipmap Off · PPU 100 으로 교정
```

이미 있는 에셋은 덮어쓰지 않는다. 수치를 바꾸려면 `Settings/Tour/AudiencePresets`, `Settings/Tour/RuleConfigs` 의 에셋을 인스펙터에서 고친다.

## 기존 시스템에 추가한 최소 API

- `AudienceRosterSystem.ConfigureForStage(AudienceStagePreset)` — 모델 재생성 + 관객 초기화 (Playing 전 1회)
- `SpecialAudienceManager.SetRuntimeConfig(config)` / `SetAutoStart(bool)`
- `NearbyConcertCrisisDirector.ConfigureForStage(config, active)`
- `DecorativeCrowd.ReplaceVariants(sprites)`
- `TutorialFlow.skipDuringTourStage` — 투어에서는 첫 스테이지 첫 플레이만 튜토리얼 자동 실행 (에디터·개발 빌드는 항상, `Scripts/Tutorial/README.md` §0)

## 투어 없이 Main 을 직접 실행할 때

`[StageRuntime]` 의 `fallbackStage` 가 비어 있으면 씬 기본값(게임잼 버전)으로 돌아간다. 특정 스테이지를 바로 보려면 `fallbackStage` 에 `Stage0x.asset` 을 넣는다.

## 디버그 (에디터·개발 빌드, 씬 배치 불필요 — `TourDebugInput` 이 스스로 생긴다)

| 키 | 동작 |
|---|---|
| `Home` | 점수를 목표 점수로 맞추고 공연 종료 (클리어) |
| `Shift+Home` | 목표 ×5 로 S 랭크 클리어 |
| `Delete` | 점수 0 으로 실패 |
| `F10` (허브) | 현재 투어 단계를 한 칸 자동 진행 (공연은 클리어 처리) |
