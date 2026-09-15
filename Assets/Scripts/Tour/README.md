# Tour — 투어 진행 · 맵 선택 화면

```
Title(TourTitleLauncher) → TourHub(TourPrototypeUI) → Main(TourPerformanceBridge) → TourHub …
TourRunManager: Map → Dialogue → Performance → Result → Reward → Map … → Completed / Failed
```

| 파일 | 역할 |
|---|---|
| `TourRunManager` / `TourRunState` / `TourTypes` | 투어 상태와 전이만 담당 (점수·규칙은 GameManager·스테이지가 담당) |
| `StageDefinition` | 한 공연의 불변 설정 (Settings/Tour/Stage0x.asset) |
| `TourPerformanceBridge` | Main 씬. 시간·목표 점수 주입, 기록 확정 → 공연 결과 1회 제출, 신문 결과창 표시 (없으면 CONTINUE TOUR 오버레이) |
| `Result/PerformanceReport` | 확정 공연 기록 (목표·콤보·피버·관객·특별 관객·위기·보스). `StageResult.report` |
| `Result/PerformanceStatsRecorder` | 기록 수집. 공연 중 EventBus 로 집계하고 종료 시 `Freeze`. 랭크는 확정 점수 + 공통 기준(`ComputeRank`) |
| `Result/ResultHeadlineSelector` | 기사 선택. 일반/보스 → 성공/실패 → 등급 문구 → 대표 기록 1개 부제. 보스는 등급 대신 도장·결말 한 줄 |
| `Result/ResultNewspaperCatalog` | 스킨(stageId → 지면 스프라이트·강조색)·헤드라인·부제·도장·버튼·기믹 기사 문구. `Resources/Tour/ResultNewspaperCatalog.asset` |
| `Result/ResultNewspaperView` | 결과 화면(신문). 데이터를 표시하고 다음 진행만 전달. 등장 연출 ≈1.5초, 클릭으로 즉시 완료(그 클릭은 넘기지 않음) |
| `TourPrototypeUI` | TourHub 씬. 단계별 화면 전환. Map/Dialogue 는 아트 화면(TourMapView·DialoguePanel), 나머지는 임시 패널 |
| `TourMapConfig` | 맵 화면 아트·노드 좌표·연출 수치. `Resources/Tour/TourMapConfig.asset` |
| `TourMapView` | 맵 선택 화면 (피그마 "맵 선택"). 아래 참고 |
| `AugmentSelectionCoordinator` 등 | 증강 선택 (장우용 담당) |
| `TourDebugInput` | 디버그 키 (Home 클리어 · Delete 실패 · F10 단계 진행). 씬 배치 불필요 |

## 맵 선택 화면 (TourMapView)

- 지도(`0910_art/배경/map_background_v2`) 위에 노드를 점으로 찍고 노드 사이를 빗금(경로 방향으로 회전한 점)으로 잇는다. 배경·경로·노드·버스는 전부 `MapContent`(1920×1080, 중앙 기준) 아래에 있고, 축소 연출은 이 오브젝트 하나의 scale/position 만 움직인다
- 노드 배치: 1 왼쪽 작은 섬 마을 · 2 큰 섬 위쪽 마을 · 3 큰 섬 아래 도시 · 4 도시 섬 · 5 우하단 스타디움 돔
- 노드 상태: 클리어 = 핀 + 마이크 + 랭크 글자 / 현재 = 활성 핀 / 다음 = 클릭 가능한 회색 점 / 잠김 = 흐린 점. 보스 노드는 마이크 대신 보스 아이콘
- 너구리 얼굴은 좌 30° ↔ 우 30° 두 프레임을 `frameInterval`(0.4초)마다 교대, 버스는 살짝 들썩인다
- 다음 노드 클릭 → `travelStartDelay` 뒤 버스가 빗금을 따라 이동(`travelSecondsPerSegment`, Linear) → 도착 핀과 아이콘이 `pinPopStartScale`(0.3배)에서 1배로 한 번 커지며 등장(`pinPopDuration`) → `arrivalBlinkDuration` 동안 가만히 → `NodeSelected` → `TourRunManager.SelectNode` → 대화창(이전 화면 블러)
- 핀은 버스가 도착한 뒤에야 나타난다(`NodeSlot.Revealed`). 이미 열려 있던 노드로 다시 돌아온 화면(Reward 뒤 Map 등)에서는 팝 없이 바로 보인다
- 핀은 평소 1배로 정지해 있고, 마우스를 올린 노드만 핀·아이콘이 `pinHoverScale`(1.25배)로 커졌다가 벗어나면 돌아온다 (`pinHoverDuration`). 점멸은 없다. 핀이 숨겨진 다음 노드는 점이 커진다
- **결승 축소 연출**: 마지막 노드가 아직 잠겨 있는 동안은 `introZoomScale`(1.55배)·`introZoomCenter`(0.31, 0.64) 로 좌상단 3개 섬만 보이고, 마지막 노드로 가는 빗금은 그리지 않는다. 4번 클리어 → Travel 에서 `zoomOutDuration`(2초) 동안 천천히 전체 지도로 축소 → 5번으로 가는 빗금이 `pathRevealDuration` 동안 순서대로 나타남 → 버스 이동 → 핀 팝. 이후 맵은 계속 전체 지도
- 좌하단 "< 타이틀로". 좌상단 "보유 증강 확인하기" 는 증강 담당 몫이라 비워 두었다

