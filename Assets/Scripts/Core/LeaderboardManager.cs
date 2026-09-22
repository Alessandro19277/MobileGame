using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Leaderboards;
using UgsLeaderboardEntry = Unity.Services.Leaderboards.Models.LeaderboardEntry;

namespace MobileGame.Core
{
    [Serializable]
    public struct LeaderboardEntry
    {
        public string initials;
        public string email;
        public int    score;
        public int    wave;
        public float  survivalTime;
        public string formattedTime;
        public string date;
    }

    [Serializable]
    internal class LeaderboardDataWrapper
    {
        public List<LeaderboardEntry> entries = new List<LeaderboardEntry>();
    }

    [Serializable]
    public class LeaderboardMetadataPayload
    {
        public string initials;
        public string email;
        public int    wave;
        public float  survivalTime;
        public string formattedTime;
    }

    /// <summary>
    /// Gestisce la classifica globale (Unity Gaming Services - Leaderboards)
    /// con cache locale robusta in PlayerPrefs per funzionamento 100% offline.
    /// </summary>
    [DefaultExecutionOrder(-950)]
    public class LeaderboardManager : MonoBehaviour, ILeaderboardService
    {
        private static LeaderboardManager _instance;
        private static bool               _isApplicationQuitting = false;

        public static bool HasInstance => _instance != null && !_isApplicationQuitting;

        public static LeaderboardManager Instance
        {
            get
            {
                if (_isApplicationQuitting) return null;

                if (_instance == null)
                {
                    _instance = FindAnyObjectByType<LeaderboardManager>();
                    if (_instance == null && Application.isPlaying && !_isApplicationQuitting)
                    {
                        var go = new GameObject("LeaderboardManager");
                        _instance = go.AddComponent<LeaderboardManager>();
                    }
                }
                return _instance;
            }
        }

        public const string LEADERBOARD_ID = "global_leaderboard";
        private const string PREFS_KEY = "NanoSurvivor_Leaderboard";
        private const int    MAX_ENTRIES = 10;

        public event Action OnLeaderboardUpdated;

        private LeaderboardDataWrapper _data = new LeaderboardDataWrapper();
        private bool _isUgsInitialized = false;
        private bool _isSyncing = false;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            LoadLeaderboard();
        }

        private async void Start()
        {
            await InitUgsAndSyncScoresAsync();
        }

        private void OnApplicationQuit()
        {
            _isApplicationQuitting = true;
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        /// <summary>
        /// Inizializza Unity Gaming Services, autentica il giocatore in anonimo
        /// e scarica la classifica globale da UGS.
        /// </summary>
        public async Task InitUgsAndSyncScoresAsync()
        {
            if (_isUgsInitialized) return;

            try
            {
                if (UnityServices.State == ServicesInitializationState.Uninitialized)
                {
                    await UnityServices.InitializeAsync();
                }

                if (!AuthenticationService.Instance.IsSignedIn)
                {
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                }

                _isUgsInitialized = true;
                Debug.Log($"[LeaderboardManager] UGS inizializzato con successo! PlayerId: {AuthenticationService.Instance.PlayerId}");

                // Scarica i punteggi globali dal cloud
                await FetchCloudScoresAsync();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LeaderboardManager] UGS offline o non disponibile ({ex.Message}). Viene usata la classifica locale.");
            }
        }

        public void RefreshCloudScores()
        {
            if (_isSyncing) return;

            if (_isUgsInitialized && AuthenticationService.Instance.IsSignedIn)
            {
                _ = FetchCloudScoresAsync();
            }
            else
            {
                _ = InitUgsAndSyncScoresAsync();
            }
        }

