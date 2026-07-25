# 관객 피드백 텍스트 (CONTEXT STAGE)

플레이어에게 "지금 무슨 일이 일어났는지"를 글자로 알려주는 연출 묶음.
세 곳에서 쓰이며, 어느 것도 카드·점수·관객 시스템을 수정하지 않고 EventBus 만 구독한다.

| 연출 | 컴포넌트 | 폴더 |
|---|---|---|
| 관객 입퇴장 (+1 관객 / -3 관객) | `AudienceFlowTextUI` | `Scripts/Audience` |
| 특별 관객 말풍선 (Mosh! 등) | `SpecialAudienceSpeechBubble` | `Scripts/SpecialAudience` |
| 저격 성공 보상 배너 | `SpecialHitRewardBanner` | `Scripts/SpecialAudience` |

## 셋업

```
Tools/Feedback/Setup All Crowd Feedback   →   Ctrl+S
```

개별 실행도 가능하다 (`Setup Audience Flow Text` / `Setup Special Audience Speech Bubble` /
`Setup Special Hit Banner`). **이미 있는 것은 덮어쓰지 않으므로** 여러 번 실행해도 안전하다.

## 1. `FloatingWorldText` — 공용 표현

떠오르며 사라지는 월드 텍스트 한 장. `AudienceReactionPopup` 과 같은 연출
(상승 + 스케일 펀치 + 페이드)이지만 관객 개체에 묶여 있지 않다.

**프리팹 에셋을 쓰지 않는다.** 스프라이트도 자식 구조도 없는 텍스트라 프리팹으로 관리할
이유가 없고, 프리팹이 없으면 셋업 메뉴가 연결을 빠뜨릴 일도 씬 저장을 놓칠 일도 없다.
`FloatingWorldTextPool` 이 런타임에 직접 만들어 돌려쓴다 — 전부 재생 중이면 **가장 먼저
시작한 것**을 뺏어 쓰므로 관객이 한꺼번에 빠져나가도 최신 텍스트가 밀려나지 않는다.

움직임 값은 `FloatingWorldTextStyle` 하나에 묶여 있어 표시하는 쪽이 인스펙터에서 각자 조절한다.

시간은 전부 `Time.unscaledDeltaTime` 을 쓴다 — 히트스톱이나 게임오버 팝업으로
`timeScale` 이 떨어져도 연출은 정상 속도로 끝난다.

## 2. 관객 입퇴장 텍스트 (`AudienceFlowTextUI`)

`AudienceJoined` / `AudienceDeparted` 를 구독해 관객 무리 위에 띄운다.

**집계가 핵심이다.** 두 이벤트는 관객 한 명당 한 번씩 발행되므로 그대로 띄우면
`-1` 이 세 번 뜬다. `aggregateWindow`(기본 0.15초) 동안 모아 한 장으로 합친다.

**제외 목록** — 공연 시작·리셋으로 명단이 통째로 갈리는 것은 플레이어의 행동이 아니다.
기본값으로 `Initialization` / `Reset` 을 걸러 놓았다. 인스펙터 리스트라 코드 수정 없이 조정한다.

| 사유 | 기본 동작 |
|---|---|
| `Initialization` / `Reset` | 표시 안 함 (공연 시작마다 "+8 관객"이 뜬다) |
| `NaturalArrival` / `SpecialCardTarget` | 표시 |
| `EngagementDepleted` | 표시 |
| `NearbyConcert` | 표시 (주황 + "-{0} 옆 공연으로") |

이탈 사유별 색·문구는 `departureStyles` 표로 늘린다. 집계 버킷도 스타일별로 나뉘어 있어
한 창 안에서 사유가 섞여도 색이 뒤바뀌지 않는다.

위치는 `AudienceRosterPresenter.MemberRoot` 를 매 프레임 따라가고 `followOffset` 만큼 띄운다.
`followTarget` 을 직접 지정하면 그쪽을 따라간다.

