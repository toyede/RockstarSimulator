# 큐시트 UI 적용 및 보스 미션 통합

2026-10-08. 실제 프로젝트와 연결된 Unity 6000.3.20f1 Editor에서 작업.

## 적용 내용

- **대화 화살표:** 각 줄의 타이핑이 완료되면 기존 대화창 우하단 흰 모서리에 고정. 짧은/긴 대사에 같은 앵커를 사용한다. 기존 상자 이미지의 투명 여백 때문에 단순 RectTransform 끝에 놓으면 밖으로 보이는 문제도 보정했다. 기존 상자/화자/본문의 수동 배치는 유지.
- **튜토리얼:** 집게 달린 종이 초안 재사용. 어두운 상단 띠에는 `공연 가이드 · CUE SHEET`, 설명은 밝은 종이에 어두운 잉크로 표시. 제목/본문/계속 안내를 분리. 진행/클릭/호버 조건은 변경하지 않았다.
- **미션:** imagegen으로 투명 배경의 빈 도트 슬레이트 신규 제작. 글자는 이미지에 굽지 않는다. 튜토리얼 배경을 복사하던 보스 UI 의존성을 제거했다.
- **보스:** 패턴과 `stadium_contested_fan`의 시작/진행/결과를 하나의 좌측 중단 슬레이트에 표시. 일반 이벤트 UI는 보스의 해당 이벤트만 무시하며 다른 무대 이벤트는 계속 표시.

## 표시 우선순위

진행 중인 보스 패턴 → 진행 중인 스탠딩석 요청 → 보스 패턴 결과 → 스탠딩석 결과.

다른 진행 목표나 직전 결과가 있으면 슬레이트 하단에 별도로 표시한다. 두 목표가 실제로 동시에 활성화되는 경우 어느 하나도 숨기지 않는다. 기본 512×288; 결과를 함께 표시하면 높이 352, 두 진행 목표를 함께 표시하면 높이 368로 여유를 확보한다. 위치는 1920×1080 기준 좌측 중단, 왼쪽 여백 32.

이벤트 ID를 확인하므로 다른 미션의 뒤늦은 진행/결과가 현재 미션을 덮지 않는다. 결과 표시 시간은 채널별로 관리하여 오래된 숨김 코루틴이 새 미션을 끄지 않는다. 일시정지 시 보스 캔버스를 숨기고, 재개 시 복원. Ready/GameOver/스테이지 변경/컴포넌트 비활성화 시 상태와 연출을 정리한다.

**변경하지 않은 것:** 미션 성공 조건, 시간, 점수, 팬 이동 판정, 상단 팬 게이지, Rank·점수·시계·카드 배치, 디버그 기능. 일반 무대 이벤트는 기존 상단 중앙 위치를 유지하며 슬레이트 높이만 읽기 좋게 확보했다.

## 변경 파일과 책임

| 파일 | 책임 |
|---|---|
| `Assets/Scripts/Dialogue/DialoguePanel.cs` | 줄 완료 시 화살표 고정 |
| `Assets/Resources/Dialogue/DialoguePanel.prefab` / `Assets/Settings/Dialogue/DialogueStyle.asset` | 기존 아트 여백에 맞춘 화살표 오프셋 (-80, 42) |
| `Assets/Scripts/Tutorial/TutorialOverlayUI.cs` | 튜토리얼 종이 스킨, 텍스트 레이아웃 |
| `Assets/Scripts/Stage/Events/MissionCueSheetView.cs` | 슬레이트 배경과 텍스트 영역만 구성; 판정 없음 |
| `Assets/Scripts/Stage/Boss/BossBattleUI.cs` | 보스 미션 표시 채널 통합, 우선순위, 수명주기 |
| `Assets/Scripts/Stage/Events/StageEventNoticeUI.cs` | 일반 이벤트 표시, 보스 스탠딩석 중복 억제 |
| `Assets/Scenes/Main.unity` | 튜토리얼 종이 참조, 보스 미션 위치/크기 저장 |
| `Assets/Resources/UI/CueSheets/*.png` | TutorialPaper / MissionSlate, 독립 스킨 |

