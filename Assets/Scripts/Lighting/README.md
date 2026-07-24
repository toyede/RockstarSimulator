# 무대 조명 (CONTEXT STAGE)

열기 단계(`HeatStage`)를 조명 색·강도·펄스로 보여주고, Special Hit 순간 화면을 번쩍인다.
URP 2D Light **3개만** 쓴다 (Global 1 + 좌우 2). Shader Graph·노멀맵·ShadowCaster2D·Bloom 없음.

## 1. 셋업

```
Tools/Lighting/Setup Stage Lighting   →   Ctrl+S
```

씬에 이미 있는 `Global Light 2D` 를 **재사용**하고(두 번째 Global Light 를 만들지 않는다),
좌우 Point Light 2개를 만들어 컨트롤러에 연결한다.

```
StageLighting                    ← StageLightController + StageLightEventBridge
├── Global Light 2D              ← 기존 것을 그대로 사용 (플래시도 이 조명)
├── Left Stage Light 2D          ← Point, z회전 160°
└── Right Stage Light 2D         ← Point, z회전 20°
```

## 2. 단계별 값 (전부 인스펙터 조정 가능)

| | 색 | Global 강도 | 무대 기준 강도 | 펄스 속도 | 펄스 폭 |
|---|---|---|---|---|---|
| Chill | `#31DFEA` | 0.55 | 0.75 | 0.6 | 0.04 |
| Singalong | `#6431EA` | 0.70 | 1.00 | 1.5 | 0.10 |
| Mosh | `#F01F1F` | 0.80 | 1.25 | 3.0 | 0.18 |

전환 시간 0.35초. 좌우 조명은 `rightPulsePhaseOffset`(1.5)만큼 위상이 어긋나 같이 뛰지 않는다.
Global Light 은 펄스시키지 않는다 — 화면 전체가 깜빡이면 UI 가독성이 떨어진다.
`stageIntensityCeiling`(2.0)으로 스프라이트가 하얗게 타는 것을 막는다.

## 3. 공개 API

```csharp
controller.SetHeatStage(HeatStage.Mosh);          // 0.35초 보간
controller.SetHeatStage(HeatStage.Chill, true);   // 즉시 (리셋용)
controller.PlaySpecialHitFlash();                 // 흰색 플래시
controller.PlaySpecialHitFlash(Color.yellow);     // 색 지정
controller.PlaySpecialHitFlash(HeatStage.Mosh);   // 요청 타입 색으로
```

## 4. 연결 (`StageLightEventBridge`)

두 시스템 **어느 쪽도 수정하지 않고** 이미 발행 중인 EventBus 이벤트만 구독한다.

| 이벤트 | 처리 |
|---|---|
| `HypeChanged` | `HypeConfig.ResolveStage(e.Value)` 로 단계를 계산해 **바뀐 순간만** 조명 갱신 |
| `SpecialHitLanded` | 요청 타입 색으로 플래시 (점수·열기는 건드리지 않음) |
| `GameStateChanged` | Ready/Playing 에 현재 열기 단계로 맞춤 (이전 판 색 잔상 제거) |

단계 판정에 **카드 판정과 같은 `HypeConfig.ResolveStage()`** 를 쓰므로,
조명이 보여주는 단계와 카드가 채점하는 단계가 어긋날 수 없다.

## 5. 왜 코루틴을 안 쓰나

전환·플래시 모두 `Update` 에서 **매 프레임 현재 단계 값으로부터 최종 색·강도를 새로 만든다.**
이전 프레임 값을 누적하지 않기 때문에:

- 플래시가 연속으로 들어와도 타이머만 리셋되고 강도가 쌓이지 않는다
- 플래시가 끝나면 반드시 현재 단계 색으로 **정확히** 복구된다
- 컴포넌트를 Disable 해도 정리할 코루틴 자체가 없다 (`OnDisable` 에서 단계 값으로 되돌리기만 함)

전환 도중 새 단계가 들어오면 "지금 보이는 값"을 출발점으로 삼아 튀지 않는다.

## 6. 디버그

인스펙터 컴포넌트 우측 `⋮` 메뉴:
`Debug/Set Chill` · `Set Singalong` · `Set Mosh` · `Play Special Hit Flash`

`enableDebugKeys` 를 켜면 Play Mode 에서 `7`/`8`/`9` = 단계, `0` = 플래시.
(에디터·개발 빌드에서만 컴파일된다. 기본값은 꺼짐 — 카드 숫자키와 헷갈리지 않도록)

## 7. Sorting Layer / Material

- 이 프로젝트의 Sorting Layer 는 **`Default` 하나뿐**이라 조명 대상 레이어를 나눌 것이 없다.
  없는 레이어를 만들지 않았다.
- **UI 는 Screen Space Overlay Canvas 라 Light2D 영향을 원래 받지 않는다.** 카드 UI 밝기는 안전하다.
  (나중에 World Space Canvas 로 바꾸면 그때 Light2D 의 Target Sorting Layers 를 손봐야 한다)
- 씬의 SpriteRenderer 와 런타임 생성 관객·특별 관객 모두 URP 기본 **`Sprite-Lit-Default`** 를 쓴다.
  머티리얼을 바꿀 것이 없어 아무것도 건드리지 않았다.
