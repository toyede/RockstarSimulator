# Dialogue — 공연 전 대화창 (기획서 §12·§14)

```
StageDefinition.preDialogueId ─→ DialogueCatalog (Resources/Dialogue) ─→ DialogueSequence ─→ DialoguePanel
                                        └ DialogueStyle (폰트·색·타이핑 속도·화살표)
```

| 파일 | 역할 |
|---|---|
| `DialogueTypes` | `DialogueLine`(화자·초상화·표정·본문·좌우·SFX), `DialoguePresentationContext`(공연장 이름·배경·룰 카드) |
| `DialogueSequence` | 시퀀스 하나 (`intro_stage_01` …). `Settings/Dialogue/Sequences/*.asset` |
| `DialogueCatalog` | sequenceId → 시퀀스. `Resources/Dialogue/DialogueCatalog.asset` |
| `DialogueStyle` | 표현 수치. `Settings/Dialogue/DialogueStyle.asset` (DungGeunMo SDF 연결) |
| `DialoguePanel` | 화면 + 진행: 클릭/Space/Enter/탭 → 다음, 타이핑 중 입력 → 즉시 완성, 스킵, 마지막에 룰 카드, 완료 콜백 1회 |
| `DialoguePanelFactory` | 계층 코드 생성 (프리팹 없을 때 폴백 + 프리팹 생성 메뉴가 사용) |
| `DialogueRunner` (`Dialogue`) | `Dialogue.TryPlay(id, context, onComplete)` 정적 파사드 |
| `Common/HangulTypewriter` | 한글 초성→중성→종성 조립 타이핑 프레임 (Hover 말풍선과 같은 방식) |

## 흐름

TourHub 의 `TourPrototypeUI` 가 Dialogue 단계에서 `Dialogue.TryPlay(stage.PreDialogueId, …)` 를 호출한다.
배경은 `StageVisualCatalog` 의 Base 를 어둡게 깔고, 마지막 줄 뒤에 같은 카탈로그의 룰 카드(제목·본문·아이콘)를 보여준 뒤
`TourRunManager.CompleteDialogue()` → Main 로드. 시퀀스가 없으면 예전 임시 텍스트 화면으로 폴백한다.

## 셋업

```
Tools/Dialogue/Setup Dialogue Data                  스타일·기본 시퀀스·카탈로그 생성 (있으면 유지)
Tools/Dialogue/Rewrite Default Sequences (Overwrite) 승인된 스토리 초안 적용 (수동 편집한 기본 대사는 덮어씀)
Tools/Dialogue/Create Dialogue Panel Prefab          Resources/Dialogue/DialoguePanel.prefab 생성
```

## 피그마 시안 적용

`Resources/Dialogue/DialoguePanel.prefab` 의 배치와 `DialogueStyle.asset` 의 색·폰트·화살표 스프라이트만 바꾸면 된다.
코드는 오브젝트 참조(`DialoguePanel` 인스펙터)만 알고 있다. 초상화는 `DialogueLine.portrait` 에 스프라이트를 넣으면 좌/우에 뜬다.

- 초상화: `Rewrite Default Sequences` 가 화자 이름으로 `0910_art/스탠딩일러/{raccoon|hedgehog|lux_fauna}_standing` 을 자동 연결한다. 너구리는 왼쪽, 나머지는 오른쪽. **스컹크 스탠딩은 현재 없어 이름과 대사만 표시**한다. 아트가 준비되면 시퀀스의 `portrait`에 연결하고 셋업의 `PortraitOf`에도 매핑을 추가한다.
- 표정·프레임 초상화: `Resources/Dialogue/DialoguePortraitCatalog.asset` (`Tools/Art/Apply 0918 Art` 가 만든다) 이 화자 ID + 표정 ID → 스프라이트(프레임)를 준다. 대화창은 줄마다 (speakerId, emotionId) 일치 → 화자 기본 → `DialogueLine.portrait` 순으로 고른다. 현재 너구리 happy(excited·relieved 포함)/angry/crying(sad), 고슴도치 angry, LUX//FAUNA 는 3프레임(0.45초 교대). 새 표정은 카탈로그에 항목만 추가하고 대사에 `Line(화자, 대사, "표정")` 로 붙인다.
- 캐릭터 말투: 너구리 = 성공하고 싶지만 허술한 보컬/기타 / 고슴도치 = 실용적이고 짓궂은 오랜 친구, 드러머 / 스컹크 = 여러 밴드를 거친 무뚝뚝한 선배 베이시스트 / LUX//FAUNA = 흥행과 즉각적인 관객 반응을 우선하는 프로 전자음악 듀오. 로봇 말투나 장르 자체를 악으로 취급하는 설정은 사용하지 않는다.
- 화살표: 줄이 끝나면 대화창 우하단 흰 모서리에 항상 고정한다. 위치는 `DialogueStyle.arrowOffset`으로 조절하며 본문 길이/줄 수를 참조하지 않는다. 과거 씬에 저장된 텍스트 따라가기 설정은 더 이상 사용하지 않는다. `arrowSprite`가 비어 있으면 12×8 픽셀 삼각형을 만들고, `arrowStepMotion`을 켜면 제자리에서 계단식으로 튄다.
- 타이핑: `typingInterval`(자모당 간격), `punctuationDelay`(문장 부호 뒤 정지), `typingSoundId`(SoundLibrary ID).
- DungGeunMo 에는 `▶` 글리프가 없다. 특수 기호는 ASCII(`>>`)를 쓰거나 폰트 폴백을 추가할 것.