        /// <summary>
        /// Scarica la Top 10 dal cloud Unity Gaming Services e aggiorna la cache locale.
        /// </summary>
        public async Task FetchCloudScoresAsync()
        {
            if (_isSyncing) return;
            _isSyncing = true;

            try
            {
                var options = new GetScoresOptions
                {
                    Offset = 0,
                    Limit = MAX_ENTRIES,
                    IncludeMetadata = true
                };

                var scoresPage = await LeaderboardsService.Instance.GetScoresAsync(LEADERBOARD_ID, options);

                if (scoresPage != null && scoresPage.Results != null && scoresPage.Results.Count > 0)
                {
                    var cloudEntries = new List<LeaderboardEntry>(scoresPage.Results.Count);

                    for (int i = 0; i < scoresPage.Results.Count; i++)
                    {
                        var item = scoresPage.Results[i];
                        string initials = "AAA";
                        string email = string.Empty;
                        int wave = 1;
                        float survivalTime = 0f;
                        string formattedTime = "00:00";
                        string dateStr = item.UpdatedTime.ToLocalTime().ToString("dd/MM/yyyy HH:mm");

                        if (!string.IsNullOrEmpty(item.Metadata))
                        {
                            try
                            {
                                var meta = JsonUtility.FromJson<LeaderboardMetadataPayload>(item.Metadata);
                                if (meta != null)
                                {
                                    if (!string.IsNullOrEmpty(meta.initials)) initials = SanitizeInitials(meta.initials);
                                    if (!string.IsNullOrEmpty(meta.email)) email = meta.email;
                                    wave = meta.wave;
                                    survivalTime = meta.survivalTime;
                                    if (!string.IsNullOrEmpty(meta.formattedTime)) formattedTime = meta.formattedTime;
                                    else
                                    {
                                        int m = (int)(survivalTime / 60f);
                                        int s = (int)(survivalTime % 60f);
                                        formattedTime = $"{m:00}:{s:00}";
                                    }
                                }
                            }
                            catch
                            {
                                if (!string.IsNullOrEmpty(item.PlayerName)) initials = SanitizeInitials(item.PlayerName);
                            }
                        }
                        else if (!string.IsNullOrEmpty(item.PlayerName))
                        {
                            initials = SanitizeInitials(item.PlayerName);
                        }

                        cloudEntries.Add(new LeaderboardEntry
                        {
                            initials      = initials,
                            email         = email,
                            score         = (int)item.Score,
                            wave          = wave,
                            survivalTime  = survivalTime,
                            formattedTime = formattedTime,
                            date          = dateStr
                        });
                    }

                    _data.entries = cloudEntries;
                    SaveLeaderboard();
                    OnLeaderboardUpdated?.Invoke();
                    Debug.Log($"[LeaderboardManager] Sincronizzati {_data.entries.Count} record dalla classifica globale UGS.");
                }
                else
                {
                    // La tabella cloud è vuota: se abbiamo record locali, carichiamo il migliore come seed iniziale!
                    if (_data.entries != null && _data.entries.Count > 0)
                    {
                        var top = _data.entries[0];
                        await SubmitScoreToCloudAsync(top.score, top.wave, top.survivalTime, top.initials, top.email);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LeaderboardManager] Errore recupero record cloud: {ex.Message}");
            }
            finally
            {
                _isSyncing = false;
            }
        }

        private async Task SubmitScoreToCloudAsync(int score, int wave, float survivalTime, string initials, string email)
        {
            try
            {
                if (!_isUgsInitialized || !AuthenticationService.Instance.IsSignedIn)
                {
                    return;
                }

                string cleanInitials = SanitizeInitials(initials);
                string cleanEmail = !string.IsNullOrEmpty(email) ? email.Trim() : string.Empty;
                int mins = (int)(survivalTime / 60f);
                int secs = (int)(survivalTime % 60f);

                var metaPayload = new LeaderboardMetadataPayload
                {
                    initials      = cleanInitials,
                    email         = cleanEmail,
                    wave          = wave,
                    survivalTime  = survivalTime,
                    formattedTime = $"{mins:00}:{secs:00}"
                };

                var options = new AddPlayerScoreOptions
                {
                    Metadata = metaPayload
                };

                await LeaderboardsService.Instance.AddPlayerScoreAsync(LEADERBOARD_ID, score, options);
                Debug.Log($"[LeaderboardManager] Punteggio {score} inviato con successo alla classifica globale UGS ({LEADERBOARD_ID})!");

                // Riscarica la classifica per aggiornare l'ordine e il rank globale
                await FetchCloudScoresAsync();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LeaderboardManager] Errore invio punteggio a UGS: {ex.Message}");
            }
        }

        public bool QualifiesForTop(int score, float survivalTime)
        {
            if (score <= 0) return false;
            if (_data.entries == null || _data.entries.Count < MAX_ENTRIES) return true;

            var last = _data.entries[_data.entries.Count - 1];
            if (score > last.score) return true;
            if (score == last.score && survivalTime > last.survivalTime) return true;

            return false;
        }

