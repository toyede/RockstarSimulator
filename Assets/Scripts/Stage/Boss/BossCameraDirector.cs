using System.Collections;
using UnityEngine;

namespace ContextStage
{
    /// <summary>
    /// 보스전 카메라. Main Camera 의 x 만 구역 사이로 옮긴다 (y·크기 고정, SmoothStep).
    /// 자유 스크롤은 없다. 연출 담당(BossStagePresentation)만 호출하고, 해제·게임 종료 때는 즉시 제자리로 돌아온다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BossCameraDirector : MonoBehaviour
    {
        [SerializeField, Tooltip("비우면 Camera.main")] Camera targetCamera;

        BossArenaLayout _arena;
        Coroutine _routine;

        public BossZone CurrentZone { get; private set; } = BossZone.OurStage;
        public bool IsMoving => _routine != null;

        Camera Cam => targetCamera != null ? targetCamera : Camera.main;

        void Awake() => _arena = GetComponent<BossArenaLayout>();

        /// <summary>즉시 우리 무대로. 룰 해제·공연 종료 때 호출된다.</summary>
        public void SnapHome()
        {
            if (_routine != null)
            {
                StopCoroutine(_routine);
                _routine = null;
            }
            MoveTo(BossZone.OurStage);
        }

        /// <summary>구역으로 팬. 이미 이동 중이면 그 이동을 끊고 현재 위치에서 이어간다.</summary>
        public Coroutine PanTo(BossZone zone, float duration)
        {
            if (_routine != null) StopCoroutine(_routine);
            _routine = StartCoroutine(PanRoutine(zone, duration));
            return _routine;
        }

        /// <summary>시퀀스 안에서 yield 하기 위한 코루틴. 끝나면 CurrentZone 이 바뀐다.</summary>
        public IEnumerator PanRoutine(BossZone zone, float duration)
        {
            Camera cam = Cam;
            if (cam == null || _arena == null)
            {
                CurrentZone = zone;
                _routine = null;
                yield break;
            }

            Vector3 from = cam.transform.position;
            Vector3 anchor = _arena.AnchorOf(zone);
            Vector3 to = new Vector3(anchor.x, from.y, from.z);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = duration <= 0f ? 1f : Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                cam.transform.position = Vector3.Lerp(from, to, t);
                yield return null;
            }
            cam.transform.position = to;
            CurrentZone = zone;
            _routine = null;
        }

        void MoveTo(BossZone zone)
        {
            Camera cam = Cam;
            if (cam != null && _arena != null && _arena.IsBuilt)
            {
                Vector3 anchor = _arena.AnchorOf(zone);
                cam.transform.position = new Vector3(anchor.x, cam.transform.position.y, cam.transform.position.z);
            }
            CurrentZone = zone;
        }

        void OnDisable() => SnapHome();
    }
}
