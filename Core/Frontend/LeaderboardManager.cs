using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class LeaderboardManager : MonoBehaviour
{
    public static LeaderboardManager Instance;

    [Header("UI References")]
    [SerializeField] private Transform leaderboardContainer; // Parent with Vertical Layout Group
    [SerializeField] private GameObject leaderboardEntryPrefab;
    [SerializeField] private GameObject loadingSpinner;
    [SerializeField] private TextMeshProUGUI statusText;

    public List<GameObject> entryInstances = new List<GameObject>();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }

        ClearEntries();
    }

    public void LoadLeaderboard()
    {
        Debug.Log("[Leaderboard] Loading leaderboard...");

        // Show loading spinner
        if (loadingSpinner != null)
            loadingSpinner.SetActive(true);

        if (statusText != null)
            statusText.text = "";

        ClearEntries();

        FirebaseManager.Instance?.GetLeaderboard(OnLeaderboardSuccess, OnLeaderboardError);
    }

    private void OnLeaderboardSuccess(List<LeaderboardEntry> leaderboard)
    {
        Debug.Log($"[Leaderboard] Loaded {leaderboard.Count} entries");

        // Hide loading spinner
        if (loadingSpinner != null)
            loadingSpinner.SetActive(false);

        if (leaderboard.Count == 0)
        {
            if (statusText != null)
                statusText.text = "No players yet. Be the first!";
            return;
        }

        if (statusText != null)
            statusText.text = "";

        PopulateLeaderboard(leaderboard);
    }

    private void OnLeaderboardError(string error)
    {
        Debug.LogError($"[Leaderboard] Error: {error}");

        // Hide loading spinner
        if (loadingSpinner != null)
            loadingSpinner.SetActive(false);

        if (statusText != null)
            statusText.text = $"Failed to load leaderboard: {error}";
    }

    private void PopulateLeaderboard(List<LeaderboardEntry> leaderboard)
    {
        if (leaderboardContainer == null || leaderboardEntryPrefab == null)
        {
            Debug.LogError("[Leaderboard] Container or prefab not assigned!");
            return;
        }

        foreach (LeaderboardEntry entry in leaderboard)
        {
            GameObject entryObj = Instantiate(leaderboardEntryPrefab, leaderboardContainer);

            LeaderboardEntryUI entryUI = entryObj.GetComponent<LeaderboardEntryUI>();
            if (entryUI != null)
            {
                entryUI.Setup(entry);
            }

            entryInstances.Add(entryObj);
        }

        Debug.Log($"[Leaderboard] Populated {leaderboard.Count} entries");
    }

    public void ClearEntries()
    {
        foreach (GameObject entry in entryInstances)
        {
            if (entry != null)
            {
                Destroy(entry);
            }
        }

        entryInstances.Clear();
    }
}