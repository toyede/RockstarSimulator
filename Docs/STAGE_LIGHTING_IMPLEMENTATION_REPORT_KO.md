# 스테이지 조명 — 1차 구현 결과

2026-10-08 · 실제 프로젝트 Main 씬에 연결·저장. 작업 B는 미실행.

## 플레이 피드백 반영 — V2

1차의 얇은 삼각형·여러 색 혼합을 폐기하고 **기존 PixelSpotlight2D 셰이더**로 넓은 감쇠·6단계 밝기·도트 디더를 복원했다. 아래의 1차 설명보다 이 항목이 최신 설정이다.

- 우리 무대의 모든 주 조명은 `StageLightController.HintColor` 하나를 사용한다. Stage 2/4도 공연장 고정색과 힌트색을 혼합하지 않는다. Fever 때만 기존 금색/백색 예외 유지.
- Stage 1: 넓은 힌트 조명 2개, 하늘색 배경과 약한 주황 보조광. 주 조명은 게임 힌트이므로 MOSH/SINGALONG 비율이 높아지면 빨강/보라로 바뀐다. 하늘색으로 고정해 잘못된 힌트를 주지는 않는다.
- Stage 2: 같은 색·같은 방향의 하향 조명 3개. 반경 8.5, 폭 6. 움직임은 작게 통일.
- Stage 3: 5개 작은 빔 대신 2개 넓은 교차 빔(반경 12, 폭 13).
- Stage 4: 큰 소프트박스 2개 + 넓은 힌트 빔. 첨부해 준 발광 소프트박스 PNG를 `ST04_StudioSoftbox_Lit_v2.png`로 가져왔다. 기존 수동 UI가 아닌 새 조명 자식만 이동.
- LUX: 마젠타/시안 유지. 큰 교차 빔 2개(반경 10, 폭 8), 대칭 레이저 기본 2개 / 강화·DROP 최대 4개. 레이저는 부드러운 가장자리와 밝은 중심선으로 교체. 무작위 같은 개별 위상은 제거.
- Stage 1/3 기본 백색 밝기 최소 1.05, Stage 4 최소 1.1. Stage 2/보스 밝기는 상향하지 않음. 정전 규칙은 이 기본 밝기보다 우선한다.
- 피버 설정과 기존 UI 위치 보존 검사 통과, 반복 적용해도 Rig 6개 유지, shader 오류 없음. Play Mode 활성화/정전/스모그 상한/Disable-Enable 검사 재통과. Stage 4 실제 렌더링 주 빔 2개가 힌트 RGB와 일치함을 확인.

수정 메뉴: **Tools / Lighting / Apply Lighting Feedback V2**. 이 메뉴는 피드백 적용용으로 새로 추가한 조명 자식의 배치를 의도적으로 재조정한다. 기존 StageSet 소품/수동 UI를 재배치하지 않는다. 일반 Install 메뉴와 구분해서 사용한다.

캡처는 `Docs/Verification/LightingV2`에 보관. 보스는 UI를 제외한 라이벌 앵커 전용 검증 카메라, 나머지는 실제 Game View이다. 미술적 만족도는 사용자 재플레이로 확인해야 하며, WebGL/박자 동기화는 아직 미검증이다.

## 적용 내용

| 공연장 | 표시 연출 |
|---|---|
| 1 골목 | 짧고 고정된 시안/따뜻한 빔. 기존 StageSet 소품 배치 유지 |
| 2 지하 | 빨강 테마, 천장 PAR 3개, 좁은 하향 빔·작은 스윕 |
| 3 페스티벌 | 보라 테마, 5개 부채형 빔. 기존 비·깃발 이벤트에 색/스윕 반응 |
| 4 방송국 | 중성 백색/회청색 빔, 소프트박스 3개, ON AIR 표시. 대형 화면 이벤트는 백색 강조 |
| 5 우리 무대 | 따뜻한 빔. Fever 때 금색/백색으로 전환 |
| 5 LUX//FAUNA | 마젠타/시안 빔 4개, 레이저 최대 6개, LED 체이스, 스모그 2개 분출구, 팬 이동 방향의 바닥등 |

보스 예고·패턴 시작·패턴 종료·Revenge·팬 이동·Drain을 기존 이벤트에서 읽는다. 예고 때 밀도를 낮추고, B2B/BEATMATCH/DROP은 다른 스윕을 사용한다. GUEST LIST는 레이저 수를 줄이고 KILL SWITCH는 일부 LED를 끈다. 성공/실패 응답은 우리/라이벌 무대로 구분한다. 최종 승패는 기존 `ClearVerdictOverride`가 있을 때만 읽으며 조명이 승패를 재판정하지 않는다.

## 책임 / 변경 경계

