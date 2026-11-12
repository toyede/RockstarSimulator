using System;
using System.Collections;
using System.Collections.Generic;
using GameJamKit;
using UnityEngine;
using UnityEngine.Networking;

namespace ContextStage
{
    // Opt-in, numeric summaries only. No nicknames, free text, device IDs or cross-run identifier.
    internal static class PlaytestMetrics
    {
        const int QueueLimit = 30;
        static Sample _stage, _tutorial;
        static float _tutorialStart;
        static bool _uploading;
        static bool? _remoteConfigured;

        [Serializable] sealed class Sample
        {
            public string id, kind, stage, outcome;
            public int chill, singalong, mosh, requestSpecials, aimedSpecials, hits;
            public int openingPlays, openingPositive, blocked, durationSeconds;
        }
        [Serializable] sealed class Queue { public List<Sample> items = new(); }
        [Serializable] sealed class RequestBody { public Sample p_sample; }

        // A local-only opt-in does not grant consent to upload those samples in a later build.
        static string Consent => RemoteConfigured ? "playtest_metrics_remote_consent_v1" : "playtest_metrics_local_consent_v1";
        static string QueueKey => RemoteConfigured ? "playtest_metrics_remote_queue_v1" : "playtest_metrics_local_queue_v1";
        public static bool Enabled => Save.GetBool(Consent, true);
        public static bool RemoteConfigured => _remoteConfigured ??=
            Resources.Load<TextAsset>("Playtest/EnableRemoteMetrics") != null;
        static bool Recording => Enabled && !Application.isEditor && !Debug.isDebugBuild;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetSession()
        {
            _stage = _tutorial = null;
            _uploading = false;
            _remoteConfigured = null;
        }

        public static void SetEnabled(bool value)
        {
            Save.SetBool(Consent, value);
            if (!value)
            {
                _stage = _tutorial = null;
                Save.Delete(QueueKey);
            }
            Save.Flush();
            if (value) TryUpload();
        }

        public static void StartTutorial(string stage)
        {
            if (!Recording) return;
            _tutorial = NewSample("tutorial", stage);
            _tutorialStart = Time.realtimeSinceStartup;
        }

        public static void RecordTutorialBlock()
        {
            if (_tutorial != null) Increment(ref _tutorial.blocked);
        }

        public static void EndTutorial(string outcome)
        {
            if (_tutorial == null) return;
            Sample sample = _tutorial;
            _tutorial = null;
            sample.durationSeconds = Mathf.Clamp(Mathf.RoundToInt(Time.realtimeSinceStartup - _tutorialStart), 0, 3600);
            Enqueue(sample, outcome);
        }

        public static void RecordCard(CrowdPreference preference, CardRole role, bool requestActive, bool aimed, bool hit, int score)
        {
            if (!Recording || TutorialFlow.IsRunning || role == CardRole.Utility) return;
            string stage = StageRuntimeDirector.CurrentStage?.StageId;
            if (string.IsNullOrEmpty(stage)) return;
            if (_stage == null || _stage.stage != stage) _stage = NewSample("performance", stage);
            switch (preference)
            {
                case CrowdPreference.Chill: Increment(ref _stage.chill); break;
                case CrowdPreference.Singalong: Increment(ref _stage.singalong); break;
                case CrowdPreference.Mosh: Increment(ref _stage.mosh); break;
            }
            if (role == CardRole.Special && requestActive)
            {
                Increment(ref _stage.requestSpecials);
                if (aimed) Increment(ref _stage.aimedSpecials);
                if (hit) Increment(ref _stage.hits);
            }
            if (_stage.openingPlays < 3)
            {
                _stage.openingPlays++;
                if (score > 0) _stage.openingPositive++;
            }
        }

        public static void EndStage(string outcome, string stage = null)
        {
            if (!Recording || _tutorial != null) return;
            Sample sample = _stage;
            _stage = null;
            if (sample == null && string.IsNullOrEmpty(stage)) return;
            if (sample == null || (stage != null && sample.stage != stage))
                sample = NewSample("performance", stage);
            Enqueue(sample, outcome);
        }

        static void Increment(ref int value) => value = Mathf.Min(value + 1, 3000);
        static Sample NewSample(string kind, string stage) => new()
        {
            id = Guid.NewGuid().ToString("D"), kind = kind, stage = stage
        };

        static void Enqueue(Sample sample, string outcome)
        {
            if (!Recording) return;
            sample.outcome = outcome;
            var queue = Save.GetObject<Queue>(QueueKey) ?? new Queue();
            if (queue.items.Count >= QueueLimit) queue.items.RemoveAt(0);
            queue.items.Add(sample);
            Save.SetObject(QueueKey, queue);
            TryUpload();
        }

        static void TryUpload()
        {
            if (!Recording || _uploading || !RemoteConfigured || !RemoteLeaderboardClient.IsConfigured) return;
            _uploading = true;
            TimerRunner.Instance.StartCoroutine(Upload());
        }

        static IEnumerator Upload()
        {
            try
            {
                while (Recording)
                {
                    var queue = Save.GetObject<Queue>(QueueKey);
                    if (queue?.items == null || queue.items.Count == 0) yield break;
                    Sample next = queue.items[0];
                    using var req = RemoteLeaderboardClient.CreatePlaytestRequest(JsonUtility.ToJson(new RequestBody { p_sample = next }));
                    var op = req.SendWebRequest();
                    while (!op.isDone && Recording) yield return null;
                    if (!Recording) { req.Abort(); yield break; }
                    if (req.result != UnityWebRequest.Result.Success) yield break;
                    queue = Save.GetObject<Queue>(QueueKey);
                    queue?.items?.RemoveAll(item => item.id == next.id);
                    Save.SetObject(QueueKey, queue);
                }
            }
            finally { _uploading = false; }
        }
    }
}