노드 좌표(정규화)·배율·간격·시간·축소 수치는 전부 `TourMapConfig.asset` 인스펙터에서 조절한다. 코드에는 수치가 없다.

## 대화창 초상화

`Settings/Dialogue/Sequences/*.asset` 의 각 줄에 `portrait` 가 있으면 화자 위치(너구리 = 왼쪽, 나머지 = 오른쪽)에 뜬다. 스프라이트가 있는 화자는 너구리·고슴도치·LUX//FAUNA 셋 (`0910_art/스탠딩일러`). TourHub 씬 `PortraitLeft`/`PortraitRight` 는 좌·우 하단 앵커, 560×640, preserveAspect — 위치·크기는 인스펙터에서 고친다.

## 결과창 (신문)

- 공연 종료 → `TourPerformanceBridge` 가 `PerformanceStatsRecorder.Freeze` 로 기록을 확정하고 `StageResult` 에 담아 제출 → `ResultHeadlineSelector.Compose` → `ResultNewspaperView.Show`
- 지면은 `Docs/ArtDrafts/ResultNewspaper_20260910_v1` 초안 5장 (stage_01 골목일보 · 02 동물중앙 · 03 Rolling Hairballs · 04 Pitchfur · 05 The Animal Times). 03·04 는 배경 투명화가 안 된 초안이라 체크무늬가 보인다 — 아트 교체 시 `ResultNewspaperCatalog` 의 스킨 스프라이트만 바꾼다
- 씬 계층 `Main/[TourPerformance]/[ResultNewspaper]` 의 텍스트·사진·도장 위치는 인스펙터에서 고친다 (지면 1672×941 기준 로컬 좌표). 스킨마다 칸 위치가 조금씩 달라 공통 좌표를 쓴다
- 문구·임계값(부제를 쓸 최소 콤보 등)은 전부 카탈로그 에셋에 있다
- 다음 진행 버튼: 성공 → "증강 선택하기" / 마지막 노드 성공 → "투어의 결말 보기" / 실패 → "투어 결과 확인". 재도전 버튼은 없다 (체크포인트 정책 미확정)
- 아직 없음: 상세 기록 펼치기, 실제 하이라이트 캡처, 신문 이미지 저장, 밴드 동료 레이어(밴드 편성 시스템 자체가 아직 없다)

## 셋업

```
Tools/Tour/Setup Result Newspaper   지면 초안 복사·임포트 + 카탈로그 + Main 씬 [ResultNewspaper] 계층 (있으면 유지)
Tools/Tour/Rebuild Result Newspaper 계층을 지우고 다시 만든다 (씬에서 고친 위치는 사라짐)
Tools/Tour/Setup Prototype Loop   StageDefinition 5개 · TourHub 씬 · Title 시작 버튼 연결
Tools/Tour/Setup Stage Runtime    스테이지 프리셋·룰·카탈로그 (Scripts/Stage/README.md)
Tools/Tour/Setup Tour Map         0823_art 임포트 교정 + TourMapConfig 생성 (있으면 유지)
Tools/Tour/Fix Tour Art Import    맵·대화 아트만 Single · Point · Mipmap Off 로 교정 (2_* 증강 아트는 건드리지 않음)
Tools/Tour/Apply 0910 Art         0910_art 임포트 교정(배경 4096) + 맵 배경 v2·노드 5개 좌표·MapContent 재구성 + 초상화 슬롯 + 스테이지 1~3 배경 교체 + [StageSets] 재생성
```

`Apply 0910 Art` 는 이미 실행돼 있다. 새 아트가 오면 `Editor/TourMapArtSetup.cs` 의 경로·`NodePositions` 만 바꾸고 다시 실행한다 (TourHub·Main 씬을 함께 저장한다). `stage4_background_v2` 파일명은 **스테이지 3** 의 배경이다 (아트 파일명 기준).

큰 텍스처 첫 임포트는 Pipeline CLI 의 30초 제한에 걸릴 수 있다. 에디터에서는 계속 진행되므로 끝난 뒤 다시 실행하면 된다.
