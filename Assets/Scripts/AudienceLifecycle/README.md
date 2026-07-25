# AudienceLifecycle

관객 "일반 신규 유입"과 "이탈 처리"를 담당하는 시스템. 개인별 몰입도(0~100) 계산과
성향 대응 카드 반응값은 이 폴더가 아니라 `Assets/Scripts/CrowdComposition/` 쪽 개편 작업의
책임이다 (기획 문서 `.myDox/기획안 변경 아이디어 정리.md` 참고). 이 폴더는 그 작업과 무관하게
"유입"과 "이탈"만 선행 구현했고, 개인 몰입도 시스템이 완성되면 아래 연동 지점 한 줄만 호출하면
이어붙일 수 있게 만들어 두었다.

## 연동 지점 (몰입도 담당이 알아야 할 전부)

몰입도가 0이 된 관객을 퇴장시키려면:

```csharp
using ContextStage;

if (AudienceChurnEvaluator.ShouldExit(currentImmersion))
    AudienceLifecycle.RequestExit(memberId, AudienceExitReason.NaturalDecay);
```

- `memberId` 는 그 관객이 유입될 때 발행된 `AudienceMemberSpawned` 이벤트의 `MemberId` 를 그대로 보관해 쓰면 된다.
- `RequestExit` 가 퇴장 연출(디스폰), `AudienceMemberExited`/`AudienceCountChanged` 이벤트 발행, 관객 0명일 때 `GameManager.Instance.GameOver()` 호출까지 전부 처리한다.

## 이벤트 (`GameJamKit/Core/GameEvents.cs` 하단)

| 이벤트 | 발행 시점 |
|---|---|
| `AudienceMemberSpawned` | 초기 관객 포함, 새 관객이 유입될 때. `MemberId`/`Preference`/`InitialImmersion` 포함 |
| `AudienceMemberExited` | `AudienceLifecycle.RequestExit` 호출로 실제 퇴장 처리가 끝났을 때 |
| `AudienceCountChanged` | 관객 수가 바뀔 때마다 (디버그 UI/연출용) |

개인 몰입도 시스템은 `AudienceMemberSpawned` 를 구독해 자기 쪽 몰입도 데이터를 만들 때 `MemberId` 를 키로 쓰면 된다.

## 시각 스폰: 기존 CrowdSpawner 재사용

새 프리팹/좌표 시스템을 따로 만들지 않고, `Assets/Scripts/Crowd/CrowdSpawner.cs` 에 **추가 전용 API** 를 붙여 재사용한다.

- `CrowdSpawner.SpawnAdditional(CrowdPreference)` — 관객 1명을 프리셋 그리드(`_members`, `CrowdCompositionManager` 기반 21명 고정) 와 별개인 `_additionalMembers` 목록에 스폰. 프리셋 그리드보다 앞쪽(음의 y)에 별도 줄로 배치되어 겹치지 않는다.
- `CrowdSpawner.DespawnAdditional(CrowdMemberView)` — 기존 `AnimateExit` 퇴장 연출을 재사용해 제거.
- **`CrowdCompositionManager`, 프리셋/전환(`BuildReplacements`, `TransitionComposition`) 로직은 전혀 건드리지 않았다** — 팀원이 개인 몰입도로 개편 중인 영역. `CrowdSpawner.cs` 파일 자체는 수정했지만 새 메서드를 추가만 했을 뿐 기존 코드 경로는 그대로다.
- **주의**: `CrowdSpawner.cs` 는 팀원이 동시에 편집할 수 있는 파일이다. 이 시스템을 머지하기 전에 팀원과 한 번 확인하는 것을 권장한다.

## 지금은 임시인 부분 (통합 시 교체 예정)

- **인원수 카운터**: `AudienceInflowSystem` 이 자체 `Dictionary<int, CrowdMemberView>` 로 임시 관리한다. 몰입도 시스템이 완성되면 그쪽 로스터가 진짜 소스가 되고 이 임시 카운터는 제거될 예정.
- **시각적 스폰 좌표**: `SpawnAdditional` 이 만드는 "추가 줄" 배치는 프로토타입 확인용 임시 좌표이며, 프리셋 시스템 자체가 가변 인원(0~10) 모델로 개편되면 `CrowdSpawner` 의 배치 로직 전체가 다시 설계될 가능성이 크다.

## 설정 (`AudienceLifecycleConfig`)

`Create/ContextStage/Audience Lifecycle Config` 로 에셋 생성. 초기/최대 인원, 유입 몰입도 범위(기획 3.1: 30~50), 유입 판정 주기·확률을 인스펙터에서 조정한다. 전부 기획서 10장 "핵심 밸런스 조정 항목"에 따라 확정값이 아니므로 플레이 테스트로 튜닝한다.

## 씬 배치

빈 오브젝트에 `AudienceInflowSystem` 을 붙이고:
- `Config` 에 `AudienceLifecycleConfig` 에셋 연결
- `Crowd Spawner` 에 씬에 이미 있는 `CrowdSpawner` 오브젝트 연결 (프리팹 참조는 필요 없음 — `CrowdSpawner` 가 이미 갖고 있음)

## 디버그

`AudienceInflowSystem` 인스펙터의 `Enable Debug Input` 체크 시 (에디터/개발 빌드 한정):
- `F5` — 강제 유입 1회 시도
- `F6` — 임의 관객 1명 강제 이탈

몰입도 시스템 없이도 유입 → 최대 인원 상한 → 강제 이탈 → 0명 게임오버 흐름을 단독 테스트할 수 있다.
