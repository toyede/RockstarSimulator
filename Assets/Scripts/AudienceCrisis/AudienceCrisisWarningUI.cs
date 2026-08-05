using System.Collections;
using GameJamKit;
using UnityEngine;
using UnityEngine.UI;

namespace ContextStage
{
    [DisallowMultipleComponent]
    public sealed class AudienceCrisisWarningUI : MonoBehaviour
    {
        [SerializeField] GameObject overlayRoot;
        [SerializeField] Text titleText;
        [SerializeField] Text descriptionText;
        [SerializeField] Text countdownText;
        [SerializeField] Text progressText;
        [SerializeField, Min(0f)] float resultHoldDuration = 1.35f;

        Coroutine _hideRoutine;

        void OnEnable()
        {
            EventBus.Subscribe<AudienceCrisisWarningStarted>(
                OnWarningStarted);
            EventBus.Subscribe<AudienceCrisisProgressChanged>(
                OnProgressChanged);
            EventBus.Subscribe<AudienceCrisisDepartureStarted>(
                OnDepartureStarted);
            EventBus.Subscribe<AudienceCrisisResolved>(
                OnResolved);
            EventBus.Subscribe<AudienceCrisisCancelled>(
                OnCancelled);
            EventBus.Subscribe<AudienceComebackStarted>(
                OnComebackStarted);
            EventBus.Subscribe<AudienceComebackResolved>(
                OnComebackResolved);
            HideImmediate();
        }

        void OnDisable()
        {
            EventBus.Unsubscribe<AudienceCrisisWarningStarted>(
                OnWarningStarted);
            EventBus.Unsubscribe<AudienceCrisisProgressChanged>(
                OnProgressChanged);
            EventBus.Unsubscribe<AudienceCrisisDepartureStarted>(
                OnDepartureStarted);
            EventBus.Unsubscribe<AudienceCrisisResolved>(
                OnResolved);
            EventBus.Unsubscribe<AudienceCrisisCancelled>(
                OnCancelled);
            EventBus.Unsubscribe<AudienceComebackStarted>(
                OnComebackStarted);
            EventBus.Unsubscribe<AudienceComebackResolved>(
                OnComebackResolved);
            if (_hideRoutine != null) StopCoroutine(_hideRoutine);
            _hideRoutine = null;
        }

        void OnWarningStarted(AudienceCrisisWarningStarted e)
        {
            if (!HasRequiredReferences()) return;
            if (_hideRoutine != null)
            {
                StopCoroutine(_hideRoutine);
                _hideRoutine = null;
            }

            overlayRoot.SetActive(true);
            titleText.text = "!  CROWD CRISIS  !";
            descriptionText.text =
                "A nearby concert is starting!\n" +
                $"Raise threatened fans to {Mathf.CeilToInt(e.RetentionEngagement)} " +
                "engagement or land a SPECIAL card.";
            countdownText.text = e.Duration.ToString("0.0");
            progressText.text =
                $"FANS SECURED  0 / {e.ThreatenedAudience.Length}";
        }

        void OnProgressChanged(AudienceCrisisProgressChanged e)
        {
            if (!HasRequiredReferences() ||
                !overlayRoot.activeSelf)
                return;

            countdownText.text = e.Remaining.ToString("0.0");
            progressText.text =
                $"FANS SECURED  {e.SecuredCount} / {e.ThreatenedCount}" +
                (e.SpecialSaveCount > 0
                    ? $"   SPECIAL SAVE +{e.SpecialSaveCount}"
                    : string.Empty);
        }

        void OnDepartureStarted(AudienceCrisisDepartureStarted e)
        {
            if (!HasRequiredReferences() ||
                !overlayRoot.activeSelf)
                return;

            countdownText.text = "0.0";
            titleText.text = e.DepartureCount > 0
                ? "THE CROWD IS LEAVING!"
                : "THE CROWD STAYS!";
        }

        void OnResolved(AudienceCrisisResolved e)
        {
            if (!HasRequiredReferences()) return;
            overlayRoot.SetActive(true);
            titleText.text = e.DepartedCount == 0
                ? "CROWD SECURED!"
                : "CROWD LOST";
            descriptionText.text = e.DepartedCount == 0
                ? "Your performance convinced every threatened fan to stay."
                : $"{e.DepartedCount} fan(s) left for the nearby concert.";
            countdownText.text = string.Empty;
            progressText.text =
                $"SECURED {e.RetainedCount} / THREATENED {e.ThreatenedCount}";

            if (_hideRoutine != null) StopCoroutine(_hideRoutine);
            _hideRoutine = StartCoroutine(HideAfterDelay());
        }

        IEnumerator HideAfterDelay()
        {
            if (resultHoldDuration > 0f)
                yield return new WaitForSeconds(resultHoldDuration);
            HideImmediate();
            _hideRoutine = null;
        }

        void OnCancelled(AudienceCrisisCancelled e) =>
            HideImmediate();

        void OnComebackStarted(AudienceComebackStarted e)
        {
            if (!HasRequiredReferences()) return;
            if (_hideRoutine != null)
            {
                StopCoroutine(_hideRoutine);
                _hideRoutine = null;
            }

            overlayRoot.SetActive(true);
            titleText.text = "GOOD NEWS!";
            descriptionText.text =
                "공연의 입소문을 듣고 새로운 관객이 찾아옵니다!";
            countdownText.text = "NEW FANS";
            progressText.text = "COMEBACK CHANCE";
        }

        void OnComebackResolved(AudienceComebackResolved e)
        {
            if (!HasRequiredReferences()) return;
            overlayRoot.SetActive(true);
            titleText.text = "THE CROWD GROWS!";
            descriptionText.text =
                $"새 관객 {e.JoinedCount}명이 입장했고 공연장의 호응이 회복됐습니다.";
            countdownText.text = string.Empty;
            progressText.text = $"ENGAGEMENT BOOST  {e.BoostedCount} FANS";

            if (_hideRoutine != null) StopCoroutine(_hideRoutine);
            _hideRoutine = StartCoroutine(HideAfterDelay());
        }

        void HideImmediate()
        {
            if (overlayRoot != null)
                overlayRoot.SetActive(false);
        }

        bool HasRequiredReferences()
        {
            return overlayRoot != null &&
                   titleText != null &&
                   descriptionText != null &&
                   countdownText != null &&
                   progressText != null;
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            resultHoldDuration = Mathf.Max(
                0f,
                resultHoldDuration);
        }
#endif
    }
}
