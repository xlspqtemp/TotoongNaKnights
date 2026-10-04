using System;
using System.Collections.Generic;
using LootLocker;
using LootLocker.Requests;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Submits eligible run scores and loads rows for the wellness-best player leaderboard.
/// </summary>
public class LeaderboardManager : MonoBehaviour
{
    private const string LeaderboardKey = "wellness_best";
    private const int LeaderboardEntryCount = 20;
    private const int LeaderboardStartOffset = 0;
    private const string OfflineMessage = "Leaderboards need an internet connection.";
    private const string SaveFailureMessage = "Couldn't save your score. Check your connection.";
    private const string GuestWinMessage = "Log in with an account to save your score.";
    private const string DisplayNameSuffix = "***";

    [Serializable]
    private sealed class ScoreMetadata
    {
        public string difficulty;
    }

    private Text statusText;
    private Button retryButton;
    private Text[] rankTexts;
    private Text[] playerNameTexts;
    private Text[] scoreTexts;
    private Text[] difficultyTexts;
    private Image[] rowBackgrounds;
    private string currentPlayerUlid;
    private string currentPlayerPublicUid;
    private bool requestInProgress;

    /// <summary>
    /// Binds the leaderboard UI and immediately starts loading scores.
    /// </summary>
    public void Initialize(Text requestStatusText, Button requestRetryButton, Text[] rowRanks,
        Text[] rowPlayers, Text[] rowScores, Text[] rowDifficulties, Image[] backgrounds)
    {
        statusText = requestStatusText;
        retryButton = requestRetryButton;
        rankTexts = rowRanks;
        playerNameTexts = rowPlayers;
        scoreTexts = rowScores;
        difficultyTexts = rowDifficulties;
        rowBackgrounds = backgrounds;
        if (retryButton != null)
        {
            retryButton.onClick.AddListener(LoadScores);
        }

        LoadScores();
    }

    /// <summary>
    /// Refreshes the leaderboard when opened again or when Retry is pressed.
    /// </summary>
    public void LoadScores()
    {
        if (requestInProgress)
        {
            return;
        }

        ClearRows();
        if (Application.internetReachability == NetworkReachability.NotReachable ||
            !TryGetActivePlayer(out currentPlayerUlid, out LootLockerPlayerData currentPlayerData))
        {
            currentPlayerPublicUid = string.Empty;
            ShowStatus(OfflineMessage, true);
            return;
        }
        currentPlayerPublicUid = currentPlayerData.PublicUID;

        requestInProgress = true;
        ShowStatus("Loading...", false);
        LootLockerSDKManager.GetScoreList(LeaderboardKey, LeaderboardEntryCount, LeaderboardStartOffset, response =>
        {
            if (this == null)
            {
                return;
            }

            requestInProgress = false;
            if (response == null || !response.success)
            {
                ClearRows();
                ShowStatus(OfflineMessage, true);
                return;
            }

            LootLockerLeaderboardMember[] entries = response.items ?? Array.Empty<LootLockerLeaderboardMember>();
            if (entries.Length == 0)
            {
                ShowStatus("No scores yet", false);
                return;
            }

            ShowStatus(string.Empty, false);
            DisplayEntries(entries);
        });
    }

    /// <summary>
    /// Submits a final run score only for authenticated non-guest wins at or above the win threshold.
    /// </summary>
    public void SubmitWinningScore(float finalWellness, float winThreshold, Action<string> onStatus)
    {
        if (!TryGetActivePlayer(out string playerUlid, out LootLockerPlayerData playerData))
        {
            onStatus?.Invoke(SaveFailureMessage);
            return;
        }

        if (playerData.CurrentPlatform.Platform == LL_AuthPlatforms.Guest)
        {
            onStatus?.Invoke(GuestWinMessage);
            return;
        }

        if (finalWellness < winThreshold)
        {
            return;
        }

        if (Application.internetReachability == NetworkReachability.NotReachable)
        {
            onStatus?.Invoke(SaveFailureMessage);
            return;
        }

        int score = Mathf.RoundToInt(finalWellness);
        string metadata = CreateDifficultyMetadata();
        LootLockerSDKManager.SubmitScore(string.Empty, score, LeaderboardKey, metadata, response =>
        {
            if (this == null)
            {
                return;
            }

            if (response == null || !response.success)
            {
                onStatus?.Invoke(SaveFailureMessage);
            }
        }, playerUlid);
    }