- `StageLightingProfile`: 색·속도·강도·공유 Material 등 표시용 SO. `Assets/Settings/Lighting`에 6개.
- `StageLightingRig`: 각 StageSet의 새 조명 자식만 제어. 기존 소품과 관객은 이동/삭제하지 않는다.
- `StageShowDirector`: 기존 이벤트 → 조명 상태. 관객·점수·카드·증강·미션 규칙 변경 없음.
- `PixelLaserRenderer` + `PixelShowBeam.shader`: 픽셀 월드 빔. 레이저마다 Light2D를 추가하지 않는다.
- `StageShowSprite.shader`: 생성 아트의 낮은 알파 테두리를 표시에서 제거. 원본 PNG는 보존.
- `StageLightController`: 새 Rig가 활성화된 스테이지에서 기존 성향 힌트를 좁고 낮은 강도의 조명으로 분리. 힌트 판정은 유지하고 전체 색 틴트/플래시만 억제. Rig 비활성 시 기존 StageGeometry로 복귀.
- `RivalStagePlaceholder`: 새 조명이 동작할 때 기존 LED/듀오 점멸을 맡기고 임시 검정 단상을 숨김. 팬과 듀오의 프레임 애니메이션은 유지. 새 Director 비활성 시 기존 표시로 폴백.
- 기존 `FeverSpotlight`: 게임 규칙 변경 없이 부드러운 1Hz 밝기 변조로 설정.

기존 Rank·게이지·시계·미션 UI 위치 및 디버그 기능은 수정하지 않았다. 기존 dirty 파일의 다른 작업도 보존했다.

## 설정 방법

Main 씬 Edit Mode → **Tools / Lighting / Install Stage Shows In Main**.

반복 실행해도 기존 Rig/Profile을 재사용한다. 기존 Rig의 수동 배치를 재생성하지 않는다. 각 StageSet의 `[LightingRig]`, 보스의 `[LUX LightingRig]`에서 위치를 조절하고 `Assets/Settings/Lighting/Stage*_Show.asset`, `Stage5_LUX.asset`에서 강도·색·속도를 조절한다. 새 조명을 끄려면 `StageShowDirector`를 비활성화한다.

아트는 `Assets/Sprites/Lighting`의 PNG 4개. 원본 초안은 `Docs/ArtDrafts/Lighting_20261008_v1`에 보존. Point / mipmap OFF / 비압축 / 최대 128~256으로 가져왔다. LED는 저장된 `LED_Square.asset`을 사용하며 런타임 생성 Sprite를 씬에 저장하지 않는다.

## 실제 검증

- Unity Editor 컴파일 완료, 새 shader 2개 오류 없음.
- 셋업 반복: Rig 6개 유지, 모든 Sprite 참조가 저장된 Asset.
- 셋업 전후: Stage 1 소품 Transform 및 기존 UI RectTransform 변화 없음. 새 월드 ON AIR 텍스트는 검사에서 별도 구분.
- Play Mode: Stage 1~4 활성 Rig 각 1개, Stage 5 활성 Rig 2개.
- 모든 Rig의 정전 표시 검사: 빔/레이저 꺼짐.
- 보스 Rival 앵커 일치 확인. 카메라 이동으로 효과 루트를 이동시키지 않음.
- 일시정지 전후 ShowTime `112.813858` 동일. Resume 후 진행.
- 스모그 100회 강제 분출: 생존 20개 / 상한 20개. Director Disable/Enable 3회 후 입자 0개.
- 코드 재로드 중 발생한 `PixelLaserRenderer` 임시 PropertyBlock 초기화 오류를 수정하고 다시 Play Mode 검사.
- 최종 Play Mode 진입·검사·종료 구간 Console 신규 오류 0개. 이전 Pipeline 타임아웃/수정 전 예외 및 기존 경고는 Console 이력에 남아 있다.
- Play Mode 종료, Main 씬 저장. 검증용 카메라는 런타임에서만 생성했고 저장하지 않음.

검증 스크립트: `Docs/Verification/Lighting/VerifySetup.cs`, `VerifyRuntime.cs` — Pipeline `eval_file`용 문장 파일(일반 MonoBehaviour 파일이 아님).

스크린샷: `Docs/Verification/Lighting/stage2.png`, `stage4.png`, `lux-final.png`. 앞의 두 장은 실제 Game View(UI 포함), 마지막은 라이벌 무대 전용 검증 카메라(UI 제외). 일부 중간 화면은 튜토리얼/강제 패턴 검증 중 캡처했다.

## 아직 확인/구현하지 않은 범위

- 정확한 BGM 박자 동기화: 공개 재생 위치 API가 없어 로컬 시간 + 게임 이벤트로 구동. 음악 재생 코드/피치는 변경하지 않음.
- WebGL 실제 빌드·모바일 프레임 성능·광과민 접근성 사용자 검사. 저점멸 기본값만으로 안전성을 보장하지 않음.
- 배경에 이미 그려진 빛까지 제거하는 정전용 마스크 아트.
- 실제 관객 경로를 따라가는 빛, GUEST LIST의 개별 관객 후광, 완전한 보스 공연 밸런스/승패 전수 검사.
- 작업 B(카드·관객 움직임 등). 조명 구현 범위에 넣지 않음.
