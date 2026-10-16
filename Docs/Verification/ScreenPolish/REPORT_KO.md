# 화면·공연 연출 개선 — 현재 구현/검증

2026-10-16 최종 Editor 검증. 승인된 P0~P6 구현 완료. 자연 플레이·WebGL·모바일 검증과 구분한다.

## 적용한 것

- 튜토리얼 제목/본문/계속 안내를 검은색으로 분리. 성향·행동의 진한 강조색 유지. 안내를 종이의 실제 흰 영역 안으로 이동.
- Stage3/4 이벤트를 보스와 같은 좌측 중단 큐시트로 통일. 작은 공통 배치 헬퍼 사용.
- 보스 바에 지정된 back/me/boss 3개 아트 적용. 606×30 정수 배율, 우리 왼쪽·라이벌 오른쪽, 팬 수 표시. 점수 감소와 팬 비율을 분리.
- 카메라 버튼 우하단 배치. 기존 클릭/Tab·일시정지/미션 잠금 동작 유지.
- 일반 관객 가로 간격 1.35→2.6, 행 간격 0.55→1.0, 반 칸 엇갈림·ID 기반 작은 고정 오프셋. 4:3에서는 카메라 폭에 맞게 자동으로 안쪽 배치.
- 사건 시작/결과의 세계 좌표 테이프·음표·파편 버스트, 큐시트 바깥 종이 조각. 기존 소나기에 초기 방출·물보라 추가. 정전 중 입자는 끄고 복구 시 스파크.
- 실제 점수 HUD 옆에 실제 차감량 표시. 0점에서는 가짜 손실 없음. 카드 표시 대기 중 최신 점수가 과거 값으로 돌아가는 코루틴 경로 방어.

Stage1 조명 위치·스테이지 BGM·점수 공식·보스 난이도·덱/증강·시계/콤보·등급 게이지는 변경하지 않았다. 기존 사용자 변경을 보존했다.

## 이번에 변경한 코드 / 연결

- `TutorialOverlayUI`, `AudienceRosterPresenter`, `ScoreUI`
- `StageEventNoticeUI`, `MissionCueSheetView`, `RainShowerVFX`
- `BossBattleUI`, `BossStagePresentation`
- 새 `MissionCueSheetLayout`, `StageEventPresentationVFX`
- Main: 아트 참조, 관객 간격 prefab override, 이벤트 표시 컴포넌트 연결. 이전 코드의 드랍 직렬화 필드가 함께 저장됐으며 수동 YAML 편집은 하지 않았다.
- 후속 P4: `StageEventPresentationVFX`에 실제 방어/합류 결과의 내향 파동과 정전 복구 연기를 추가했다. `BlackoutPresentation.IsRestoring`으로 복구 점멸 중의 조기 방출을 막는다. Main에는 기존 Point `steam_wispy_0` 스프라이트 참조만 추가 연결했다.

## 검증

| 검사 | 통과 | 범위 |
| --- | --- | --- |
| ScreenPolish/Verify.cs | 54/54 | 검은 글자·강조색, 넓은 배치·고정 jitter, 미션 배치·본문, 파티클 중복/정리/Pause, 바 경계/초기화, 실제 차감·표시 지연 |
| BossCameraPause/VerifyMain.cs 재실행 | 16/16 | 실제 ESC, 옵션의 블러 앞 표시, 카메라 포인터 raycast/클릭·왕복·Pause 잠금 |
| DropLighting/VerifyBoss.cs 재실행 | 28/28 | 실제 미션의 드랍 큐·레이저·팬 점프·복귀·상태/인원 불변 |
| DropLighting/VerifyDistortion.cs 재실행 | 6/6 | GPU 색 유지 및 이전 정렬 대조군 |
| ScreenPolish/VerifyEvents.cs | 23/23 | 실제 Stage3/4 Rule 시작·성공·실패, 긴 큐 순서, 실패 관객 이탈, 완전 복구 후 연기, 실제 방어 결과 파동·상한·Pause·정리 |
| ScreenPolish/VerifyTourLoop.cs | 81/81 | 실제 Title 런처→5개 공연/신문→4회 개별 리롤·선택·맵 이동→보스 결과→엔딩. 씬 로딩 완료를 기다린 뒤 판정, 로컬 최고점 원복 확인 |
| ScreenPolish/Check720.cs | 7/7 | 실제 1280×720, 보스/스탠딩 통합 큐시트의 5개 텍스트 영역, 중복 미션 없음 |

최종 **215/215** 통과. Unity 컴파일 completed/failed=false. 검사 중 발견한 큐시트 CanvasGroup fake-null과 복구 첫 점멸의 조기 연기 방출을 수정한 뒤 재검증했다.

