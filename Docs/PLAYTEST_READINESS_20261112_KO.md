# 지인 테스트 전: 로딩·계측·영문 설정

## 로딩 진단

재빌드나 실제 배포본의 전송 시간 측정 없이 확인한 소스/Unity 의존성 결과다. 아래 MiB는 **원본 합계**이며, 배포 .data 다운로드량이나 런타임 메모리가 아니다. 같은 파일이 공용 리소스와 씬에 모두 연결될 수 있으므로 합산하지 않는다.

| Unity 루트 | 오디오 원본 | 신규 동료 프레임 원본 |
| --- | ---: | ---: |
| Resources/SoundLibrary | 38개 · 87.8 MiB | — |
| Title / TourHub 씬 직접 의존성 | 각 0개 (Resources는 별도) | — |
| Main 씬 | 10개 · 29.2 MiB | 73장 · 29.4 MiB |

- **첫 방문에 타이틀 전에 오래 걸림:** 공용 Resources 카탈로그의 스테이지별 클립과 Main 의존 에셋이 초기 Web 빌드 데이터에 포함될 가능성이 높다. 기존 LastBuild 보고서는 8월 12일 것이므로 현재 배포 수치로 사용할 수 없다. 랭킹 서버는 랭킹 팝업을 열 때만 조회하며, 이 시작 경로를 차단하지 않는다.
- **공연장 이동 중 오래 걸림:** Main은 73장의 신규 동료 프레임과 오디오 등 공연 리소스를 한 번에 참조한다. 기존 SceneLoader는 페이드아웃 후 비동기로 로드하며, 90% 이후 씬 활성화와 Awake가 따로 걸린다. 비동기 로드라는 사실만으로 활성화 비용이 없다고 볼 수 없다.
- **공연 직전 끊김:** 용량이 큰 트랙 몇 개는 Preload Audio Data가 꺼져 있고 Load In Background도 꺼져 있다. 첫 재생 비용 후보이지만 Web 대상 지원/임포트 결과와 타이밍을 실제 확인하기 전에는 원인 확정이 아니다.

검증·개선 순서:

1. 현재 Web 릴리스 파일을 기준으로 캐시 끈 첫 방문과 캐시 켠 재방문을 각각 3회 기록. Network의 `.data` / `.wasm` 전송 크기·시간과 타이틀 첫 조작 가능 시점을 분리한다. 전송 후 기다림이 길면 초기화/텍스처 업로드를 우선 프로파일링.
2. 공연 전환은 화면 페이드, SceneLoader의 90% 도달, 활성화 완료, 첫 입력 가능 시점을 분리. 현재 작업에서는 로딩 정책을 바꾸지 않았다.
3. Build Report에서 상위 기여 에셋을 확인한 뒤, 타이틀에 필요한 음원만 기본 묶음에 두고 후반 BGM/스테이지 그림을 분리해 직전 씬에서 미리 받아둔다. 음원은 길이·채널·품질을 들어보면서 조정하고, 텍스처는 동료 프레임의 실제 표시 크기에서 Point 픽셀 경계가 유지되는 임포트 최대 크기를 찾는다.
4. 현재 Web 설정은 데이터 캐싱 켜짐, JS 압축 해제 fallback 꺼짐. 호스팅의 실제 Content-Encoding과 재방문 캐시 동작을 Network에서 확인한 뒤 압축/번들을 결정한다.