씬/프리팹/임포터는 Unity Pipeline/Editor API로 수정하고 저장했다. YAML/.meta를 수동 편집하지 않았다. 기존 미완료 작업과 다른 변경은 보존했다.

## 아트

- TutorialPaper: `Docs/ArtDrafts/CueSheet_20261008_v1/cuesheet_blank.png` 원본을 복사. 원본 보존.
- MissionSlate: imagegen 신규 생성, `Assets/Resources/UI/CueSheets/MissionSlate.png`.
- 모두 Point, mipmap 없음, RGBA 외곽 투명 픽셀 확인. Unity 화면에서 외곽/가독성 확인.

생성 프롬프트 요약: 동물 록밴드 카드 게임용 정면의 가로형 픽셀 슬레이트. 닫힌 흑백 사선 클래퍼와 좌측 힌지, 빈 짙은 자주색 제목 띠, 넓고 차분한 밝은 회색 종이. 투명 외곽, 얇은 도트 테두리. 브랜드/로고/문자/숫자/표/배경/블룸 금지. 모든 실제 텍스트는 Unity에서 표시.

## 검증

- Unity 컴파일 성공.
- 마지막 35개 검증 재실행 후 Console 현재 오류/경고 모두 0. 마지막 오류 확인 커서 1779, 테스트 구간(since 1734)에 새 오류 없음.
- Play Mode `RuntimeChecks.cs`: 26개 통과. 라우팅, 중복 억제, 두 목표 보존, 오래된 이벤트 무시, 일시정지 복원, 새 미션 보호, 일반 이벤트 유지, 튜토리얼 특수 배치 복원, 긴/짧은 대사 화살표 위치.
- Play Mode `ExtendedChecks.cs`: 9개 통과. 실제 길이의 동시 미션 5개 텍스트 영역 넘침 없음, GameOver/Disable 정리, 재활성화 구독 정상.
- 실제 Game View 1920×1080 캡처로 확인 후 하단 문구 넘침 수정 및 재촬영.
- Pipeline eval/import 호출 3회에서 5초 도구 시간 초과 로그가 있었다. 실제 적용 여부를 재조회하여 확인했고 마지막 테스트 재실행은 성공했다. 게임 코드 컴파일/런타임 오류와 구분한다.

캡처:

- `Assets/Docs/Verification/CueSheets/boss-unified.png`
- `Assets/Docs/Verification/CueSheets/tutorial-paper.png`
- `Assets/Docs/Verification/CueSheets/dialogue-arrow.png`

검증 스크립트는 `Docs/Verification/CueSheets/`에 두어 플레이어 빌드 코드에 포함되지 않는다. 테스트용 이벤트를 발행한 통제 장면으로 검증했으며 전체 투어 반복 플레이, WebGL 재빌드, 모바일 기기 검증은 이번 범위에서 수행하지 않았다.

### 화살표 후속 수정 — 실제 TourHub 씬

이전 화살표 검증은 프리팹을 별도로 생성한 장면이어서, 실제 `TourHub` 씬에 저장된 `arrowFollowsText: 1`을 놓쳤다. 해당 필드와 마지막 글자 좌표 계산을 제거했다. 이제 모든 줄 완료 시 `DialogueBox` 우하단 앵커 및 스타일 오프셋 `(-80, 42)`를 적용한다. 새 `DialogueStyle`의 기본값도 같은 위치다. 기존 씬의 수동 배치와 대사 내용은 변경하지 않았다.

타이틀의 `StartTour` → 실제 투어 노드 선택 → 씬에 배치된 대화창으로 검증했다. `intro_stage_01`의 15개 대사와 짧은/빈/세 줄 본문에 대해 89개 위치·표시 검사 통과. 새 Console 오류 없음. 실제 화면 캡처: `Assets/Docs/Verification/CueSheets/dialogue-arrow-tourhub.png`. WebGL 재빌드와 모바일 검증은 별도다.
