# B 작업 구현·검증 보고

2026-10-08 · 실제 프로젝트 `E:\UnityProjects\RockstarSimulator` · Unity 6000.3.20f1

## 결론

조명 V3 검증을 끝낸 뒤 B의 프로토타입을 구현했다. 기존 관객 힌트·기본 튜토리얼·DEBUG ON과 수동 배치는 유지했다. 실제 사람의 재미 평가와 모바일 WebGL 확인은 완료로 처리하지 않았다.

| 항목 | 반영 내용 | 상태 |
|---|---|---|
| B0 | 대사표·연출표·투명 큐시트 초안 | 문서/시안 완료, 최종 아트 적용 대기 |
| B1 | 관객/조명 선택 근거 비교 기록표 | 준비 완료, 사람 테스트 대기. 힌트 축소 안 함 |
| B2 | 압축 교육 비교 옵션, 총 반응/최종 점수 분리 | 구현, 기본 OFF. CHILL 연습의 카드 허용 상태 누수 수정 |
| B3 | Stage2/3/보스의 추상적인 대사 구체화, 펑 플래시·배경 충격·짧은 SFX | 구현. 표정은 기존 에셋/폴백 사용 |
| B4 | 일반 공연 4곳 × 6종 헤드라인, 실제 기록 기사, 도장 SFX | 구현. 기사 본문 상단 정렬로 하단 선 겹침 완화 |
| B5 | 입퇴장 완급·시차, 모쉬핏 벌어짐/복귀, 마이크 공동 반응, Chill 작은 호응 | 구현, 기존 인원/점수 이벤트만 사용 |
| B6 | 맵 가속·감속/먼지, 15초·5초 시계 경고, 신규 카드 반짝임 | 구현. 기존 카드 오디오 연결 점검·보존 |
| 선택 연출 | 픽셀 커서/터치 피드백 | 구현, 기본 OFF. 실제 기기 테스트 필요 |

## 실제 검증

| 검사 | 결과 | 의미/한계 |
|---|---|---|
| Unity 컴파일 | 오류 없음 | Unity Editor/Pipeline 기준 |
| `VerifyRuntime.cs` | 25/25 | 기본/압축 교육, 2초 Hover, 10초 1,000점, 자동 Fever 차단, 잘못된 카드 소비·점수 불변, 관객 동작/풀 복구, 시계 연장 연출 보존 |
| `VerifyContentAndCue.cs` | 17/17 | 등급/실패 헤드라인, 없던 팬 합류를 기사에 쓰지 않음, 펑 1회·새 줄/스킵/숨김 정리 |
| `VerifyPresentationExtras.cs` | 15/15 | 움직인 관객 Hover 좌표, 같은 종류 보충 카드 판별, 64픽셀 상한·알파·입력 비차단·비활성 정리, 6개 카드 오디오 연결 |
| `VerifySpecialGeometry.cs` | 4/4 | 이동하는 특별 관객 콜라이더의 화면 좌표/드롭 원 판정, 바깥 좌표 거절. 실제 마우스 드래그/모바일을 대신하지 않음 |
| 보충 이펙트 지연 정리 | 2/2 | 1장 손패도 소비 기록을 남김, 같은 프레임 손패 2회 갱신/공연 종료 후 지연 픽셀 0 |
| `VerifyResultTextFit.cs` | 30/30 | 5개 스킨, 긴 헤드라인/7자리 점수/보스 기사 렌더 bounds. 모든 문구 완전 표시를 보장하는 것은 아님(기존 Ellipsis 유지) |
| 조명 회귀 검사 | 47/47 + 6/6 | B 반영 후 다시 실행. 암전/Fever/워시/레이저/역광/재시작 유지 |

### 실제 투어 루프

`Title → TourHub 맵 → 대화 → Main Stage1 → 신문 → 증강 개별 리롤/선택 → 맵 이동 → Stage2 → Stage3 → Stage4 → 보스 → 신문 → TourHub Completed/엔딩`

- 기존 버튼의 리스너와 전환 코루틴으로 실행했다. 일반 공연별 slot0 리롤 후 slot1 잔여 횟수 1 유지, slot0 0 확인. 총 결과 5개·보유 증강 4개로 종료했다.
- **점수를 목표만큼 강제로 더해 종료한 전환 검사**다. 실제 5곡 완주·난이도·밸런스가 검증됐다는 뜻이 아니다.
- 보스 팬 영입 2명·이탈 2명을 실제 룰 메서드로 실행했다. 논리/화면 인원 일치, 이동 종료 후 고아 팬 없음, 총인원 보존을 확인했다. 자연 호응 이탈도 라이벌에 합산됐다.
- 엔딩 선택 로그: `Mosh (ending_mosh)`, 총점 42,500, Completed/TourHub 확인. 점수 기록 버튼을 누르거나 원격 랭킹에 테스트 점수를 보내지 않았다.
- 튜토리얼 테스트는 저장 완료 키를 쓰지 않았다. Play Mode 수정은 저장하지 않았다.

