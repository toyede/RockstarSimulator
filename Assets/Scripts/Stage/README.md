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
