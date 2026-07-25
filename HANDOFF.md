# CONTEXT STAGE 인수인계 (Unity 게임잼)

아래 내용을 새 세션에 그대로 붙여넣으면 됩니다.

---

## 프로젝트

- 경로: `E:\UnityProjects\RockstarSimulator`
- Unity **6000.3.20f1** / URP 2D / 픽셀아트
- 게임: 관객의 옷차림(성향)과 움직임(신남 정도)을 읽고 카드를 골라 공연을 성공시키는 카드 게임
- 팀 게임잼. 나는 **사운드 담당**으로 시작했지만 실제로는 연출·UI 전반(사운드/관객 비주얼/조명/셰이더/콤보 HUD/튜토리얼/캐릭터 애니메이션)을 맡았다
- 현재 브랜치 `main`, 워킹트리 클린. 빌드 씬은 `Title` → `Main`

## 반드시 지켜야 할 작업 규칙 (중요)

### 1. C# 수정 후에는 Roslyn으로 직접 컴파일 검증한다
unity-mcp가 붙지 않는 세션이 많아서, 검증 없이 넘기면 사용자가 에디터에서 에러를 발견해 되돌아온다.

```bash
CSC="/e/UnityEditor/6000.3.20f1/Editor/Data/DotNetSdkRoslyn/csc.dll"

python Tools/make_rsp.py runtime && dotnet "$CSC" "@Tools/verify-runtime.rsp" 2>&1 | grep -c "error CS"
python Tools/make_rsp.py editor  && dotnet "$CSC" "@Tools/verify-editor.rsp"  2>&1 | grep -c "error CS"
```

`make_rsp.py`는 Unity가 마지막에 쓴 `Library/Bee/artifacts/*.dag/Assembly-CSharp.rsp`(+`-Editor.rsp`)에서
**참조·define만 재사용하고 `"Assets/..."` 소스 목록은 현재 디스크 상태로 교체**한다.
(rsp가 stale해서 새 파일이 빠져 있는 경우가 있음)
에디터 어셈블리 검증 시 `-r:` 의 `Assembly-CSharp.ref.dll` / `.dll`을 방금 만든 런타임 산출물로 바꿔치기해야 한다.
안 그러면 "새 타입이 없다"는 가짜 에러가 난다.

> 주의: heredoc(`<<'PY'`)으로 파이썬을 넘기면 백슬래시가 먹히므로 Write 도구로 파일을 만들어 실행할 것.

### 2. Unity 컴포넌트에 `??` 연산자를 쓰지 않는다
`UnityEngine.Object`는 `==`만 오버라이드하고 `??`는 순수 참조 비교라, 네이티브가 파괴된 fake-null이
그대로 통과해 `MissingComponentException`이 난다. 프로젝트 전역에서 이미 10곳을 고쳤다.

```csharp
// 금지: go.GetComponent<T>() ?? Undo.AddComponent<T>(go)
// 사용: EditorSetupUtility.EnsureComponent<T>(go)
```

### 3. 셋업 메뉴 실행 후 반드시 `Ctrl+S`
이 프로젝트에서 반복적으로 사고가 났다. 무음 버그, 조명 NullReference, 드롭 판정 미작동이
**전부 씬 저장 누락** 때문이었다. 씬 오브젝트는 저장 안 하면 다음 실행에 사라진다.

### 4. 셰이더는 Always Included에 등록해야 빌드에 들어간다
런타임 `Shader.Find`로만 로드하는 셰이더는 빌드에서 스트리핑된다.
`ProjectSettings/GraphicsSettings.asset`의 `m_AlwaysIncludedShaders`에 4개가 등록돼 있다
(`PixelDissolveUI`, `PartialDistortion`, `SpecialAudienceOutline`, `PixelSpotlight2D`).
셰이더를 새로 추가하면 여기에도 넣어야 한다.

### 5. 팀원 코드는 EventBus 구독으로만 붙인다
카드·점수·관객 시스템은 다른 담당자 소유다. 직접 수정하지 않고
`GameJamKit/Core/GameEvents.cs`의 이벤트를 구독하는 방식으로 연결해 왔다.
불가피하게 수정했다면 담당자에게 공유해야 한다.

## 구조 요약

기반은 자체 스니펫 킷 `Assets/GameJamKit`(외부 패키지 없음):
`MonoSingleton<T>` / `GameManager`(상태머신+점수) / `EventBus`(타입 기반) / `PoolManager` /
`AudioManager` / `Save`(PlayerPrefs) / `UIManager`+`UIPopup` / `CameraShake`.

핵심 데이터 흐름:

```
HypeSystem(열기 0~100)
  └ HypeConfig.ResolveStage() → HeatStage 3단계 (Chill / Singalong / Mosh)
       ├→ 카드 판정 (CardEffectResolver)
       ├→ 조명 (StageLightEventBridge)
       ├→ 관객 앰비언스 (CrowdAmbienceSystem)
       └→ 관객 비주얼
```

**`HeatStage`는 단일 출처다.** 같은 의미의 enum을 새로 만들지 말 것
(내가 만든 `SpecialAudienceRequestType`을 카드팀의 `HeatStage`로 통합해 폐기한 이력 있음).
단계 판정은 전부 `HypeConfig.ResolveStage()` 하나를 쓰므로 시스템 간 어긋날 수 없다.

## 내가 만든 시스템 (전부 `Main.unity`에 배치·동작 확인됨)

