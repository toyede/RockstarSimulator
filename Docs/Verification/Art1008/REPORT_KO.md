# 1008 아트 적용

- 스프라이트 Import Filter Mode를 Point로 통일했다. 최초 549개 중 71개가 Point가 아니었으며, 카드 애니메이션 일부도 포함됐다. 필터 외 압축/해상도/스프라이트 분할/폰트 텍스처는 변경하지 않았다.
- `Assets/Scripts/Editor/PointSpriteImportPolicy.cs`가 Assets의 Sprite 텍스처 재임포트 시 Point를 유지한다. 최초 도입 시 Unity의 텍스처 재임포트가 발생했다.
- 실제 신규 파일명은 `Assets/Sprites/1008_art/tuto-mission_UI.png`. 기존 런타임 경로 `Assets/Resources/UI/CueSheets/MissionSlate.png`의 이미지 내용만 교체해 기존 GUID와 공용 미션 호출을 유지했다. 두 파일 SHA-256 동일.
- 새 슬레이트 내부가 투명하므로 안쪽에만 어두운 바탕을 추가했다. 제목/시간을 빗금 아래로, 본문/진행도를 판 내부에 배치하고 밝은 글자/강조색을 적용했다. 패널 자체 위치·크기는 변경하지 않았다. 보스 진행도의 덮어쓰는 색도 새 바탕에 맞췄다.
- Title의 기존 AnimatedBackground에 `Assets/Sprites/1008_art/시작화면_배경/BG_0000.png`~`BG_0028.png`를 연결했다. 기존 1920×1080, PPU, Transform, 12fps, loop 및 메뉴 배치 유지. Title 씬 diff는 SpriteRenderer 첫 프레임과 애니메이션 프레임 29개 연결만 포함한다.

## 확인

- Unity 컴파일 성공.
- 전체 Sprite TextureImporter 검사: Point 아닌 항목 0개.
- Title Play Mode 검사 6/6 통과: 29장, 신규 경로, 실제 프레임 변화, 기존 속도/반복, Point, 신규 아트 출력.
- 공용 미션 UI를 기존 보스 패널 크기 512×384로 Play Mode 렌더링해 새 아트/폰트/배경 가독성 확인.
- 최종 확인 구간 Console seq 19 이후 새 Error 없음. 초기 import 대기 중 Pipeline 요청 타임아웃은 게임 오류와 구분했다.
- meta의 유효 설정 변경은 필터 1→0이다. 연기 스프라이트 3개에는 Unity가 비활성(`overridden: 0`) WebGL 기본 항목도 직렬화했다. `git diff --check` 통과.
- Play Mode 종료, Title 씬으로 유지. WebGL 빌드 및 각 스테이지 미션의 전체 플레이는 실행하지 않았다.

캡처: `Assets/Docs/Verification/Art1008/title-new.png`, `Assets/Docs/Verification/Art1008/mission-new.png`.