### 화면 확인

- `Assets/Docs/Verification/WorkB/result_fixed.png`: 1280×720, 짧은 후기의 하단 선 겹침 완화 확인.
- `Assets/Docs/Verification/WorkB/boss_result_fixed_1080.png`: 1920×1080, 7자리 점수와 2줄 보스 후기 스트레스 프리뷰.
- 앞선 `result_screen.png`, `boss_result.png`는 본문 정렬 수정 전 실제 루프 캡처다.
- 조명 대표 캡처와 검증은 `Docs/LIGHTING_V3_VERIFICATION_KO.md` 참조.

### Console 주의점

B 루프/상태 검사 구간 이후 새 오류는 없었다. 캡처 저장의 AssetDatabase.Refresh에서 기존 `SpecialAudienceOutline`의 `_TexelSize/_ST` 2D SRP Batcher 호환 경고가 2회 기록됐다. 이번 작업에서 해당 셰이더/배칭을 임의로 변경하지 않았다.

조명 초기 진단에서 직접 Camera.Render를 사용한 캡처에는 역사적 URP Renderpass 오류가 있었다. 최종 검증은 그 경로를 쓰지 않았고, 게임 Pause 후 실제 렌더 프레임을 거쳐 다시 촬영했다. Console 역사 전체가 오류 0이라고 주장하지 않는다.

최종 재검사에서 픽셀 비활성 정리 검사 1개가 실패했다. Ready 정리로 이미 disabled인 emitter에 검사 코드가 직접 Emit한 뒤 다시 disabled를 넣어 OnDisable 전이를 만들지 않은 테스트 준비 문제였다. 검사에서 실제 활성→비활성 전이를 만들도록 보정 후 재검사했다. 플레이 코드의 카드 보충은 발광 전에 emitter를 활성화한다.

## 변경 파일 안내

- 교육: `Assets/Scripts/Tutorial/TutorialFlow.cs`
- 대화: `Assets/Scripts/Dialogue/DialogueTypes.cs`, `DialoguePanel.cs`, `Assets/Scripts/Editor/DialogueSetupMenu.cs`, 기존 intro_stage_02/03/05_boss asset
- 기사: `Assets/Scripts/Tour/Result/ResultNewspaperCatalog.cs`, `ResultHeadlineSelector.cs`, `ResultNewspaperView.cs`, 기존 Resources 카탈로그
- 관객: `Assets/Scripts/Audience/AudienceMemberActor.cs`, `AudienceRosterPresenter.cs`, `AudienceTypes.cs`, `Assets/Scripts/Stage/Boss/RivalStagePlaceholder.cs`
- UI 연출: `Assets/Scripts/Cards/UI/CardHandUI.cs`, `Assets/Scripts/Tour/TourMapView.cs`, `Assets/Scripts/UI/PerformanceTimerUI.cs`, `Assets/Scripts/Effects/UIPixelBurstEmitter.cs`, 신규 `PointerPixelFeedback.cs`
- 연결 도구: 신규 `Assets/Scripts/Editor/PlaytestWorkBSetup.cs`. 전체 UI 재생성이 아닌 내용/참조만 갱신하며 기존 편집 대사는 덮어쓰지 않는다.
- Main: 도장 SFX와 기본 OFF 커서 컴포넌트만 B에서 연결. Title/TourHub 씬을 저장 수정하지 않았다.

덱·증강 효과·점수 공식·보스 미션/드레인 규칙·BGM 피치/속도는 변경하지 않았다. 기존 dirty worktree 전체가 이번 B의 변경인 것은 아니다.

## 다음 사람 확인

1. `TutorialFlow > Compact Experience`를 Play Mode에서 비교: 설명 클릭 2회 감소가 실제 이해를 돕는지. 기본은 기존 교육이다.
2. 후반 카드 선택이 조명만 보는지, 옷/움직임을 읽는지 기록. 확인 전 조명 힌트 축소 금지.
3. 모쉬핏 움직임 강도, 시계 긴박감, 맵 먼지를 실제 음악과 함께 평가.
4. 큐시트 초안/추가 표정/앰프 사고 삽화 승인·아트 제작. 신문 스킨의 인쇄 선과 작은 라벨 정렬은 최종 디자인 단계에서 함께 확인.
5. 모바일 WebGL 길게 누르기·실제 카드 드래그·오디오·창 크기 변경/전체화면 복귀 테스트. 빌드/배포는 이번에 하지 않았다.

자세한 문구와 비교표: `Docs/WORK_B_CONTENT_AND_CUES_KO.md`.

최종 인계: Main Edit Mode, 05:47 재컴파일 성공. 5시간 사용량 최근 67%/주간 26%; 초기화 크레딧 사용 없음. 재개 예약은 중복 실행을 막도록 PAUSED로 전환했다.
