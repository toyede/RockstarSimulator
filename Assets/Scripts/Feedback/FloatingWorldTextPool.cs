using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace ContextStage
{
    /// <summary>
    /// <see cref="FloatingWorldText"/> 인스턴스를 만들어 돌려쓰는 작은 풀.
    ///
    /// GameJamKit 의 PoolManager 는 프리팹 에셋을 요구하지만, 이 텍스트는 스프라이트도
    /// 자식 구조도 없어서 프리팹으로 관리할 이유가 없다. 그래서 런타임에 직접 만든다 —
    /// 셋업 메뉴가 텍스트 프리팹을 생성·연결할 필요가 없고, 씬 저장 누락 사고도 없다.
    ///
    /// 전부 재생 중이면 <b>가장 먼저 시작한 것</b>을 뺏어 쓴다.
    /// 관객이 한꺼번에 빠져나가도 최신 텍스트가 밀려나지 않는다.
    /// </summary>
    public sealed class FloatingWorldTextPool
    {
        readonly List<FloatingWorldText> _instances;
        readonly Transform _parent;
        readonly TMP_FontAsset _font;
        readonly string _sortingLayer;
        readonly FloatingWorldTextStyle _style;
        readonly int _capacity;
        readonly string _instanceName;

        public FloatingWorldTextPool(
            Transform parent,
            TMP_FontAsset font,
            string sortingLayer,
            FloatingWorldTextStyle style,
            int capacity,
            string instanceName = "FloatingText")
        {
            _parent = parent;
            _font = font;
            _sortingLayer = sortingLayer;
            _style = style.Sanitized();
            _capacity = Mathf.Max(1, capacity);
            _instanceName = instanceName;
            _instances = new List<FloatingWorldText>(_capacity);
        }

        public FloatingWorldText Play(
            string text,
            Color color,
            Vector3 localPosition,
            int sortingOrder)
        {
            FloatingWorldText instance = Rent();
            if (instance == null) return null;

            instance.Play(text, color, localPosition, sortingOrder);
            return instance;
        }

        FloatingWorldText Rent()
        {
            if (_parent == null) return null;

            // 1) 쉬고 있는 것 먼저
            for (int i = 0; i < _instances.Count; i++)
            {
                FloatingWorldText instance = _instances[i];
                if (instance == null) continue;
                if (!instance.IsPlaying) return instance;
            }

            // 2) 아직 여유가 있으면 새로 만든다
            if (_instances.Count < _capacity) return Create();

            // 3) 전부 재생 중이면 가장 오래된 것을 재사용
            FloatingWorldText oldest = null;
            float oldestTime = float.PositiveInfinity;
            for (int i = 0; i < _instances.Count; i++)
            {
                FloatingWorldText instance = _instances[i];
                if (instance == null) continue;
                if (instance.StartedAt >= oldestTime) continue;

                oldest = instance;
                oldestTime = instance.StartedAt;
            }

            return oldest != null ? oldest : Create();
        }

        FloatingWorldText Create()
        {
            var go = new GameObject($"{_instanceName}_{_instances.Count:00}");
            go.transform.SetParent(_parent, worldPositionStays: false);

            var instance = go.AddComponent<FloatingWorldText>();
            instance.Initialize(_font, _sortingLayer, _style);
            _instances.Add(instance);
            return instance;
        }

        public void StopAll()
        {
            for (int i = 0; i < _instances.Count; i++)
                if (_instances[i] != null) _instances[i].Stop();
        }
    }
}
