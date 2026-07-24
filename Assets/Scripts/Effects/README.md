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

## 렌더러 전제

- URP 2D Renderer의 Camera Sorting Layer Texture가 켜져 있어야 한다.
- 캡처 경계 뒤에 `Effects` Sorting Layer가 있어야 한다.
- 현재 캡처 경계는 `Default`이므로 그 레이어까지의 월드 스프라이트가 효과에 포함된다.
- Screen Space Overlay Canvas는 캡처 대상이 아니므로 왜곡되지 않는다.
