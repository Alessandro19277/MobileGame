using System;
using UnityEngine;

namespace MobileGame.Core
{
    /// <summary>
    /// Gestisce i dati del profilo pilota (Email e 3 Iniziali Arcade per la classifica).
    /// Persistenza locale tramite PlayerPrefs.
    /// Zero allocazioni nei loop di gioco (AGENTS.md §4).
    /// </summary>
    [DefaultExecutionOrder(-960)]
    public class UserProfileManager : MonoBehaviour
    {
        private static UserProfileManager _instance;
        private static bool               _isApplicationQuitting = false;

        public static bool HasInstance => Instance != null;

        public static UserProfileManager Instance
        {
            get
            {
                if (_isApplicationQuitting) return null;

                if (_instance == null)
                {
                    _instance = FindAnyObjectByType<UserProfileManager>();
                    if (_instance == null && Application.isPlaying && !_isApplicationQuitting)
                    {
                        var go = new GameObject("UserProfileManager");
                        _instance = go.AddComponent<UserProfileManager>();
                    }
                }
                return _instance;
            }
        }

        private const string PREFS_EMAIL    = "NanoSurvivor_User_Email";
        private const string PREFS_INITIALS = "NanoSurvivor_User_Initials";
        private const string PREFS_LINKED   = "NanoSurvivor_User_IsLinked";

        [SerializeField] private string _email    = string.Empty;
        [SerializeField] private string _initials = string.Empty;
        [SerializeField] private bool   _isLinked = false;

        public string Email    => _email;
        public string Initials => _initials;
        public bool   IsLinked => _isLinked && !string.IsNullOrEmpty(_email) && _initials.Length == 3;
        public bool   HasInitials => IsLinked;

        public event Action OnProfileChanged;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
            LoadProfile();
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

        // ── Persistenza ────────────────────────────────────────────────────────

        private void LoadProfile()
        {
            _email    = PlayerPrefs.GetString(PREFS_EMAIL, string.Empty);
            _initials = PlayerPrefs.GetString(PREFS_INITIALS, string.Empty);
            _isLinked = PlayerPrefs.GetInt(PREFS_LINKED, 0) == 1;

            if (_initials.Length > 3)
            {
                _initials = _initials.Substring(0, 3).ToUpperInvariant();
            }

            // Se l'utente non ha collegato un account email, ripulisci lo stato
            // per garantire che il pilota rimanga "GUEST" e a ogni record vengano richieste le 3 lettere.
            if (!_isLinked || string.IsNullOrEmpty(_email) || _initials.Length != 3)
            {
                _isLinked = false;
                _email    = string.Empty;
                _initials = string.Empty;
                PlayerPrefs.DeleteKey(PREFS_EMAIL);
                PlayerPrefs.DeleteKey(PREFS_INITIALS);
                PlayerPrefs.DeleteKey(PREFS_LINKED);
                PlayerPrefs.Save();
            }
        }

        private void SaveProfile()
        {
            PlayerPrefs.SetString(PREFS_EMAIL, _email);
            PlayerPrefs.SetString(PREFS_INITIALS, _initials);
            PlayerPrefs.SetInt(PREFS_LINKED, _isLinked ? 1 : 0);
            PlayerPrefs.Save();

            OnProfileChanged?.Invoke();
        }

        // ── API Pubblica ───────────────────────────────────────────────────────

        /// <summary>
        /// Collega l'utente associando la propria email e le 3 iniziali arcade per i record.
        /// </summary>
        public bool LinkProfile(string email, string initials, out string errorMessage)
        {
            string cleanEmail = email != null ? email.Trim() : string.Empty;
            string cleanInitials = SanitizeInitials(initials);

            if (string.IsNullOrEmpty(cleanEmail) || !IsValidEmail(cleanEmail))
            {
                errorMessage = "Inserisci un indirizzo email valido (es. nome@dominio.com)";
                return false;
            }

            if (cleanInitials.Length != 3)
            {
                errorMessage = "Le iniziali devono essere composte da esattamente 3 caratteri!";
                return false;
            }

            _email    = cleanEmail;
            _initials = cleanInitials;
            _isLinked = true;
            SaveProfile();

            errorMessage = string.Empty;
            return true;
        }

        /// <summary>
        /// Rimuove il collegamento dell'account locale.
        /// </summary>
        public void UnlinkProfile()
        {
            _email    = string.Empty;
            _initials = string.Empty;
            _isLinked = false;

            PlayerPrefs.DeleteKey(PREFS_EMAIL);
            PlayerPrefs.DeleteKey(PREFS_INITIALS);
            PlayerPrefs.DeleteKey(PREFS_LINKED);
            PlayerPrefs.Save();

            OnProfileChanged?.Invoke();
        }

        /// <summary>
        /// Imposta solo le 3 iniziali (solo se l'account è collegato).
        /// </summary>
        public void SetInitialsOnly(string initials)
        {
            if (!_isLinked) return;

            string clean = SanitizeInitials(initials);
            if (clean.Length == 3)
            {
                _initials = clean;
                PlayerPrefs.SetString(PREFS_INITIALS, _initials);
                PlayerPrefs.Save();
                OnProfileChanged?.Invoke();
            }
        }

        // ── Helper di Validazione ──────────────────────────────────────────────

        public static string SanitizeInitials(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return string.Empty;

            raw = raw.Trim().ToUpperInvariant();
            if (raw.Length > 3) raw = raw.Substring(0, 3);
            return raw;
        }

        public static bool IsValidEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email)) return false;
            if (email.Length < 5) return false;

            int atIndex = email.IndexOf('@');
            int lastDotIndex = email.LastIndexOf('.');

            return atIndex > 0 && lastDotIndex > atIndex + 1 && lastDotIndex < email.Length - 1;
        }
    }
}
