using System;

[Serializable]
public class LeaderboardEntry
{
    public int rank;
    public string userId;
    public string username;
    public int totalSolved;
    public int totalMysteries;
    public int fastestTime;
    public int currentStreak;

    public float SolveRate
    {
        get
        {
            if (totalMysteries == 0) return 0f;
            return (float)totalSolved / totalMysteries * 100f;
        }
    }

    public string FastestTimeFormatted
    {
        get
        {
            int minutes = fastestTime / 60;
            int seconds = fastestTime % 60;
            return $"{minutes}:{seconds:D2}";
        }
    }
}