# 튜토리얼 (CONTEXT STAGE)

> **"옷은 무엇을 좋아하는지, 움직임은 지금 얼마나 신났는지 알려줍니다."**

별도 씬 없이 **실제 게임 씬 위에서** 관객 구성·카드 사용을 고정해 진행하는 70~90초 미니 공연.
항상 우상단 **[튜토리얼 건너뛰기]** 버튼으로 스킵할 수 있고, 완료/스킵하면 `Save("tutorial_done")`에
기록되어 다음 실행부터 뜨지 않는다.

## 1. 셋업

```
Tools/Tutorial/Setup Tutorial   →   Ctrl+S  (Main.unity 에서!)
```

`[Tutorial]`(TutorialFlow + TutorialTips) + `TutorialCanvas`(메시지 패널·스킵 버튼·팁 텍스트)가
생기고 전부 연결된다.

## 2. 진행 (기획안 → 구현 매핑)

| 단계 | 내용 | 진행 조건 |
|---|---|---|
| Intro | 관객 Mosh 1명 등장, 손패는 성향별 고정 카드 3장(Mosh/Singalong/Chill), 딤 + 핵심 문장 | 클릭 |
| PrefMosh | Mosh 관객 스포트라이트, **정답 공개** | 맞는 카드 사용 |
| PrefSingalong | Mosh 퇴장 → Singalong 관객 등장, **절반 힌트** | 맞는 카드 사용 |
| PrefChill | Singalong 퇴장 → Chill 관객 등장, **힌트 없음** — 직접 판단 | 맞는 카드 사용 |
| CrossUse→Explain | 세 관객(Mosh/Singalong/Chill) 재소집 → 아무 공연 카드 사용 → 실제 총반응 수치로 교차 반응 설명 | 사용 → 클릭 |
| Excitement | Chill 관객을 지루 상태로 만들고 구하게 함 | Chill 카드 사용 |
| CrowdChange | 흥분한 Singalong 관객 입장, **2초 관찰 강제** | 클릭 |
| FinalRun | 관객 6명(지루한 Chill 2 포함), **30초 · 반응 +100 · 이탈 ≤1** | 타이머 |
| Complete/Fail | 성공 → 본 공연 시작 / 실패 → **미니 공연만 재시작** | 클릭 |

- 튜토리얼 시작 손패는 랜덤이 아니라 성향별 카드 1장씩, 총 3장으로 고정된다 (`CardSystem.SetHand`)
- 관객은 Intro~PrefChill 구간에서 한 명씩만 등장해 세 성향을 순서대로 경험하고, CrossUse부터
  세 관객이 함께 재소집된다 (`SetRoster`)
- 틀린 카드는 **소모되지 않고 손패로 돌아오며** 힌트 문구가 뜬다 (CardInput.UseFilter 훅)
- 손패에 정답 카드가 없으면 소프트락 방지를 위해 전부 허용된다
- 스포트라이트 글로우 색은 대상 관객의 성향(Chill 청록/Singalong 보라/Mosh 빨강)에 맞춰 바뀐다
- 튜토리얼 중: 자연 유입 정지 · 관객 개별 몰입도 자연 감소 정지 · 전역 Hype 감소 정지 ·
  특별 관객 정지 · Crisis 정지 → 끝나면 전부 복구
- 끝나면 로스터/콤보/점수/호응도 리셋 후 **깨끗한 본 공연** 시작

## 3. 연출

- **딤**: 월드 전용 검은 스프라이트(sortingOrder 400). 카드·HUD는 Screen Space 라 안 어두워짐
- **스포트라이트**: 대상 관객 렌더러를 +500 부스트 + 발밑에 런타임 생성 글로우(따라다님)

## 4. 본 게임 컨텍스트 팁 (TutorialTips)

처음 발생하는 순간 1회씩, 4초 표시 후 페이드:

| 트리거 | 문구 |
|---|---|
| 첫 콤보(≥2) | "긍정적인 총반응을 이어가면 COMBO 배율이 올라갑니다!" |
| 첫 특별 관객 | "원하는 카드를 그 관객 위로 직접 드래그해 전달하세요." |
| 첫 Crisis 경고 | "경고가 끝나기 전에 공연을 달아오르게 하세요." |
| 첫 유틸리티 카드 | "특수 카드로 손패의 흐름을 바꿀 수 있습니다." |

## 5. 팀원 파일에 추가한 최소 훅 (기본값에서 아무 동작 없음)

| 파일 | 훅 |
|---|---|
| `CardInput.cs` | `static UseFilter` / `UseBlocked` — null 이면 평소와 동일 |
| `AudienceRosterSystem.cs` | `SuppressNaturalArrivals` — false 면 평소와 동일 |
| `AudienceRosterSystem.cs` | `SuppressEngagementDecay` — false 면 평소와 동일 (관객 개별 몰입도 자연 감소) |
| `AudienceRosterPresenter.cs` | `TryGetActor(id)` 읽기 전용 조회 |
| `CardSystem.cs` | `SetHand(cards)` — 손패를 지정 카드로 강제 교체, 덱은 건드리지 않음 |

※ 프리젠터의 구버전 API 참조 2건(`PlayDeparture`/`LayoutPosition`)도 이번에 수정 — 컴파일 복구.

## 6. 테스트

1. `Ctrl+R` → Setup → `Ctrl+S` → Play → 공연 시작 → 0.6초 뒤 튜토리얼 자동 시작
2. 재실행: `[Tutorial]` 인스펙터 ⋮ → `Debug/Start Tutorial` (완료 기록도 함께 초기화)
3. 완료 기록만 초기화: `Debug/Reset Tutorial Done Flag`

## 7. 기획안 대비 미구현 (시간 판단, 필요 시 위치 명시)

- 너구리 오프닝 연기 연출 → 텍스트 인트로로 대체 (`EnterIntro`)
- 관객별 동시 반응 팝업(2단계) → 단일 HUD 원칙 유지, 총반응 수치 설명으로 대체
- FinalRun 5초 무행동 단계별 힌트 → `Phase.FinalRun` Update 에 타이머 추가하면 됨
- 카드 하단 성향별 ▲▼ 기호 → `CardSlotUI` 확장 건 (별도 작업)
- 이탈 임박 보조 신호(문 아이콘 등) → 액터 아트 작업 필요
