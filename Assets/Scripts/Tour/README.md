# Tour — 투어 진행 · 맵 선택 화면

```
Title(TourTitleLauncher) → TourHub(TourPrototypeUI) → Main(TourPerformanceBridge) → TourHub …
TourRunManager: Map → Dialogue → Performance → Result → Reward → Map … → Completed / Failed
```

| 파일 | 역할 |
|---|---|
| `TourRunManager` / `TourRunState` / `TourTypes` | 투어 상태와 전이만 담당 (점수·규칙은 GameManager·스테이지가 담당) |
| `StageDefinition` | 한 공연의 불변 설정 (Settings/Tour/Stage0x.asset) |
| `TourPerformanceBridge` | Main 씬. 시간·목표 점수 주입, 공연 결과 1회 제출, CONTINUE TOUR 오버레이 |
| `TourPrototypeUI` | TourHub 씬. 단계별 화면 전환. Map/Dialogue 는 아트 화면(TourMapView·DialoguePanel), 나머지는 임시 패널 |
| `TourMapConfig` | 맵 화면 아트·노드 좌표·연출 수치. `Resources/Tour/TourMapConfig.asset` |
| `TourMapView` | 맵 선택 화면 (피그마 "맵 선택"). 아래 참고 |
| `AugmentSelectionCoordinator` 등 | 증강 선택 (장우용 담당) |
| `TourDebugInput` | 디버그 키 (Home 클리어 · Delete 실패 · F10 단계 진행). 씬 배치 불필요 |

## 맵 선택 화면 (TourMapView)

- 지도(`bg_map_select`) 위에 노드를 점으로 찍고 노드 사이를 빗금(경로 방향으로 회전한 점)으로 잇는다
- 노드 상태: 클리어 = 핀 + 마이크 + 랭크 글자 / 현재 = 활성 핀 / 다음 = 클릭 가능한 회색 점 / 잠김 = 흐린 점. 보스 노드는 마이크 대신 보스 아이콘
- 너구리 얼굴은 좌 30° ↔ 우 30° 두 프레임을 `frameInterval`(0.4초)마다 교대, 버스는 살짝 들썩인다
- 다음 노드 클릭 → `travelStartDelay` 뒤 버스가 빗금을 따라 이동(`travelSecondsPerSegment`, Linear) → 도착 핀이 튀어나와(`pinPopDuration`) `arrivalBlinkDuration` 동안 점멸 → `NodeSelected` → `TourRunManager.SelectNode` → 대화창(이전 화면 블러)
- 좌하단 "< 타이틀로". 좌상단 "보유 증강 확인하기" 는 증강 담당 몫이라 비워 두었다

노드 좌표(정규화)·배율·간격·시간은 전부 `TourMapConfig.asset` 인스펙터에서 조절한다. 코드에는 수치가 없다.

## 셋업

```
Tools/Tour/Setup Prototype Loop   StageDefinition 5개 · TourHub 씬 · Title 시작 버튼 연결
Tools/Tour/Setup Stage Runtime    스테이지 프리셋·룰·카탈로그 (Scripts/Stage/README.md)
Tools/Tour/Setup Tour Map         0823_art 임포트 교정 + TourMapConfig 생성 (있으면 유지)
Tools/Tour/Fix Tour Art Import    맵·대화 아트만 Single · Point · Mipmap Off 로 교정 (2_* 증강 아트는 건드리지 않음)
```

큰 텍스처 첫 임포트는 Pipeline CLI 의 30초 제한에 걸릴 수 있다. 에디터에서는 계속 진행되므로 끝난 뒤 다시 실행하면 된다.
