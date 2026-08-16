# 증강 시스템 작업 빠른 시작 가이드

## 1. 현재 어디까지 되어 있나

증강 **선택 화면만** 구현되어 있다.

- 증강 후보 3개 표시
- 증강 이름, 설명, 아이콘, 티어 표시
- 각 후보마다 독립적인 리롤 횟수 표시
- 선택 버튼과 개별 리롤 버튼 입력
- 오류, 처리 중 입력 잠금, 팝업 열기·닫기
- 증강을 선택하면 기존 투어 흐름을 통해 Map으로 이동

현재 화면에 나오는 증강 데이터는 전부 임시 샘플이다. 실제 증강 효과,
등장 확률, 중복 방지, 티어 규칙은 아직 없으며 UI에도 넣지 않았다.

## 2. 그대로 사용하면 되는 파일

아래 세 파일은 화면 전용이므로 실제 증강 시스템에서도 그대로 사용하면 된다.

```text
Assets/Scripts/UI/Augment/AugmentSelectionViewModels.cs
Assets/Scripts/UI/Augment/AugmentChoiceView.cs
Assets/Scripts/UI/Augment/AugmentSelectionPopup.cs
```

핵심 사용법은 다음뿐이다.

```csharp
popup.Show(screenModel);            // 후보 3개 표시
popup.UpdateChoice(choiceModel);    // 특정 슬롯 하나만 갱신
popup.ShowError(message);           // 실패 표시 및 입력 잠금 해제
popup.Hide();                       // 선택 완료 후 닫기

popup.SelectRequested += OnSelect;  // 선택한 slotIndex 전달
popup.RerollRequested += OnReroll;  // 리롤할 slotIndex 전달
```

UI는 실제 증강 ID를 받지 않는다. UI와 증강 ID의 연결은 Coordinator가
`slotIndex`를 기준으로 따로 보관해야 한다.

## 3. 교체해야 하는 임시 코드

```text
Assets/Scripts/Tour/PrototypeAugmentSelectionAdapter.cs
```

이 파일은 전체 투어 흐름을 테스트하기 위한 샘플이다. 실제 시스템이 준비되면
이 클래스 대신 증강 Coordinator를 연결한다.

현재 연결 위치:

```text
Assets/Scripts/Tour/TourPrototypeUI.cs
```

```csharp
_prototypeAugmentAdapter = new PrototypeAugmentSelectionAdapter(_augmentPopup);
```

위 한 줄을 실제 Coordinator 생성 또는 주입 코드로 교체하면 된다. 화면 View는
수정하지 않아도 된다.

## 4. 실제 Coordinator가 담당할 일

권장 처리 순서:

```text
Reward 단계 진입
-> 현재 스테이지의 RewardTableId 확인
-> 실제 증강 후보 3개 생성
-> 슬롯 번호와 실제 증강 ID를 Coordinator 내부에 저장
-> 표시용 ScreenModel을 만들어 popup.Show()
```

개별 리롤 요청:

```text
RerollRequested(slotIndex)
-> 해당 슬롯의 남은 리롤 검증 및 1회 소비
-> 중복되지 않는 새 후보 생성
-> 슬롯과 실제 증강 ID 매핑 교체
-> 해당 슬롯 ViewModel만 popup.UpdateChoice()로 갱신
```

선택 요청:

```text
SelectRequested(slotIndex)
-> 실제 증강 ID 조회
-> 증강 효과 적용 및 저장 성공 확인
-> 마지막에 TourRunManager.SelectAugment(realAugmentId) 호출
-> Map 단계로 이동
```

효과 적용에 실패했다면 `TourRunManager.SelectAugment()`를 호출하지 말고
`popup.ShowError()`로 복구한다.

## 5. 개별 리롤 확장 방법

리롤 횟수의 실제 소유자는 UI가 아니라 증강 시스템이다.

```csharp
choiceViewModel.rerollsRemaining = 실제_해당_슬롯_잔여_횟수;
```

추후 증강 효과로 리롤 횟수가 늘어나더라도 이 값만 변경하면 된다. 전체 후보를
한꺼번에 리롤하지 말고 요청받은 슬롯 하나만 교체한다.

## 6. 반드시 주의할 점

1. `SelectRequested` 또는 `RerollRequested`가 발생하면 팝업은 중복 입력을 막기
   위해 잠긴다.
2. 요청 처리가 끝나면 반드시 아래 중 하나를 호출해야 한다.

```text
성공한 리롤 -> popup.UpdateChoice(...)
실패 -> popup.ShowError(...)
선택 성공 -> popup.Hide() 또는 다음 단계 전환
```

아무것도 호출하지 않으면 버튼이 계속 잠긴 상태로 남는다.

3. `TourRunManager.SelectAugment()`는 증강 ID 기록과 Map 전환을 함께 처리한다.
   실제 효과 적용이 성공한 뒤 마지막에 호출한다.
4. `ownedAugmentIds`는 현재 문자열 ID 목록만 저장한다. 스택 수치나 개별 파라미터가
   필요하면 증강 시스템에서 별도 런타임 데이터를 설계한다.
5. 최종 아트 프리팹을 만들더라도 `AugmentSelectionPopup`의 공개 API와 이벤트는
   유지하면 증강 로직을 다시 연결할 필요가 없다.

## 7. 책임 분리

### 화면 담당

- 팝업 레이아웃과 애니메이션
- 후보 카드 3개의 표시
- 버튼, 비활성화, 남은 리롤 수, 오류 문구
- 최종 아트 프리팹 교체

### 증강 시스템 담당

- 증강 정의와 실제 ID
- 효과 적용
- 티어와 등장 확률
- RewardTable별 후보 생성
- 중복 방지
- 슬롯별 리롤 횟수와 소비
- 보유 증강 저장 및 로드
- 카드, 덱, 공연 수치 변경

## 8. 최소 완료 테스트

- 후보가 항상 3개 표시되는가
- 각 슬롯의 증강 ID와 표시 정보가 일치하는가
- 1번 리롤 시 1번 슬롯만 바뀌는가
- 리롤 횟수가 0이면 해당 버튼만 비활성화되는가
- 이미 보유한 증강의 중복 등장 규칙이 의도대로 동작하는가
- 효과 적용 실패 시 Map으로 넘어가지 않는가
- 선택 성공 시 증강이 저장되고 다음 Map이 열리는가
- 화면을 다시 열었을 때 이전 이벤트 구독이 중복되지 않는가

