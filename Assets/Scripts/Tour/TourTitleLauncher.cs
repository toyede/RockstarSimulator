using System;
using System.Collections.Generic;
using GameJamKit;
using UnityEngine;

namespace ContextStage
{
    /// <summary>타이틀의 기존 시작 버튼을 투어 시작점으로 연결한다.</summary>
    public sealed class TourTitleLauncher : MonoBehaviour
    {
        [SerializeField] private List<StageDefinition> stages = new List<StageDefinition>();
        [SerializeField] private string tourHubSceneName = "TourHub";

        public void Configure(IReadOnlyList<StageDefinition> stageDefinitions, string hubSceneName)
        {
            stages.Clear();
            if (stageDefinitions != null)
            {
                for (int i = 0; i < stageDefinitions.Count; i++)
                    stages.Add(stageDefinitions[i]);
            }
            tourHubSceneName = string.IsNullOrWhiteSpace(hubSceneName) ? "TourHub" : hubSceneName;
        }

        public void StartTour()
        {
            if (SceneLoader.IsLoading) return;

            int seed = Environment.TickCount & int.MaxValue;
            TourRunManager manager = TourRunManager.Instance;
            if (manager == null || !manager.StartNewRun(stages, seed)) return;

            SceneLoader.Load(tourHubSceneName);
        }
    }
}
