using UnityEngine;
using UnityEngine.SceneManagement;
using MobileGame.Audio;

namespace MobileGame.Core
{
    public enum GameState { MainMenu, Playing, GameOver }

    /// <summary>
    /// Singleton che gestisce stato globale, punteggio, wave e salvataggio partita.
    /// NON usa DontDestroyOnLoad: viene ricreato a ogni scene reload (restart pulito).
    /// [DefaultExecutionOrder(-900)] lo rende disponibile in Awake a tutti gli altri script.
    /// </summary>
    [DefaultExecutionOrder(-900)]
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        /// <summary>
        /// Se true al caricamento della scena, salta il menu e avvia direttamente la partita (per il Retry).
        /// </summary>
        public static bool AutoStartOnLoad { get; set; } = false;

        public GameState CurrentState      { get; private set; } = GameState.MainMenu;
        public int       Score             { get; private set; }
        public int       Wave              { get; private set; }
        public float     LastSurvivalTime  { get; private set; }
        public bool      IsNewHighScore          { get; private set; }
        public bool      QualifiesForLeaderboard { get; private set; }
        public bool      ScoreRecorded           { get; private set; }
        public int       LastRank                { get; private set; }

        // Events — nessuna alloc quando invocati (multicast delegate call)
        public event System.Action<int> OnScoreChanged;
        public event System.Action<int> OnWaveChanged;
        public event System.Action      OnGameStarted;
        public event System.Action      OnGameOver;

        // ── Lifecycle ──────────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            if (AutoStartOnLoad)
            {
                AutoStartOnLoad = false;
                StartGame();
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ── Game Flow ──────────────────────────────────────────────────────────

        public void StartGame()
        {
            Score            = 0;
            Wave             = 1;
            IsNewHighScore   = false;
            QualifiesForLeaderboard = false;
            ScoreRecorded    = false;
            LastRank         = 0;
            CurrentState     = GameState.Playing;

            OnGameStarted?.Invoke();
            OnScoreChanged?.Invoke(0);
            OnWaveChanged?.Invoke(1);

            SurvivalTimer.Instance?.StartTimer();
        }

        public void AddScore(int amount)
        {
            if (CurrentState != GameState.Playing) return;
            Score += amount;
            OnScoreChanged?.Invoke(Score);
        }

        public void AdvanceWave()
        {
            Wave++;
            OnWaveChanged?.Invoke(Wave);
        }

        public void TriggerGameOver()
        {
            if (CurrentState == GameState.GameOver) return;
            CurrentState = GameState.GameOver;

            SurvivalTimer.Instance?.StopTimer();
            LastSurvivalTime = SurvivalTimer.Instance != null ? SurvivalTimer.Instance.ElapsedTime : 0f;
            ScoreRecorded = false;

            if (LeaderboardManager.Instance != null)
            {
                IsNewHighScore = Score > LeaderboardManager.Instance.GetHighScore();
                QualifiesForLeaderboard = LeaderboardManager.Instance.QualifiesForTop(Score, LastSurvivalTime);
            }
            else
            {
                IsNewHighScore = false;
                QualifiesForLeaderboard = false;
            }

            AudioManager.Instance?.PlayGameOver();
            OnGameOver?.Invoke();
        }

        /// <summary>
        /// Registra formalmente il punteggio nella leaderboard con le iniziali del giocatore ed eventuale email collegata.
        /// </summary>
        public void RecordFinalScore(string initials, string email = null)
        {
            if (ScoreRecorded) return;
            ScoreRecorded = true;

            string finalEmail = email;
            if (string.IsNullOrEmpty(finalEmail) && UserProfileManager.Instance != null && UserProfileManager.Instance.IsLinked)
            {
                finalEmail = UserProfileManager.Instance.Email;
            }

            if (LeaderboardManager.Instance != null)
            {
                IsNewHighScore = LeaderboardManager.Instance.RecordScore(Score, Wave, LastSurvivalTime, initials, finalEmail, out int rank);
                LastRank = rank;
            }
        }

        public void RestartGame()
        {
            if (!ScoreRecorded && QualifiesForLeaderboard)
            {
                string initials = (UserProfileManager.Instance != null && UserProfileManager.Instance.IsLinked)
                    ? UserProfileManager.Instance.Initials
                    : "AAA";
                string email = (UserProfileManager.Instance != null && UserProfileManager.Instance.IsLinked)
                    ? UserProfileManager.Instance.Email
                    : string.Empty;
                RecordFinalScore(initials, email);
            }

            // Riavvia subito una nuova partita senza passare dal menu
            AutoStartOnLoad = true;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        public void ReturnToMainMenu()
        {
            if (!ScoreRecorded && QualifiesForLeaderboard)
            {
                string initials = (UserProfileManager.Instance != null && UserProfileManager.Instance.IsLinked)
                    ? UserProfileManager.Instance.Initials
                    : "AAA";
                string email = (UserProfileManager.Instance != null && UserProfileManager.Instance.IsLinked)
                    ? UserProfileManager.Instance.Email
                    : string.Empty;
                RecordFinalScore(initials, email);
            }

            // Ricarica la scena e mostra il menu principale
            AutoStartOnLoad = false;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