## 3. 특별 관객 말풍선 (`SpecialAudienceSpeechBubble`)

특별 관객이 관객 사이를 돌아다니면서 자기가 원하는 것을 계속 외쳐 저격을 유도한다.
`popInterval`(기본 1.5초)마다 한 번씩 톡 떠오른다 — 상시 표시는 움직이는 캐릭터라 시선을 뺏는다.

문구는 요구 타입별 `lines` 표에서 가져온다 (`Chill;;` / `Singalong♪` / `Mosh!`).
요구 타입이 늘어나도 코드를 고칠 필요가 없다.

### 반드시 지킬 것 두 가지

1. **`SpecialAudience` 프리팹 <b>루트</b>의 자식으로 둔다.**
   `VisualRoot` 아래에 두면 액터가 진행 방향에 따라 X 스케일을 뒤집을 때(`Facing`)
   글자가 좌우로 반전된다.
2. **크기·정렬 순서는 액터를 따라간다.** 특별 관객은 줄을 옮겨 다니고 그때마다
   `CurrentVisualScale` 과 `sortingOrder` 가 바뀐다. 말풍선이 따라가지 않으면
   뒷줄에서 혼자 커 보이거나 다른 관객에게 가려진다.

### ♪ 글리프

`DungGeunMo SDF` 아틀라스에 `♪`(U+266A)가 **없다.** Atlas Population Mode 가 Dynamic 이라
에디터에서는 런타임에 추가될 수 있지만 빌드에서 두부(□)로 나올 수 있다.
셋업 메뉴가 실행 시 이걸 검사해 경고를 남긴다. 두부가 보이면:

1. Font Asset Creator 의 Custom Character List 에 `♪` 를 추가해 정적으로 굽거나
2. `lines` 표에서 문구를 `Singalong~` 등으로 바꾼다 (인스펙터 문자열이라 코드 수정 없음)

## 4. 저격 성공 보상 배너 (`SpecialHitRewardBanner`)

저격이 성공했을 때 얻은 것을 화면 상단 가운데에서 크게 알린다.

```
★ SPECIAL HIT ★
+320  ·  관객 +3  ·  COMBO x1.5
```

**카드 시스템을 수정하지 않는다.** `CardSystem` 안에서 저격 효과의 실행 결과
(`SpecialCardTargetEffectResult`)는 `out _` 로 버려지지만, 실제로 일어난 일은 전부
이벤트로 흘러나온다:

| 이벤트 | 뽑는 값 |
|---|---|
| `AudienceJoined` (SpecialCardTarget) | 새로 들어온 관객 수 |
| `AudienceStateChanged` (SpecialCardTarget) | 몰입도가 오른/내려간 관객 수 |
| `SpecialHitLanded` | 연출 유지 시간 |
| `CardResolved` (IsSpecialHit) | 최종 점수·콤보 배율 |

카드 설정값이 아니라 **실제 적용 결과**를 읽으므로 카드 밸런스를 바꿔도 문구가 자동으로 맞는다.

### 두 가지 함정

1. **발행 순서** — 관객 효과와 `SpecialHitLanded` 는 `CardResolved` <b>보다 먼저</b> 나온다.
   그래서 관객 쪽 값을 먼저 모아 두었다가 `CardResolved` 가 올 때 합친다
   (`collectWindow`, 기본 0.25초). 같은 프레임이라 짧아도 된다.
2. **오른 것과 내려간 것** — 같은 `SpecialCardTarget` 사유라도 모쉬핏 카드는 모쉬 관객을
   부르면서 나머지 관객의 몰입도를 **눌러 놓는다**. 증감 방향으로 나눠서 세지 않으면
   "관객 5명 열광"이 사실은 진정된 관객이 된다. 진정된 쪽은 보상 배너라 기본적으로
   표시하지 않는다 (`calmedFormat` 을 채우면 표시).

### 확인

`SpecialHitBanner` 인스펙터의 `⋮` → `Debug/Play Sample Banner` 로 카드 없이 연출만 본다.