테스트 도구 자체의 오류도 구분한다. 첫 투어 검사에서는 씬 교체 전 GameManager 참조를 재사용해 실패했으며, 씬마다 현재 인스턴스를 얻도록 검사 스크립트를 고친 뒤 81개가 통과했다. Game View 임시 프리셋 정리의 잘못된 인덱스 호출은 Console seq660의 `Invalid index 3 7 4`로 남아 있다. 해당 도구를 고치고 원래 10개 프리셋/Full HD 복원을 재조회했다. Console을 임의로 지우지 않았으며 버퍼 오류 0이라고 주장하지 않는다. 이후 새 게임 코드 예외는 확인되지 않았다.

화면 검증: 실제 Game View Full HD, 1366×768, 1024×768, **1280×720**. 최대 12명, Fever+방송 큐, 방어 파동, 정전 복구, 보스/스탠딩 병합 표시를 확인했다. VFX 피크 캡처는 Play Mode에서 표시/판정 입력을 강제하고 임시 timeScale=0으로 정지한 샘플이다. 투어 검사도 대화 스킵·목표점수 강제 종료로 시간을 단축했다. **자연 진행 완주·밸런스 검증이 아니다.** 외부 순위 제출 버튼은 누르지 않았다.

대표 화면:

- [튜토리얼 검은 글씨](../../../Assets/Docs/Verification/ScreenPolish/tutorial-final.png)
- [Stage3 미션](../../../Assets/Docs/Verification/ScreenPolish/stage3-event.png)
- [Stage4 최대 관객](../../../Assets/Docs/Verification/ScreenPolish/stage4-max-1366.png)
- [과장된 입장 버스트](../../../Assets/Docs/Verification/ScreenPolish/event-burst-1366.png)
- [보스 6:14 / 4:3 화면](../../../Assets/Docs/Verification/ScreenPolish/boss-fixed-1024.png)
- [Fever+12명+큐시트 / 720p](../../../Assets/Docs/Verification/ScreenPolish/fever-event-720.png)
- [실제 방어 결과의 내향 파동](../../../Assets/Docs/Verification/ScreenPolish/defense-wave-720.png)
- [복구 완료 후 스파크·얇은 연기](../../../Assets/Docs/Verification/ScreenPolish/restored-smoke-720.png)
- [보스/스탠딩 통합 큐시트 / 720p](../../../Assets/Docs/Verification/ScreenPolish/boss-missions-720.png)
- [5개 공연 후 엔딩 요약](../../../Assets/Docs/Verification/ScreenPolish/tour-ending.png)

## 제한된 부하 측정

`MeasureEffects.cs`: Editor 1920×1080, Stage4 관객 12명, 워밍업 후 비활성/활성 각 180프레임. 활성 구간에는 30프레임마다 내향 파동 호출 6회. 별도 빌드나 장시간 스트레스 검사는 하지 않았다.

| 값 | 표시 컴포넌트 비활성 | 파동 활성 |
| --- | ---: | ---: |
| 프레임 중앙값 | 4.091ms | 4.065ms |
| 프레임 P95 | 4.893ms | 5.120ms |
| 전체 프레임 GC 중앙값 | 14,407B | 14,407B |
| 전체 프레임 GC 최댓값 | 15,401B | 15,995B |

워밍업된 파동 메서드 6회 직접 호출의 합계는 0.2117ms / 관리 할당 0B였다. 샘플이 짧고 전체 프레임 값에는 Editor/다른 시스템이 포함되므로 속도 개선·FPS 보장·전체 무할당의 증거가 아니다. 이 메서드 측정에는 반사 호출 대신 캐시한 delegate를 사용했다.

## 정리 / 남은 직접 확인

- 추가 표시 입자 상한: 월드 조각 76 + 음표32 + 연기8 + UI조각12 = 128. 기존 비/드랍까지 포함한 게임 전체 입자 상한이 아니다.
- Play Mode 종료, Title 복귀, 원래 Full HD/프리셋 복원. 검사 생성 TMP 캐시만 정리했으며 `git diff --numstat -- Assets/Font` 출력 없음. 임시 입력 장치/씬 변경은 저장하지 않았다.
- 제외한 Stage1 조명 위치·스테이지 BGM 및 점수/밸런스/증강 규칙은 변경하지 않았다. 커밋·빌드·배포·순위 업로드 없음.
- 남은 사용자 확인: 자연 플레이 중 가림/연출 강도, 모든 랜덤 이벤트 조합, 장시간 성능, WebGL/모바일 직접 입력. 이번 Editor 검사로 이를 완료했다고 보지 않는다.
- 승인 범위의 Editor 구현/검증이 끝났으므로 자동 재개는 일시정지한다. 새 요청 없이 반복 수정하지 않는다.
