# 보스 카메라 · ESC 옵션 · TourHub 스킵 · Stage 4 배경 수정

## 적용

- 보스의 텍스트 전환 버튼을 `Assets/Sprites/0930_art/boss_camera.png` 이미지 버튼으로 교체. 원본 비율 유지, 하단에 목적지 표시, 클릭/Tab 전환 유지. 메뉴가 열렸거나 카메라가 이동 중이면 입력 차단.
- 옵션을 열면 일시정지의 `Buttons` 그룹만 숨긴다. 블러와 일시정지 소유권은 유지하고, 옵션 닫힘 완료/즉시 닫힘 때 버튼을 복구한다. 새 Pause 열기 시 남은 옵션은 즉시 닫는다. 기존 옵션 캔버스의 상단 정렬도 유지한다.
- F10 대화 스킵은 동기 상태 전이 구간에서만 Main 자동 로딩을 억제하고 목표 점수의 D 결과를 제출한다. `finally`에서 억제를 해제하며, 일반 대화 완료는 그대로 Main 진입. 로딩 중 F10은 무시한다. 다른 디버그 키는 제거하지 않았다.
- Stage 4의 원형 무대 `ST04_BG_Base_v2.png` 연결을 이전 `ST04_BG_Base.png`로 교체. StageSet 배치/조명/룰/다른 스테이지 배경은 유지한다.

## 검증 — Unity 6000.3.20f1 연결 Editor

| 검사 | 결과 |
| --- | --- |
| 컴파일 | 완료, 컴파일 오류 없음 |
| Main Play Mode: Stage 4 연결, 실제 ESC 입력, 옵션 열기/닫기, 블러 정렬, 카메라 클릭/왕복/일시정지 차단 | 16/16 통과 |
| TourHub Play Mode: 실제 F10 입력, 중복 결과 방지, 두 스테이지 스킵, 결과→증강, 정상 대화→Main 및 선택 스테이지 적용 | 12/12 통과 |
| 화면 캡처 | Stage 4 이전 배경, 옵션/일시정지 분리, 보스 카메라 버튼 확인 |
| Console | 최종 TourHub 검증 시작 기준 seq 2165 이후 새 Error 없음 |
| 변경 파일 whitespace 검사 | 통과 |

초기 검증 스크립트는 Pause의 메뉴 이름을 `Content`로 가정해 실패했다. 실제 `Buttons` 계층에 맞춰 수정 후 재검증했다. 카메라 복귀는 실제 scaled 시간, 정상 씬 진입은 비동기 로딩 완료까지 기다려 최종 상태를 검사했다. CLI cold eval 타임아웃은 게임 오류와 구분했고 Error Pause가 걸리면 검증을 재개했다.

검증은 Editor Play Mode 기준이며 WebGL/모바일 빌드는 수행하지 않았다. 랭킹 등록, 저장 초기화, 튜토리얼 완료 저장은 실행하지 않았다. 검증 후 Play Mode 종료 및 기존 Title 씬 복귀.

## 캡처

- `Assets/Docs/Verification/BossCameraPause/stage4-restored.png`
- `Assets/Docs/Verification/BossCameraPause/pause-after.png`
- `Assets/Docs/Verification/BossCameraPause/options-after.png`
- `Assets/Docs/Verification/BossCameraPause/boss-camera.png`

검증 재실행: `VerifyMain.cs`는 Main Play Mode, `VerifyTourHub.cs`는 TourHub Play Mode에서 `eval_file --file`로 실행. 결과는 각각 `ReadMain.cs`, `ReadTourHub.cs`로 조회한다. 테스트는 종료 시 Editor를 일시정지한다.
