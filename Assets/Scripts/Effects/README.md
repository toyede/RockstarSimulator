# Local Screen Effects

화면의 특정 영역에 디스토션·색 분리·픽셀화·컬러 플래시를 재생하는 공용 시스템이다.
`ScreenEffectSystem`은 처음 호출될 때 자동 생성되며, 이펙트 오브젝트는 `PoolManager`로 재사용한다.

## 팀원 사용 순서

1. Project 창에서 `Create > Context Stage > Effects > Local Screen Effect Profile`을 만든다.
2. 프로필에서 `Effect Type`, 영역 모양, 효과별 값, 재생 곡선과 시간을 조절한다.
3. 필요한 코드에서 아래처럼 호출한다.

```csharp
[SerializeField] LocalScreenEffectProfile hitEffect;

void PlayHit(Vector3 worldPosition)
{
    ScreenEffects.Play(
        hitEffect,
        worldPosition,
        new Vector2(3f, 3f));
}
```

프로필 없이 링 디스토션을 바로 재생할 수도 있다.

```csharp
ScreenEffects.PlayDistortion(
    worldPosition,
    radius: 1.6f,
    strength: 0.04f,
    duration: 0.7f);
```

움직이는 대상 추적, 화면 픽셀 좌표 사용, 재생 중 제어도 지원한다.

```csharp
ScreenEffectHandle handle = ScreenEffects.PlayAttached(
    hitEffect,
    targetTransform,
    new Vector2(3f, 3f));

handle.SetStrength(1.5f);
handle.SetPosition(newWorldPosition); // 추적을 끊고 위치를 고정한다
handle.Stop();

ScreenEffects.PlayScreen(
    hitEffect,
    Input.mousePosition,
    radiusPixels: 160f);
```

## Mosh Special Hit

`SpecialAudienceCrowdActor`가 `SpecialHitLanded` 이벤트를 구독한다.
요구 타입이 Mosh일 때 움직이는 특별 관객 스프라이트의 현재 중심 위치에 효과를 재생한다.

- `Mosh Special Hit Effect`가 비어 있으면 기본 링 디스토션을 사용한다.
- 프로필을 연결하면 통합 셰이더의 다른 효과로도 교체할 수 있다.
- 크기, 강도 배율, 시간 덮어쓰기는 특별 관객 프리팹의 해당 섹션에서 조절한다.

## Card Impact

`CardPresentationStarted`를 기준으로 다음 표시 순서를 만든다.

`카드색 UI 픽셀 트레일 → 무대 중앙 임팩트 → 0.05초 뒤 관객 반응 → 점수/콤보`

- `CardPixelTrail`은 Screen Space Canvas 안에서 네모 픽셀 메시를 그리며 카드 풀 수명 동안 재사용한다.
- `CardImpactVFX`는 흰 플래시, 8~12개 방사 픽셀, 0.3→1.5 픽셀 링을 재사용한다.
- 임팩트 색은 Chill=청록, Singalong=보라, Mosh=주황·빨강, Utility=흰색·노랑 계열이다.
- 씬의 Bloom 값을 덮어쓰지 않고 현재 강도에 0.35를 짧게 더한 뒤 원래 값으로 복구한다.
- 모든 타이밍은 unscaled delta를 사용하며 `Time.timeScale` Hit Stop은 사용하지 않는다.

## 렌더러 전제

- URP 2D Renderer의 Camera Sorting Layer Texture가 켜져 있어야 한다.
- 캡처 경계 뒤에 `Effects` Sorting Layer가 있어야 한다.
- 현재 캡처 경계는 `Default`이므로 그 레이어까지의 월드 스프라이트가 효과에 포함된다.
- Screen Space Overlay Canvas는 캡처 대상이 아니므로 왜곡되지 않는다.

---

# UI 블러 배경 (팝업 뒤)

일시정지·클리어·게임오버 창 뒤에 깔리는 블러. 위 `ScreenEffectSystem`과 무관한
별도 경로다 (`Scripts/UI/UIBlurBackdrop.cs` + `Shaders/UIKawaseBlur.shader`).

```
Tools/UI/Setup Blur Backdrop
```

## 왜 실시간 블러가 아닌가

이런 창은 **뒷화면이 멈춰 있는 게 정상**이라 매 프레임 블러를 돌릴 이유가 없다.
팝업이 열리는 순간 화면을 **한 번** 잡아 흐리게 만들고 그 결과를 계속 보여준다.
이후 프레임 비용이 0이고, URP Renderer Feature 도 `_CameraOpaqueTexture` 도 필요 없다.

| 단계 | 내용 |
|---|---|
| 트리거 | `UIPopup.Open()` 이 루트를 SetActive → 자식인 배경의 `OnEnable` 이 같은 프레임에 뜬다 |
| 캡처 | `WaitForEndOfFrame` 뒤 `ScreenCapture.CaptureScreenshotIntoRenderTexture` — **HUD 포함 화면 전체** |
| 블러 | 1/downscale 로 축소 → Kawase 패스를 오프셋을 키우며 반복 |
| 표시 | `RawImage` 에 꽂고 unscaled 시간으로 페이드 인 (timeScale 0 에서도 동작) |

## 조절

| 값 | 위치 | 효과 |
|---|---|---|
| `downscale` (4) | BlurBackdrop 프리팹 | 클수록 더 흐리고 더 싸다 |
| `blurPasses` (4) | 〃 | 패스마다 오프셋이 커져 흐림이 빠르게 번진다 |
| `pixelatedFilter` (켬) | 〃 | 축소 픽셀 격자를 살려 도트 느낌을 남긴다 |
| 어둡기 | `RawImage.color` | 셰이더 수정 없이 회색으로 낮추면 어두워진다 |
| `flip` (자동) | 〃 | 화면이 상하 반전돼 보이면 강제로 바꾼다 |

## 알아둘 것 두 가지

1. **캡처 프레임의 팝업 알파** — 배경 코루틴은 페이드가 시작되기 전에 돌기 시작하므로
   알파를 0으로 눌러 둔다. 그래도 `UIPopup` 이 같은 프레임에 한 스텝(기본 약 0.1)을
   올려 버리는데, 축소·블러를 거치면 보이지 않는다.
   `animDuration = 0` 인 팝업은 이 처리 덕분에 배경에 통째로 찍히지 않는다.
2. **셰이더 스트리핑** — 배경 프리팹이 `blurShader` 를 **직접 참조**하므로 빌드에 포함된다.
   `Shader.Find` 는 참조가 비었을 때의 폴백일 뿐이라
   `GraphicsSettings` 의 Always Included 에 넣지 않아도 된다.
   (참조를 지우고 Find 로만 쓸 거라면 반드시 등록할 것)

## 팀 공유 필요

셋업이 `PausePopup.prefab` 과 `ScoreCanvas.prefab` **팀원 소유 프리팹을 수정한다.**
새 팝업이 생기면 `UIBlurBackdropSetup.TargetPrefabs` 에 경로를 추가한다.