## 적용된 스토리 초안과 연결 범위

주제는 **인디 아티스트의 고충과 성장, 자기 표현과 상업성 사이의 갈등**이다.
처음에는 너구리와 고슴도치 둘뿐이며, Stage 2에서 스컹크가 합류한다. 하이에나는 대사에 등장하지 않는다.
캐릭터에게 게임 규칙을 설명시키지 않고, 기존 스테이지 룰 카드·튜토리얼을 그대로 사용한다.

| 시퀀스 ID | 내용 | 현재 연결 |
|---|---|---|
| `intro_stage_01` | 골목 버스킹, 꺼진 마이크, 라이벌 광고 | Stage 1 공연 전 |
| `intro_stage_02` | 앰프 사고, 아마추어 둘을 보던 스컹크의 합류 | Stage 2 공연 전 |
| `intro_stage_03` | 내지 못한 곡과 전 밴드 이야기, 헤드라이너 예고 | Stage 3 공연 전 |
| `intro_stage_04` | 세 사람 모두 처음인 생방송, 스타디움 파이널 예고 | Stage 4 공연 전 |
| `intro_stage_05_boss` | 베이스 파트를 줄이라는 라이벌, 준비한 편곡으로 공연 | 보스 공연 전 |
| `ending_mosh` | 지하 클럽 크라우드서핑 | 투어 완료, Mosh 반응 점수 최다 |
| `ending_singalong` | 팬들의 떼창 영상을 올리는 세 사람 | 투어 완료, Singalong 반응 점수 최다 |
| `ending_chill` | 직접 발매한 음원, 스트리밍 반응과 녹음 방법 댓글 | 투어 완료, Chill 반응 점수 최다 |
| `ending_all_s` | 자신들의 투어 광고 앞에서 단체 사진 | 투어 완료, 전 공연 S 랭크 (성향보다 우선) |
| `ending_bad` | 공연장에서 장비를 빼는 세 사람 | 투어 실패 (2번째 노드 이후) |
| `ending_bad_before_join` | 같은 배드엔딩의 너구리·고슴도치 버전 | 투어 실패 (첫 노드) |
| `ending_common` | 공연 후 장비 정리 | 기존 ID 호환용, 자동 재생 안 됨 |

- 엔딩은 **5종**이다. `ending_bad_before_join`은 별도 엔딩이 아니라 합류 전 대사 변형이며, `ending_common`은 기존 ID 호환용이다.
- 엔딩 판정·재생은 `Scripts/Tour/TourEnding.cs` (`TourEndingSelector`) 가 하고 `TourPrototypeUI.ShowEnding` 이 `Dialogue.TryPlay(id, context, onComplete)` 로 튼다. 판정은 성향별 **반응 점수** 합계와 카드 사용 횟수(동률)만 쓴다 (`Scripts/Tour/README.md` "엔딩").
- 엔딩 대화의 `DialoguePresentationContext` 는 `useBackdropImage`(블러 대신 배경 그림) 와 `brightBackdrop`(일러스트를 어둡게 하지 않음) 을 쓴다. 일러스트는 `Resources/Tour/TourEndingCatalog.asset` 에 넣는다.
- 앰프 사고·전광판·엔딩 장면은 현재 **텍스트 지문**이다. 영상/애니메이션/신규 효과음을 구현했다는 뜻이 아니다. 이름과 초상화를 비운 줄은 양쪽 초상화와 이름표를 숨긴다.
- 실제 게임은 `.asset`을 읽는다. 소스 초안은 `Editor/DialogueSetupMenu.cs`의 `CreateDefaultSequences`에 있으며, 문구를 변경한 뒤 Rewrite 메뉴를 실행해야 에셋에도 반영된다. 이 메뉴는 기존 시퀀스 GUID와 씬/프리팹 배치, 기존 스타일을 유지한다.
