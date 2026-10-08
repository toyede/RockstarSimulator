# Raccoon Roll — 조명 추가 아트 v1

제작: 2026-10-08 · 도구: 내장 image_gen / imagegen 스킬 · 상태: **투명 PNG 초안 제작 완료, Unity Import·씬 연결·게임 검증 미실행**.

기존 스테이지 배경, 수동 소품 배치, Main 씬, 런타임 코드, .meta는 이번 작업에서 수정하지 않았다.
실제 게임에서 참조하기 전에 이 폴더의 선택본을 공식 Unity Pipeline으로 적절한 Assets 폴더에 Import한다. .codex 생성 폴더를 게임의 에셋 경로로 사용하지 않는다.

## 1. 납품 파일

| 파일 | PNG 원본 크기 | 사용할 곳 | 역할 |
|---|---:|---|---|
| [VFX_PixelSmog_Neutral_v1.png](E:/UnityProjects/RockstarSimulator/Docs/ArtDrafts/Lighting_20261008_v1/VFX_PixelSmog_Neutral_v1.png) | 1536 × 1024 | Stage 5 LUX//FAUNA, ParticleSystem | 흰색/회색의 낮게 퍼지는 스모그. 색·크기·불투명도를 런타임에서 조절 |
| [ST02_CeilingPAR_Unlit_v1.png](E:/UnityProjects/RockstarSimulator/Docs/ArtDrafts/Lighting_20261008_v1/ST02_CeilingPAR_Unlit_v1.png) | 1254 × 1254 | Stage 2 지하 라이브홀 | 낡은 원통형 천장 PAR 기구. 렌즈 발광과 빨간 빔은 별도 |
| [ST04_StudioSoftbox_Unlit_v1.png](E:/UnityProjects/RockstarSimulator/Docs/ArtDrafts/Lighting_20261008_v1/ST04_StudioSoftbox_Unlit_v1.png) | 1536 × 1024 | Stage 4 방송국 | 전원이 꺼진 사각 면광원 기구. 넓은 중성 백색 조명은 별도 |
| [ST04_OnAirFrame_Blank_v1.png](E:/UnityProjects/RockstarSimulator/Docs/ArtDrafts/Lighting_20261008_v1/ST04_OnAirFrame_Blank_v1.png) | 2172 × 724 | Stage 4 방송국 | 빈 방송 사인 프레임. ON AIR 글자는 TMP, 점등은 코드 |

각 PNG는 하나의 오브젝트/입자만 담는다. 아틀라스나 애니메이션 시트가 아니다. ON AIR 중앙창은 빈 어두운 면이며, 투명 구멍이 아니다.

## 2. 검증한 것과 남은 것

확인 완료:

- 네 파일 모두 실제 RGBA PNG. 가짜 체크무늬 배경을 투명하다고 부르는 것이 아님.
- 네 모서리의 Alpha = 0. 각 파일에 완전 투명 픽셀이 존재.
- 스모그에는 부분 투명도가 존재. PNG Alpha 범위 0~254.
- 천장 PAR와 ON AIR 프레임의 Alpha 범위 0~255. 소프트박스는 0~254.
- 단일 오브젝트의 형태, 글자가 없는 사인, 기구에 빛줄기가 합쳐져 있지 않은 것을 육안 확인.
- 선택된 생성 원본을 프로젝트 내부에 비파괴적으로 복사. 기존 파일 덮어쓰기 없음.

남은 작업:

- 현재 파일은 AI가 출력한 **대형 마스터 초안**이다. 계획의 32~64px 스모그나 64~96px 기구 런타임 텍스처로 최적화된 파일은 아니다.
- 기구 외곽에는 낮은 Alpha의 부드러운 잔상이 일부 남는다. 최종 적용 전 실제 밝은/어두운 배경에서 확인하고 필요하면 하드 컷아웃 정리를 한다. 완벽한 픽셀 단위 외곽 정리 완료로 보고하지 않는다.
- 픽셀 격자·축소 품질·PPU·피벗·광원 시작점을 실제 무대 축척에 맞춰 결정해야 한다. Point 필터만 켠다고 원본의 픽셀 격자가 자동으로 정돈되지는 않는다.
- Unity Import, 광원/입자 머티리얼 연결, 씬 배치, 런타임 점멸·분출, WebGL 성능은 아직 검증하지 않았다.

## 3. 적용 규칙

### 스모그

- RGB는 중성 베이스로 사용. 마젠타/시안은 입자 색 또는 조명 표시 계층에서 약하게 추가.
- 투명도가 있는 머티리얼로 표시. 흰 구름을 불투명한 사각형으로 렌더링하지 않음.
- StageSet/라이벌 월드 앵커 아래의 ParticleSystem에서 사용. 카메라 자식 먼지 시스템과 분리.
- 완전 투명 여백을 포함한 크기이므로 실제 보이는 연기 영역을 기준으로 크기를 조절.
- 처음부터 얼굴·Hover·미션 위에 배치하지 않음. 바닥/트러스 하단 중심.

### 천장 PAR / 방송 소프트박스

- SpriteRenderer용 베이스 기구. 렌즈/패널 강조와 빔은 다른 Renderer/Light가 담당.
- 본체 Transform과 출광 위치 자식 Transform을 분리. 이미지 중심을 광원 원점이라고 가정하지 않음.
- Stage 2는 PAR의 짧은 직하광, Stage 4는 Softbox의 넓고 안정적인 면광원.
- 기구를 매 카드마다 흰색으로 번쩍이게 하지 않음. 해당 StageLightingRig만 표시값 소유.

### ON AIR 프레임

- 어두운 중앙창 위에 프로젝트 픽셀 폰트 TMP로 ON AIR를 표시.
- 빨간 텍스트와 작은 상태등의 점등을 코드로 제어. Mosh 힌트와 위치·형태 구분.
- 글자를 이미지에 굽지 않으므로 번역·방송 상태 변경 가능.

### Import 시작값 — 아직 미적용

- 기구/프레임: Sprite (2D and UI), Single, Alpha 유지, Point 필터, Mipmap Off.
- 스모그: 사용하는 ParticleSystem 머티리얼에 맞춰 Default Texture 또는 Sprite. Alpha 유지, Point 필터, Mipmap Off.
- 축소/압축은 가장자리와 픽셀 격자를 확인한 뒤 결정. 대형 원본을 그대로 여러 장 반복 렌더링하는 것을 최종 성능 설정으로 채택하지 않음.
- 빔/레이저는 기존 픽셀 shader·메쉬를 사용. 이번 PNG와 합쳐 굽지 않음.

## 4. 이번 묶음에 포함하지 않은 것

- 기존 방송/EDM 배경의 dark base + 발광 마스크 분리: 실제 연결된 배경 및 꺼질 영역을 확정한 뒤 별도 편집.
- 마젠타/시안 레이저 PNG와 LED 애니메이션 시트: 절차적 표시 계획이므로 신규 래스터 아트 불필요.
- 조명 코드, 미션/점수/팬 규칙 변경, 작업 B: 미실행.

정확한 생성 프롬프트는 [PROMPTS.md](E:/UnityProjects/RockstarSimulator/Docs/ArtDrafts/Lighting_20261008_v1/PROMPTS.md)에 기록했다.

