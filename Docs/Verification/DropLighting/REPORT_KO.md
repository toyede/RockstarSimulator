# 디스토션 조명 · 보스 드랍/관객 반응

## 수정 파일

- `Assets/Scripts/Effects/LocalScreenEffectShader.cs`
- `Assets/Scripts/Effects/ScreenEffectSystem.cs`
- `Assets/Scripts/Effects/PartialDistortionEffect.cs`
- `Assets/Scripts/Lighting/StageShowDirector.cs`
- `Assets/Scripts/Lighting/StageLightingRig.cs`
- `Assets/Scripts/Stage/Boss/BossEvents.cs`
- `Assets/Scripts/Stage/Boss/BossStagePresentation.cs`
- `Assets/Scripts/Stage/Boss/RivalStagePlaceholder.cs`

## 원인과 수정

### 디스토션 중 조명색 소실

Renderer2D의 Camera Sorting Layer Texture는 Default 레이어까지 캡처한다. 조명 워시/빔/역광은 Effects의 -150/-100/-80에 그려지는데, 디스토션이 Effects 0에서 조명 전 캡처를 덮어써 조명색이 사라졌다.

풀링형 `ScreenEffectSystem`과 독립형 `PartialDistortionEffect`의 정렬을 공통 -200으로 맞췄다. 순서는 월드 캡처 → 디스토션 → 조명 합성 → 기존 HUD/UI다. 렌더 파이프라인/레이어 설정 에셋이나 UI는 변경하지 않았다.

### 보스 미션 예고에 드랍 조명 없음

기존 조명은 PatternId `drop`에만 반응했고, 임팩트는 카메라가 우리 무대로 돌아온 뒤의 `BossPatternStarted`에서 시작했다.

- 모든 미션 예고의 라이벌 체류 진행도 55%에서 표시용 `BossDropImpact` 이벤트를 1회 발행한다.
- 조명은 예고의 정적/충전 뒤 라이벌 화면에서 레이저와 스모그/임팩트를 재생한다.
- 우리 무대 복귀 후 미션 시작 시 같은 임팩트를 재생하지 않는다. 예고가 없는 경로는 시작 이벤트를 폴백으로 사용한다.
- 다음 예고/결과/초기화에서는 큐 상태를 복구한다.

### 라이벌 관객 드랍 반응

동일 `BossDropImpact` 이벤트로 라이벌 관객의 Transform 점프·좌우 기울기·스쿼시를 약 2.4초간 덧붙인다. 개체별로 시작/높이를 조금씩 다르게 하며 끝에는 부드럽게 기존 동작으로 복귀한다. 이동 중인 관객의 입퇴장 경로는 덮지 않는다.

새 스프라이트/애니메이션은 필요 없다. 관객 HP/인원/성향/점수는 바꾸지 않으며, 기존 보스 HP 기반 Calm/Mid/Hype 표현을 유지한다. 일시정지 시 멈추고 Ready/GameOver/Disable 시 초기화한다.

## 검증

- 연결 Unity Editor 컴파일 성공.
- GPU 픽셀 비교 6/6: 조명색 `RGBA(0.082,0.914,0.996,1)`이 디스토션 중 동일하게 유지됐다. 이전 정렬 순서를 적용한 대조군에서는 `RGBA(0.133,0.133,0.133,1)`로 색이 사라져 원인을 재현했다.
- 보스 Play Mode 28/28: 실제 일반 미션의 예고→라이벌 화면 임팩트→우리 화면 복귀→미션 시작, 중복 방지, 관객 추가 점프, 점수/팬/HP 상태 불변, 일시정지, 대기 복귀와 5종 패턴의 조명 표시 경로를 검사했다. 예고가 없는 경로에서도 결과 이후 다음 미션이 재발동되는 것까지 확인했다.
- `Assets/Docs/Verification/DropLighting/boss-drop-peak.png`에서 라이벌 조명과 관객 반응 확인.
- 등급 게이지는 수정하지 않았다. 기존 작업의 UI/Point/Title 변경도 유지했다.
- 최종 검증 구간 Console seq 68 이후 새 Error 0개. Main 씬/설정 에셋 변경 없음. `git diff --check` 통과. Play Mode 종료 후 원래 Title 씬으로 복귀했다.
- 전체 투어 완주·WebGL 빌드·실기기 성능 검증은 하지 않았다.

초기 픽셀 검사 도구가 HideAndDontSave 풀링 오브젝트를 일반 FindObjects로 찾지 못했으나, 실제 핸들의 인스턴스를 조회하도록 검증 도구를 수정해 완료했다. 중간 캡처를 위한 Editor 일시정지를 제거한 연속 실행으로 보스 복귀/종료 검증을 완료했다.