        public bool RecordScore(int score, int wave, float survivalTime, string initials, string email, out int rank)
        {
            int previousHighScore = GetHighScore();

            int mins = (int)(survivalTime / 60f);
            int secs = (int)(survivalTime % 60f);
            string timeStr = $"{mins:00}:{secs:00}";
            string dateStr = DateTime.Now.ToString("dd/MM/yyyy HH:mm");

            string cleanInitials = SanitizeInitials(initials);
            string cleanEmail = !string.IsNullOrEmpty(email) ? email.Trim() : string.Empty;

            var newEntry = new LeaderboardEntry
            {
                initials      = cleanInitials,
                email         = cleanEmail,
                score         = score,
                wave          = wave,
                survivalTime  = survivalTime,
                formattedTime = timeStr,
                date          = dateStr
            };

            _data.entries.Add(newEntry);

            // Ordina decrescente per punteggio, a parità di punti per tempo sopravvissuto
            _data.entries.Sort((a, b) =>
            {
                int scoreCmp = b.score.CompareTo(a.score);
                if (scoreCmp != 0) return scoreCmp;
                return b.survivalTime.CompareTo(a.survivalTime);
            });

            rank = _data.entries.FindIndex(e => e.score == newEntry.score && Math.Abs(e.survivalTime - newEntry.survivalTime) < 0.01f) + 1;

            if (_data.entries.Count > MAX_ENTRIES)
            {
                _data.entries.RemoveRange(MAX_ENTRIES, _data.entries.Count - MAX_ENTRIES);
            }

            SaveLeaderboard();
            OnLeaderboardUpdated?.Invoke();

            // Invio asincrono al cloud UGS
            if (_isUgsInitialized && AuthenticationService.Instance.IsSignedIn)
            {
                _ = SubmitScoreToCloudAsync(score, wave, survivalTime, initials, email);
            }

            return score > previousHighScore;
        }

        public bool RecordScore(int score, int wave, float survivalTime, string initials, out int rank)
        {
            return RecordScore(score, wave, survivalTime, initials, string.Empty, out rank);
        }

        public bool RecordScore(int score, int wave, float survivalTime, out int rank)
        {
            return RecordScore(score, wave, survivalTime, "AAA", out rank);
        }

        public static string SanitizeInitials(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "AAA";
            raw = raw.Trim().ToUpperInvariant();
            if (raw.Length > 3) raw = raw.Substring(0, 3);
            return raw;
        }

        public int GetHighScore()
        {
            if (_data == null || _data.entries == null || _data.entries.Count == 0)
                return 0;

            return _data.entries[0].score;
        }

        public IReadOnlyList<LeaderboardEntry> GetTopEntries()
        {
            return _data.entries;
        }

        public void ClearLeaderboard()
        {
            _data.entries.Clear();
            PlayerPrefs.DeleteKey(PREFS_KEY);
            PlayerPrefs.Save();
            OnLeaderboardUpdated?.Invoke();
        }

        private void LoadLeaderboard()
        {
            string json = PlayerPrefs.GetString(PREFS_KEY, string.Empty);

            if (string.IsNullOrEmpty(json))
            {
                var seed = Resources.Load<TextAsset>("default_leaderboard");
                if (seed != null && !string.IsNullOrEmpty(seed.text))
                {
                    json = seed.text;
                }
            }

            if (!string.IsNullOrEmpty(json))
            {
                try
                {
                    _data = JsonUtility.FromJson<LeaderboardDataWrapper>(json) ?? new LeaderboardDataWrapper();
                    for (int i = 0; i < _data.entries.Count; i++)
                    {
                        var item = _data.entries[i];
                        if (string.IsNullOrEmpty(item.initials))
                        {
                            item.initials = "AAA";
                        }
                        if (item.email == null)
                        {
                            item.email = string.Empty;
                        }
                        _data.entries[i] = item;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[LeaderboardManager] Errore caricamento leaderboard: {e.Message}");
                    _data = new LeaderboardDataWrapper();
                }
            }
            else
            {
                _data = new LeaderboardDataWrapper();
            }
        }

        private void SaveLeaderboard()
        {
            try
            {
                string json = JsonUtility.ToJson(_data);
                PlayerPrefs.SetString(PREFS_KEY, json);
                PlayerPrefs.Save();
            }
            catch (Exception e)
            {
                Debug.LogError($"[LeaderboardManager] Errore salvataggio leaderboard: {e.Message}");
            }
        }
    }
}
