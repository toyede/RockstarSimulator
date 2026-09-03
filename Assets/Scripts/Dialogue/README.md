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
Tools/Dialogue/Setup Dialogue Data                  스타일·시퀀스 6개(인트로 5 + 공통 엔딩)·카탈로그 생성 (있으면 유지)
Tools/Dialogue/Rewrite Default Sequences (Overwrite) 대사를 기획서 초안으로 되돌림
Tools/Dialogue/Create Dialogue Panel Prefab          Resources/Dialogue/DialoguePanel.prefab 생성
```

## 피그마 시안 적용

`Resources/Dialogue/DialoguePanel.prefab` 의 배치와 `DialogueStyle.asset` 의 색·폰트·화살표 스프라이트만 바꾸면 된다.
코드는 오브젝트 참조(`DialoguePanel` 인스펙터)만 알고 있다. 초상화는 `DialogueLine.portrait` 에 스프라이트를 넣으면 좌/우에 뜬다.

- 화살표: `DialogueStyle.arrowSprite` 가 비어 있으면 12×8 픽셀 삼각형을 런타임에 만든다. `arrowStepMotion` 을 켜면 계단식으로 튄다.
- 타이핑: `typingInterval`(자모당 간격), `punctuationDelay`(문장 부호 뒤 정지), `typingSoundId`(SoundLibrary ID).
- DungGeunMo 에는 `▶` 글리프가 없다. 특수 기호는 ASCII(`>>`)를 쓰거나 폰트 폴백을 추가할 것.
