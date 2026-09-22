using System.Collections.Generic;

namespace MobileGame.Core
{
    /// <summary>
    /// Interfaccia per il servizio di classifica (supporta sia persistenza locale che Cloud UGS).
    /// </summary>
    public interface ILeaderboardService
    {
        event System.Action OnLeaderboardUpdated;
        bool QualifiesForTop(int score, float survivalTime);
        bool RecordScore(int score, int wave, float survivalTime, string initials, out int rank);
        bool RecordScore(int score, int wave, float survivalTime, string initials, string email, out int rank);
        int GetHighScore();
        IReadOnlyList<LeaderboardEntry> GetTopEntries();
        void ClearLeaderboard();
        void RefreshCloudScores();
    }
}