| 폴더 | 내용 |
|---|---|
| `Scripts/Audio/` | 열기 단계별 관객 앰비언스 크로스페이드, BGM(`big_rock`), 카드 효과음(카드별 override 표), 볼륨 4채널 |
| `Scripts/Crowd/` | `CrowdMotionEvaluator`(모션 공식 단일 출처), `CrowdMoodDirector`, `CrowdSpawner`, `CrowdMemberView` |
| `Scripts/Lighting/` | `StageLightController`(단계별 색·강도·펄스·Special Hit 플래시), `PixelSpotlight2D` |
| `Scripts/SpecialAudience/` | 특별 관객(Chill/Singalong/Mosh 요구), 드롭 판정, 무대 위 액터 |
| `Scripts/Effects/` | `CardDissolveEffect` + `Shaders/PixelDissolveUI.shader` (픽셀 디졸브) |
| `Scripts/Common/` | `SpriteAnimation`/`SpriteSheetAnimator`(Animator 미사용 프레임 애니메이션), `HypeTierUtil` |
| `Scripts/Characters/` | `RaccoonAnimator`(Idle/Stroke/GuitarSolo, 끝나면 Idle 복귀) |
| `Scripts/UI/` | `ScoreRankUI`(랭크+목표 게이지), `TitleReturn`, `ScoreUI` |
| `Scripts/Combo/` | `CardTotalTextUI`, `CardReactionTextUI`(LOVE IT!/BORED 등) |
| `Scripts/Tutorial/` | `TutorialFlow`(스킵 가능 미니 공연), `TutorialOverlayUI`, `TutorialTips` |

각 폴더에 `README.md`가 있다. **작업 전 해당 README를 먼저 읽으면 배경을 빠르게 잡을 수 있다.**

## 셋업 메뉴 (씬 배치는 손으로 하지 않는다)

```
Tools/Hype/Setup Hype Scene
Tools/Cards/Setup Prefab Card System
Tools/Audio/Setup Crowd Ambience
Tools/Crowd/Setup Crowd Scene
Tools/Special Audience/Setup Special Audience
Tools/Audience/Create Special Audience Prefab
Tools/Lighting/Setup Stage Lighting
Tools/Lighting/Fix Dark Stage Lighting      ← 화면 어두울 때 복구
Tools/UI/Setup Score Rank HUD
Tools/Combo/Setup Combo Prototype
Tools/Tutorial/Setup Tutorial
Tools/Art/Fix Character Sprite Import  →  Setup Raccoon And Friends
Tools/Project/Apply DungGeunMo Font To Scene / To Prefabs
```

전부 **이미 있는 것은 덮어쓰지 않는다**(여러 번 실행해도 안전). 실행 후 `Ctrl+S`.

## 현재 미해결 / 판단 필요

1. **관객 스프라이트 색조** — `AudienceMember.prefab`에 `chillColor`(시안)/`singalongColor`(보라)가
   박혀 있어 성향별로 스프라이트가 물든다. 관객 담당자가 성향 구분용으로 의도한 것일 수 있어
   손대지 않았다. 원래 색으로 보려면 프리팹에서 흰색으로 바꾸면 된다.
   (조명 쪽은 `globalTintStrength = 0.3`으로 이미 완화해 뒀다)

2. **`Sprites/Crowd/Animated` 임포트 문제** — `spriteMode: Multiple`로 들어와 프레임당 14개
   쓰레기 조각으로 잘려 있다. 관객 애니메이션이 정상 동작이 아닐 수 있다.
   `CharacterAnimationSetup.ImportFolders` 표에 폴더를 추가하면 같은 방식으로 교정된다.
   (사용자가 "추후 수정"으로 보류한 항목)

3. **AI 워터마크** — `Sprites/Raccoon` / `Sprites/Friends` 좌상단에 생성 워터마크가 박혀 있고
   스프라이트가 캔버스 전체라 **게임 화면에 그대로 노출된다.** 아트 담당 크롭 필요.

4. **매니저 DontDestroyOnLoad 미적용** — 킷 매니저들이 `[Managers]`의 자식으로 배치돼 있어서
   `MonoSingleton.Awake`의 `if (Persistent && transform.parent == null)` 조건 때문에
   `DontDestroyOnLoad`가 걸리지 않는다. 킷 주석은 지속된다고 전제하므로 불일치가 있다.
   씬 전환 시 상태가 어정쩡하게 남는 문제의 근원. `TitleReturn`으로 우회해 뒀다.

5. **타이틀 복귀 크래시** — 사용자가 보고했으나 `Player.log`에 크래시 순간이 없었다
   (두 로그 모두 정상 종료로 끝남). 로그로 증명된 결함(씬 언로드 중 풀 반납 경고 폭주)은 제거했고
   `TitleReturn`으로 정리 순서를 잡았다. **재발 시 크래시 직후의 로그가 필요하다:**
   `C:\Users\FOR\AppData\LocalLow\DefaultCompany\Rockstar_Simulator\Player.log`

## 사용자 스타일

- 한국어로 소통. 간결한 결론 + 근거를 원한다
- "인스펙터에서 조절 가능하게" 를 선호한다. 수치를 코드에 박지 말고 SerializeField로 노출할 것
- 확장성을 자주 요구한다 (티어/카드/상태를 리스트로 만들어 코드 수정 없이 늘릴 수 있게)
- 조사 결과와 실제 문제를 솔직하게 말하는 것을 신뢰한다. 재현 못 한 건 단정하지 말 것
- 제출 기한이 임박한 게임잼이다. 새 기능보다 **이미 만든 것이 실제로 화면에 보이는지**가 우선

## 첫 요청 예시

이 문서를 붙인 뒤 이렇게 이어가면 된다:

> 위 인수인계를 읽고, 먼저 `git log --oneline -5`와 `git status`로 현재 상태를 확인해줘.
> 그 다음 [작업 내용]을 진행해줘.