    /// <summary>
    /// Assigns a masked display name after a successful White Label login when no name exists.
    /// </summary>
    public static void EnsureWhiteLabelDisplayName(string email, string playerUlid, string existingPlayerName)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(playerUlid) ||
            !string.IsNullOrWhiteSpace(existingPlayerName))
        {
            return;
        }

        LootLockerPlayerData playerData = LootLockerSDKManager.GetPlayerDataForPlayerWithUlid(playerUlid);
        if (playerData != null && !string.IsNullOrWhiteSpace(playerData.Name))
        {
            return;
        }

        int atIndex = email.IndexOf('@');
        string emailPrefix = atIndex >= 0 ? email.Substring(0, atIndex) : email;
        if (string.IsNullOrEmpty(emailPrefix))
        {
            return;
        }

        int prefixLength = Mathf.Min(3, emailPrefix.Length);
        string displayName = emailPrefix.Substring(0, prefixLength) + DisplayNameSuffix;
        LootLockerSDKManager.SetPlayerName(displayName, response =>
        {
            if (response == null || !response.success)
            {
                Debug.LogWarning("Could not set the player's leaderboard display name.");
            }
        }, playerUlid);
    }

    private static string CreateDifficultyMetadata()
    {
        string difficulty = "Infection Severity " + DifficultySettings.EnemyLevel + "/5, " +
                            "Immune System " + DifficultySettings.DefenseLevel + "/5, " +
                            "Lifestyle " + DifficultySettings.RandomEventsLevel + "/5";
        return "{\"difficulty\":\"" + difficulty + "\"}";
    }

    private static bool TryGetActivePlayer(out string playerUlid, out LootLockerPlayerData playerData)
    {
        playerUlid = LootLockerSDKManager.GetDefaultPlayerUlid();
        playerData = null;
        if (string.IsNullOrWhiteSpace(playerUlid))
        {
            return false;
        }

        List<string> activePlayers = LootLockerSDKManager.GetActivePlayerUlids();
        bool isActive = false;
        if (activePlayers != null)
        {
            for (int i = 0; i < activePlayers.Count; i++)
            {
                if (string.Equals(activePlayers[i], playerUlid, StringComparison.OrdinalIgnoreCase))
                {
                    isActive = true;
                    break;
                }
            }
        }

        if (!isActive)
        {
            return false;
        }

        playerData = LootLockerSDKManager.GetPlayerDataForPlayerWithUlid(playerUlid);
        return playerData != null && !string.IsNullOrWhiteSpace(playerData.SessionToken);
    }

    private void DisplayEntries(LootLockerLeaderboardMember[] entries)
    {
        int displayCount = Mathf.Min(entries.Length, rankTexts != null ? rankTexts.Length : 0);
        for (int i = 0; i < displayCount; i++)
        {
            LootLockerLeaderboardMember entry = entries[i];
            if (entry == null)
            {
                continue;
            }

            if (rankTexts[i] != null)
            {
                rankTexts[i].text = entry.rank.ToString();
                rankTexts[i].gameObject.SetActive(true);
            }

            if (playerNameTexts[i] != null)
            {
                string playerName = entry.player != null && !string.IsNullOrWhiteSpace(entry.player.name)
                    ? MaskEmailDisplayName(entry.player.name)
                    : entry.player != null && !string.IsNullOrWhiteSpace(entry.player.public_uid)
                        ? entry.player.public_uid
                        : "Player";
                playerNameTexts[i].text = playerName;
                playerNameTexts[i].gameObject.SetActive(true);
            }

            if (scoreTexts[i] != null)
            {
                scoreTexts[i].text = entry.score.ToString();
                scoreTexts[i].gameObject.SetActive(true);
            }

            if (difficultyTexts[i] != null)
            {
                difficultyTexts[i].text = ParseDifficulty(entry.metadata);
                difficultyTexts[i].gameObject.SetActive(true);
            }

            if (rowBackgrounds != null && i < rowBackgrounds.Length && rowBackgrounds[i] != null)
            {
                bool isCurrentPlayer = entry.player != null &&
                    (string.Equals(entry.player.ulid, currentPlayerUlid, StringComparison.OrdinalIgnoreCase) ||
                     (!string.IsNullOrWhiteSpace(currentPlayerPublicUid) &&
                      string.Equals(entry.player.public_uid, currentPlayerPublicUid, StringComparison.OrdinalIgnoreCase)));
                rowBackgrounds[i].color = isCurrentPlayer
                    ? new Color(0.12f, 0.43f, 0.43f, 1f)
                    : i % 2 == 0
                        ? new Color(0.035f, 0.078f, 0.092f, 0.98f)
                        : new Color(0.025f, 0.06f, 0.073f, 0.98f);
                rowBackgrounds[i].gameObject.SetActive(true);
            }
        }
    }

    private static string MaskEmailDisplayName(string playerName)
    {
        int atIndex = playerName.IndexOf('@');
        if (atIndex < 0)
        {
            return playerName;
        }

        string prefix = playerName.Substring(0, atIndex);
        int prefixLength = Mathf.Min(3, prefix.Length);
        return prefix.Substring(0, prefixLength) + DisplayNameSuffix;
    }

    private static string ParseDifficulty(string metadata)
    {
        if (string.IsNullOrWhiteSpace(metadata))
        {
            return "—";
        }

        try
        {
            ScoreMetadata parsed = JsonUtility.FromJson<ScoreMetadata>(metadata);
            return parsed != null && !string.IsNullOrWhiteSpace(parsed.difficulty)
                ? parsed.difficulty
                : "—";
        }
        catch (Exception)
        {
            return "—";
        }
    }

    private void ClearRows()
    {
        if (rankTexts == null)
        {
            return;
        }

        for (int i = 0; i < rankTexts.Length; i++)
        {
            SetRowText(rankTexts, i, string.Empty);
            SetRowText(playerNameTexts, i, string.Empty);
            SetRowText(scoreTexts, i, string.Empty);
            SetRowText(difficultyTexts, i, string.Empty);
            if (rowBackgrounds != null && i < rowBackgrounds.Length && rowBackgrounds[i] != null)
            {
                rowBackgrounds[i].gameObject.SetActive(false);
            }
        }
    }

    private static void SetRowText(Text[] texts, int index, string value)
    {
        if (texts != null && index < texts.Length && texts[index] != null)
        {
            texts[index].text = value;
            texts[index].gameObject.SetActive(false);
        }
    }

    private void ShowStatus(string message, bool showRetry)
    {
        if (statusText != null)
        {
            statusText.text = message;
            statusText.gameObject.SetActive(!string.IsNullOrEmpty(message));
        }

        if (retryButton != null)
        {
            retryButton.gameObject.SetActive(showRetry);
        }
    }
}