공식 문서: [Unity Web AssetBundles](https://docs.unity.com/en-us/engine/6000.0/manual/platform-specific/webgl/building-distribution/assetbundles).

## 플레이테스트 지표

옵션에서 동의가 기본 꺼짐이다. Editor/개발 빌드는 통계에 섞이지 않는다. 릴리스에서 동의하면 닉네임, 기기 식별자, 입력 원문 없이 공연/튜토리얼 단위 숫자만 기기에 저장한다. 표본 UUID는 같은 전송을 중복 제거하기 위해서만 쓰며, 사람이나 여러 판을 연결하지 않는다. 보류 큐는 30건까지이며 공유를 끄면 보류 데이터는 지운다. 로컬 기록과 서버 공유의 동의·큐는 분리하며, 기존 로컬 기록은 서버가 켜져도 업로드하지 않는다. 이미 전송된 기록은 이 옵션으로 삭제되지 않는다.

- **선택 성향 분포:** 정상 소비된 공연 카드 CHILL/SINGALONG/MOSH 카운트. Utility, 봉인·입력 거부, 연습 카드는 제외.
- **저격 명중:** 요청이 활성일 때 사용한 SPECIAL 수, 실제 대상에 드롭한 SPECIAL 수, 서버 판정이 아닌 게임 로직의 실제 명중 수를 각각 기록. 명중률은 명중/대상 드롭과 명중/활성 요청 중 사용을 따로 계산한다. 이탈로 기록 자체가 없거나 요청에 카드를 한 장도 내지 않은 횟수는 이 비율에 들어가지 않는다.
- **튜토리얼:** 스테이지별 완료/스킵/이탈 구분, 걸린 시간(실시간), 막힌 카드 입력 수. 평균 완료 시간에는 완료만 넣는다. 브라우저 종료 등 강제 종료는 보장되지 않는다.
- **이해도 단서:** 튜토리얼 밖 각 공연 첫 3회 카드 중 양수 점수 비율. 연습 이해도를 직접 증명하지 않으므로 행동 관찰 및 “왜 그 카드를 냈는지” 질문과 함께 해석한다.

서버 연결은 비활성 상태다. `PlaytestMetricsMigration.sql`을 승인된 테스트 Supabase 프로젝트에서 실행하고 유효한 행, 중복, 잘못된 데이터, 비인가 SELECT/UPDATE/DELETE를 검증한다. 현재 클라이언트는 랭킹과 같은 프로젝트 호스트의 별도 RPC를 호출하도록 준비됐으므로, 테스트 프로젝트가 따로라면 먼저 호스트를 분리한다. 그 후에만 테스트 빌드에 `Resources/Playtest/EnableRemoteMetrics.txt`를 넣고 실제 Web에서 동의/거부/오프라인 큐를 확인한다. 데이터 보관 기간과 접근 담당자를 정하고, 외부 공개 빌드에서는 익명 RPC에 직접 쓰기 권한을 열기보다 제한된 수집 엔드포인트를 사용한다.

## 영문 설정

타이틀/일시정지 옵션의 언어 선택을 저장한다. 기본 메뉴, 랭킹 조회 문구, 스테이지 1·2 교육 단계와 결과 신문 UI를 1차로 연결했다. 그림에 그려진 글자는 대상에서 제외했다. 별도 스토리 대사 에셋, 카드 이름/툴팁, 보스·이벤트·증강, 엔딩/허브의 동적 문구는 미완료이므로 영어는 Preview로 표시한다.

## 이번 변경 파일

- 연출: `CompanionPerformanceAnimator.cs`, `ResultNewspaperView.cs`, `ResultRankInk.cs`.
- 계측: `CardSystem.cs`, `TutorialFlow.cs`, `TourPerformanceBridge.cs`, `PlaytestMetrics.cs`, `RemoteLeaderboardClient.cs`, `PlaytestMetricsMigration.sql`.
- 영문 설정: `GameLanguage.cs`, `OptionsPopup.cs`, `TutorialFlow.cs`, `TutorialOverlayUI.cs`, `ResultHeadlineSelector.cs`, `ResultNewspaperView.cs`, `TitleLeaderboardPopup.cs`.

기존 Editor Play Mode에서 동료 두 명의 SpeedMultiplier 0.75, 실제 결과 UI의 D 등급(숨김/중간 필기/완료), 클릭 시 연출 즉시 완료, 옵션의 영문 전환 및 동의 기본 꺼짐을 확인했다. 런타임 검증에 사용한 언어 저장값은 원복했다. 실제 Web 빌드·업로드·운영 DB 변경 및 계측 전송은 하지 않았다.
