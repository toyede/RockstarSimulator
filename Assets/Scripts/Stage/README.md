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
| `StageBackgroundView` | `Background` 오브젝트. Base 스프라이트 교체(Order 0), 조명 레이어(Order 1), 전경 소품(Order 46), 장식 관객 교체 |
| `IStageRule` / `StageRuleBehaviour` | 룰 인터페이스와 공통 뼈대. `Activate(StageRuleContext)` / `Deactivate()` 중복 호출 안전 |
| `Rules/BuskingWalkInRule` | `busking_walk_in` — Stage 1. 기본 상태(특별 관객·위기 OFF) 그대로, 자연 유입 수만 센다 |
| `Rules/SpecialAudienceRequestsRule` | `special_audience_requests` — Stage 2~. 기존 `SpecialAudienceManager` 를 켜고 스테이지별 Config(Stage 2 = 18/24/8초)를 주입 |
| `Rules/CrisisEventRule` | `festival_nearby_concert`(확정 1회) / `arena_adaptive_event`(적응형) — 기존 `NearbyConcertCrisisDirector` 에 Config 주입 후 활성화 |
| `Rules/RivalCrowdChallengeRule` | `rival_crowd_challenge` — Boss. 30/65/100초에 성향을 예고(7초)하고 최대 2명 위협. 호응 60+ 또는 해당 성향 Special Hit 로 방어. 기존 AudienceCrisis* 이벤트를 재발행해 외곽선·경고 UI 를 재사용 |
| `Rules/RivalAttackNoticeUI` | 보스 공격 문구 (라이벌 이름·성향). 런타임에 캔버스를 스스로 만든다 |

## 보스전 (`Boss/`, 룰 `boss_battle`)

| 파일 | 역할 |
|---|---|
| `BossBattleConfig` | 수치 전부. `Settings/Tour/RuleConfigs/BossBattle.asset` |
| `BossBattleRule` | 체력(= 목표 점수, 카드 점수가 피해) · 패턴 스케줄(10초 첫 예고, 종료 후 12초, PEAK TIME 8초) · 성공 12% 피해 + 상대 팬 합류 / 실패 5% 회복 + 우리 팬 이탈 · 체력 50% 이하 PEAK TIME(강화 패턴, 진입 즉시 DROP) · 체력 0 또는 5연속 성공 시 즉시 격파(남은 초 × 40점) · 클리어 판정을 `StageRuntimeDirector.ClearVerdictOverride` 로 브리지에 전달 |
| `BossPattern` | B2B(덱의 Normal/Special 지목 카드 사용) · GUEST LIST(손패 Special 로 상대 팬 영입) · BEATMATCH(콤보 유지 + 카드 N장) · KILL SWITCH(성향 봉인, 양수 반응 N번) · DROP(피버 진입, PEAK TIME 전용) |
| `BossBattleUI` | 상단 체력 바 · 연속 성공 점 · 패턴 예고/카운트다운/진행도 · PEAK TIME/격파 자막. 런타임 자체 생성 |
| `RivalStagePlaceholder` | 라이벌 단상·듀오·전광판(라이벌 무대 구역)·대기 팬(스탠딩석 구역)을 Square 로 표현. 아트가 오면 스프라이트 교체 |
| `BossArenaLayout` | 3화면 배치(백지). 우리 무대 x=0 은 그대로, 왼쪽에 관객 스탠딩석(x=−19.2)·라이벌 무대(x=−38.4) Square 구역. 구역 폭 = 배경 아트 1920px/PPU 100. `standingBackground`/`rivalBackground` 에 스프라이트를 넣으면 Square 대신 사용 |
| `BossCameraDirector` | Main Camera x 만 구역 사이로 팬(SmoothStep). 자유 스크롤 없음, 해제·종료 시 즉시 홈 |
| `BossStagePresentation` | 연출 담당. 패턴 예고·격파 때 **카드 잠금(`CardInput.Locked`) + 공연 타이머·호응·관객 몰입도 감소 정지 → 검은 틴트 + 손패 하강(`CardHandUI.SetStowed`) → 라이벌 무대로 팬 → 체류 → 복귀 → 해제**. 보스전 동안 랭크·점수·시간 HUD 를 숨기고(CanvasGroup) 남은 시간은 체력 바 아래에 표시. 시간·틴트·거리는 `BossBattleConfig` "연출" 항목 |
| `Editor/BossBattleSetup` | `Tools/Tour/Setup Boss Battle`: 설정 에셋 · Stage05Boss 룰 ID · Main 씬 `[StageRuntime]/Rule_BossBattle` (룰·UI·플레이스홀더·3화면 연출 컴포넌트) |

패턴 흐름: 예고(`BossPatternAnnounced`, 연출 약 3.8초, 타이머 정지) → 복귀 후 창 시작(`BossPatternStarted`) → 판정. 격파는 `BossDefeated` → 라이벌 무대 소등 연출 → GameOver.

디버그: 보스 룰 활성 중 `F6` 라이벌 무대 왕복 미리보기, `F7` 체력 −25%, `F8` 다음 패턴 즉시, `F9` 현재 패턴 성공.

노드 5개 유지: `Tools/Tour/Restore Five Nodes` (Title 런처의 스테이지 목록만 Stage01~05 로 되돌린다. Stage04 아트가 오면 `StageVisualCatalog` 의 stage_04 배경만 교체).

## 셋업

```
Tools/Tour/Setup Stage Runtime      프리셋·룰 설정·카탈로그 생성 + StageDefinition ID 입력 + Main 씬 배치 (실행 후 Ctrl+S)
Tools/Tour/Fix Stage Sprite Import  Sprites/Stages 를 Single · Point · Mipmap Off · PPU 100 으로 교정
```

이미 있는 에셋은 덮어쓰지 않는다. 수치를 바꾸려면 `Settings/Tour/AudiencePresets`, `Settings/Tour/RuleConfigs` 의 에셋을 인스펙터에서 고친다.

## 기존 시스템에 추가한 최소 API

- `AudienceRosterSystem.ConfigureForStage(AudienceStagePreset)` — 모델 재생성 + 관객 초기화 (Playing 전 1회)
- `SpecialAudienceManager.SetRuntimeConfig(config)` / `SetAutoStart(bool)`
- `NearbyConcertCrisisDirector.ConfigureForStage(config, active)`
- `DecorativeCrowd.ReplaceVariants(sprites)`
- `TutorialFlow.skipDuringTourStage` — 투어 스테이지 중에는 튜토리얼 자동 실행 안 함

## 투어 없이 Main 을 직접 실행할 때

`[StageRuntime]` 의 `fallbackStage` 가 비어 있으면 씬 기본값(게임잼 버전)으로 돌아간다. 특정 스테이지를 바로 보려면 `fallbackStage` 에 `Stage0x.asset` 을 넣는다.

## 디버그 (에디터·개발 빌드, 씬 배치 불필요 — `TourDebugInput` 이 스스로 생긴다)

| 키 | 동작 |
|---|---|
| `Home` | 점수를 목표 점수로 맞추고 공연 종료 (클리어) |
| `Shift+Home` | 목표 ×5 로 S 랭크 클리어 |
| `Delete` | 점수 0 으로 실패 |
| `F10` (허브) | 현재 투어 단계를 한 칸 자동 진행 (공연은 클리어 처리) |
