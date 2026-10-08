using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ContextStage
{
    /// <summary>적용이 끝난 Run의 변경 알림만 읽는다. 덱·증강·후보·리롤은 수정하지 않는다.</summary>
    [DisallowMultipleComponent]
    public sealed class DeckAcquisitionFeedback : MonoBehaviour
    {
        TourRunManager _manager;
        TourRunState _run;
        readonly HashSet<string> _seen = new HashSet<string>();
        Canvas _canvas;
        CanvasGroup _group;
        RectTransform _toast;
        Image _art;
        Text _label;
        Font _font;
        float _remaining;

        public void ConfigureFont(Font font) { if (font != null) _font = font; }
        void OnEnable()
        {
            _manager = GetComponent<TourRunManager>();
            if (_manager == null) return;
            _manager.StateChanged += OnRunChanged;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
            Observe(_manager.CurrentRun, false); // restored run is not a new acquisition
        }
        void OnDisable()
        {
            if (_manager != null) _manager.StateChanged -= OnRunChanged;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
            Clear();
        }
        void OnRunChanged() => Observe(_manager.CurrentRun, true);
        void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode) => Clear();
        void Observe(TourRunState run, bool notify)
        {
            if (!ReferenceEquals(_run, run)) { _run = run; _seen.Clear(); Clear(); notify = false; }
            if (run?.deck?.addedCards == null) return;
            foreach (var card in run.deck.addedCards)
            {
                if (card == null || string.IsNullOrEmpty(card.instanceId) || !_seen.Add(card.instanceId)) continue;
                if (notify && run.phase == RunPhase.Travel && !string.IsNullOrEmpty(card.sourceAugmentId)) Show(card.cardId);
            }
        }
        void Show(string cardId)
        {
            var catalog = CardCatalog.LoadDefault();
            if (catalog == null || !catalog.TryGetCard(cardId, out var card)) return;
            if (_canvas == null)
            {
                _canvas = TourPrototypeUIFactory.CreateCanvas("DeckAcquisitionToast", 180);
                _canvas.transform.SetParent(transform, false);
                // purely decorative: never covers map clicks or blocks the selection pipeline
                var raycaster = _canvas.GetComponent<GraphicRaycaster>(); if (raycaster != null) raycaster.enabled = false;
                var go = new GameObject("AcquiredCard", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
                go.transform.SetParent(_canvas.transform, false);
                _toast = (RectTransform)go.transform;
                _toast.anchorMin = _toast.anchorMax = new Vector2(0.5f, 0);
                _toast.pivot = new Vector2(0.5f, 0);
                _toast.anchoredPosition = new Vector2(0, 80); _toast.sizeDelta = new Vector2(650, 110);
                var bg = go.GetComponent<Image>(); bg.color = new Color32(0x2e, 0x22, 0x2f, 235); bg.raycastTarget = false;
                _group = go.GetComponent<CanvasGroup>(); _group.blocksRaycasts = false; _group.interactable = false;
                var art = new GameObject("CardArt", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                art.transform.SetParent(_toast, false); _art = art.GetComponent<Image>(); _art.raycastTarget = false; _art.preserveAspect = true;
                TourPrototypeUIFactory.SetRect(_art.rectTransform, new Vector2(-260, 0), new Vector2(65, 95));
                _label = TourPrototypeUIFactory.CreateText(_toast, "AcquiredLabel", 25, TextAnchor.MiddleCenter, new Color32(0xf9, 0xc2, 0x2b, 255));
                TourPrototypeUIFactory.SetRect(_label.rectTransform, new Vector2(40, 0), new Vector2(510, 100));
                _label.resizeTextForBestFit = true; _label.resizeTextMinSize = 18; _label.resizeTextMaxSize = 25;
            }
            if (_font != null) _label.font = _font;
            _label.text = card.DisplayName + "\n덱에 추가됨";
            _art.sprite = card.Artwork;
            _remaining = 1.1f; _group.alpha = 1; _canvas.gameObject.SetActive(true);
            var pixels = _canvas.GetComponent<UIPixelBurstEmitter>();
            if (pixels == null) pixels = _canvas.gameObject.AddComponent<UIPixelBurstEmitter>();
            pixels.EmitBurst(_art.rectTransform, new Color32(0xf9, 0xc2, 0x2b, 255), 12, 35, 85, 3, 5, 0.25f, 0.4f);
        }
        void Update()
        {
            if (_remaining <= 0 || _canvas == null) return;
            _remaining = Mathf.Max(0, _remaining - Time.unscaledDeltaTime);
            float age = 1.1f - _remaining;
            _art.transform.localScale = Vector3.one * (1 + 0.08f * Mathf.Sin(Mathf.Clamp01(age / 0.3f) * Mathf.PI));
            _group.alpha = Mathf.Clamp01(age / 0.12f) * Mathf.Clamp01(_remaining / 0.2f);
            if (_remaining <= 0) Clear();
        }
        void Clear() { _remaining = 0; if (_canvas != null) _canvas.gameObject.SetActive(false); }
    }
}
