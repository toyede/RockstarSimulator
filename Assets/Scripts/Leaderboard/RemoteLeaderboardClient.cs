using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace ContextStage
{
    /// <summary>
    /// Supabase REST 기반의 공유 리더보드 클라이언트.
    /// 네트워크 실패 여부는 호출자에게 반환하며 로컬 저장은 건드리지 않는다.
    /// </summary>
    public static class RemoteLeaderboardClient
    {
        const string ScoresEndpoint =
            "https://sktngzjtfffwtarkgxpo.supabase.co/rest/v1/raccoon_roll_scores";
        const string PublishableKey =
            "sb_publishable_JT3KbKwsZak8Ulry5EpJhw_nrYiqWWr";
        const int RequestTimeoutSeconds = 8;
        const int MaximumPlayerNameLength = 12;
        const int MaximumAcceptedScore = 1_000_000;

        public static bool IsConfigured =>
            !string.IsNullOrWhiteSpace(ScoresEndpoint) &&
            !string.IsNullOrWhiteSpace(PublishableKey);

        public static string NormalizePlayerName(string value)
        {
            string normalized = string.IsNullOrWhiteSpace(value)
                ? "Player"
                : value.Trim();
            normalized = normalized
                .Replace("<", string.Empty)
                .Replace(">", string.Empty)
                .Replace("\r", " ")
                .Replace("\n", " ");
            normalized = normalized.Trim();
            if (string.IsNullOrEmpty(normalized)) normalized = "Player";
            if (normalized.Length > MaximumPlayerNameLength)
                normalized = normalized.Substring(0, MaximumPlayerNameLength);
            return normalized;
        }

        public static IEnumerator SubmitScore(
            string playerName,
            int score,
            Action<bool> completed)
        {
            if (!IsConfigured)
            {
                completed?.Invoke(false);
                yield break;
            }

            var payload = new RemoteScoreSubmission
            {
                player_name = NormalizePlayerName(playerName),
                score = Mathf.Clamp(score, 0, MaximumAcceptedScore)
            };
            byte[] body = Encoding.UTF8.GetBytes(JsonUtility.ToJson(payload));

            using var request = new UnityWebRequest(
                ScoresEndpoint,
                UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = new UploadHandlerRaw(body),
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = RequestTimeoutSeconds
            };
            ConfigureRequest(request);
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Prefer", "return=minimal");

            yield return request.SendWebRequest();
            completed?.Invoke(request.result == UnityWebRequest.Result.Success);
        }

        public static IEnumerator GetTopScores(
            int limit,
            Action<bool, List<ScoreRecord>> completed)
        {
            if (!IsConfigured)
            {
                completed?.Invoke(false, null);
                yield break;
            }

            int safeLimit = Mathf.Clamp(limit, 1, 100);
            string url = ScoresEndpoint +
                "?select=player_name,score" +
                "&order=score.desc,created_at.asc" +
                $"&limit={safeLimit}";

            using var request = UnityWebRequest.Get(url);
            request.timeout = RequestTimeoutSeconds;
            ConfigureRequest(request);
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                completed?.Invoke(false, null);
                yield break;
            }

            try
            {
                string wrappedJson =
                    "{\"entries\":" + request.downloadHandler.text + "}";
                RemoteScoreResponse response =
                    JsonUtility.FromJson<RemoteScoreResponse>(wrappedJson);
                var records = new List<ScoreRecord>(safeLimit);
                if (response?.entries != null)
                {
                    for (int i = 0; i < response.entries.Count; i++)
                    {
                        RemoteScoreRow row = response.entries[i];
                        if (row == null) continue;
                        records.Add(new ScoreRecord
                        {
                            playerName = NormalizePlayerName(row.player_name),
                            score = row.score
                        });
                    }
                }
                completed?.Invoke(true, records);
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"[Leaderboard] 서버 응답 파싱 실패: {exception.Message}");
                completed?.Invoke(false, null);
            }
        }

        static void ConfigureRequest(UnityWebRequest request)
        {
            request.SetRequestHeader("apikey", PublishableKey);
            request.SetRequestHeader("Accept", "application/json");
        }

        [Serializable]
        sealed class RemoteScoreSubmission
        {
            public string player_name;
            public int score;
        }

        [Serializable]
        sealed class RemoteScoreRow
        {
            public string player_name;
            public int score;
        }

        [Serializable]
        sealed class RemoteScoreResponse
        {
            public List<RemoteScoreRow> entries;
        }
    }
}
