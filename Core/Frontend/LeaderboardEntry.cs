using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LeaderboardEntryUI : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private TextMeshProUGUI rankText;
    [SerializeField] private TextMeshProUGUI usernameText;
    [SerializeField] private TextMeshProUGUI solvedText;
    [SerializeField] private TextMeshProUGUI fastestTimeText;
    [SerializeField] private TextMeshProUGUI streakText;

    [Header("Rank Colors")]
    [SerializeField] private Color rank1Color = new Color(1f, 0.84f, 0f); // Gold
    [SerializeField] private Color rank2Color = new Color(0.75f, 0.75f, 0.75f); // Silver
    [SerializeField] private Color rank3Color = new Color(0.8f, 0.5f, 0.2f); // Bronze

    public void Setup(LeaderboardEntry entry)
    {
        // Rank
        if (rankText != null)
        {
            rankText.text = $"#{entry.rank}";
            
            // Color top 3 ranks
            if (entry.rank == 1)
                rankText.color = rank1Color;
            else if (entry.rank == 2)
                rankText.color = rank2Color;
            else if (entry.rank == 3)
                rankText.color = rank3Color;
        }

        // Username
        if (usernameText != null)
        {
            usernameText.text = entry.username;
        }

        // Total solved
        if (solvedText != null)
        {
            solvedText.text = $"{entry.totalSolved}";
        }

        // Fastest time
        if (fastestTimeText != null)
        {
            if (entry.fastestTime > 0)
            {
                fastestTimeText.text = entry.FastestTimeFormatted;
            }
            else
            {
                fastestTimeText.text = "--:--";
            }
        }

        // Current streak
        if (streakText != null)
        {
            streakText.text = $"{entry.currentStreak}";
        }
    }
}