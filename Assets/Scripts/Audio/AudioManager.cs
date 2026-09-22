using UnityEngine;
using MobileGame.Core;

namespace MobileGame.Audio
{
    /// <summary>
    /// Gestore audio centrale (Singleton DontDestroyOnLoad):
    /// - Architettura ibrida: sintesi procedurale automatica all'avvio (Opzione C)
    ///   con supporto completo a clip audio personalizzati assegnabili da Inspector.
    /// - Pool round-robin di AudioSource per SFX senza allocazioni nei frame (AGENTS.md §4).
    /// - Pitch randomization anti-fatica per spari e crunch nemici.
    /// - Pitch scaling per combo di gemme XP.
    /// - AudioSource dedicato per la colonna sonora Synthwave in loop con switch automatico Menu/Gameplay.
    /// - Salvataggio stato Mute e Volume in PlayerPrefs.
    /// </summary>
    [DefaultExecutionOrder(-800)]
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        private const string PREFS_SFX_MUTED   = "Audio_SfxMuted";
        private const string PREFS_MUSIC_MUTED = "Audio_MusicMuted";

        [Header("Clip SFX — Assegna da Inspector o lascia vuoto per sintesi procedurale")]
        [SerializeField] private AudioClip _clipShoot;
        [SerializeField] private AudioClip _clipEnemyDie;
        [SerializeField] private AudioClip _clipPlayerHit;
        [SerializeField] private AudioClip _clipGameOver;
        [SerializeField] private AudioClip _clipHeal;
        [SerializeField] private AudioClip _clipXpPickup;
        [SerializeField] private AudioClip _clipLevelUp;
        [SerializeField] private AudioClip _clipButtonClick;

        [Header("Musica di Sottofondo (BGM)")]
        [SerializeField] private AudioClip _clipGameplayMusic;
        [SerializeField] private AudioClip _clipMenuMusic;

        [Header("Parametri Pool & Volumi")]
        [SerializeField] [Range(4, 16)] private int    _poolSize   = 10;
        [SerializeField] [Range(0f, 1f)] private float _sfxVolume  = 0.85f;
        [SerializeField] [Range(0f, 1f)] private float _musicVolume = 0.55f;

        // Pool SFX pre-allocato
        private AudioSource[] _sources;
        private int           _sourceIndex;

        // Canale Musica
        private AudioSource _musicSource;

        // Stato Mute
        private bool _isSfxMuted;
        private bool _isMusicMuted;

        // Sistema Pitch Scaling Gemme XP (zero GC)
        private float _lastXpPickupTime;
        private int   _xpComboCount;
        private static readonly float[] XP_PITCH_STEPS = { 1.00f, 1.06f, 1.12f, 1.19f, 1.26f, 1.33f, 1.41f, 1.50f, 1.68f, 1.88f, 2.00f };

        public bool IsSfxMuted   => _isSfxMuted;
        public bool IsMusicMuted => _isMusicMuted;
        public float SfxVolume   { get => _sfxVolume;   set => SetSfxVolume(value); }
        public float MusicVolume { get => _musicVolume; set => SetMusicVolume(value); }

        // ── Lifecycle ──────────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            LoadSettings();
            InitializeAudioSources();
            EnsureClipsGenerated();
        }

        private void Start()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnGameStarted += HandleGameStarted;
                GameManager.Instance.OnGameOver    += HandleGameOver;

                if (GameManager.Instance.CurrentState == GameState.Playing)
                    PlayGameplayMusic();
                else
                    PlayMenuMusic();
            }
            else
            {
                PlayMenuMusic();
            }
        }

        private void OnDestroy()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnGameStarted -= HandleGameStarted;
                GameManager.Instance.OnGameOver    -= HandleGameOver;
            }

            if (Instance == this) Instance = null;
        }

        // ── Inizializzazione Sorgenti & Generazione Procedurale ────────────────

        private void InitializeAudioSources()
        {
            // Canale Musica dedicato
            _musicSource = gameObject.AddComponent<AudioSource>();
            _musicSource.playOnAwake = false;
            _musicSource.loop        = true;
            _musicSource.volume      = _isMusicMuted ? 0f : _musicVolume;

            // Pool SFX Round-Robin
            _sources = new AudioSource[_poolSize];
            for (int i = 0; i < _poolSize; i++)
            {
                _sources[i]             = gameObject.AddComponent<AudioSource>();
                _sources[i].playOnAwake = false;
                _sources[i].volume      = _isSfxMuted ? 0f : _sfxVolume;
            }
        }

        private void EnsureClipsGenerated()
        {
            // Se un clip non è stato assegnato dall'Inspector, lo genera al volo con la sintesi DSP
            if (_clipShoot == null)         _clipShoot         = AudioSynthesizer.GenerateShoot();
            if (_clipEnemyDie == null)      _clipEnemyDie      = AudioSynthesizer.GenerateEnemyDie();
            if (_clipPlayerHit == null)     _clipPlayerHit     = AudioSynthesizer.GeneratePlayerHit();
            if (_clipGameOver == null)      _clipGameOver      = AudioSynthesizer.GenerateGameOver();
            if (_clipHeal == null)          _clipHeal          = AudioSynthesizer.GenerateHeal();
            if (_clipXpPickup == null)      _clipXpPickup      = AudioSynthesizer.GenerateXpPickup();
            if (_clipLevelUp == null)       _clipLevelUp       = AudioSynthesizer.GenerateLevelUp();
            if (_clipButtonClick == null)   _clipButtonClick   = AudioSynthesizer.GenerateButtonClick();

            if (_clipGameplayMusic == null) _clipGameplayMusic = AudioSynthesizer.GenerateGameplayBGM();
            if (_clipMenuMusic == null)     _clipMenuMusic     = AudioSynthesizer.GenerateMenuBGM();
        }

        // ── API Riproduzione SFX ───────────────────────────────────────────────

        /// <summary>Laser al plasma con lieve pitch casuale per non affaticare l'orecchio.</summary>
        public void PlayShoot()
        {
            float pitch = Random.Range(0.94f, 1.06f);
            PlaySfx(_clipShoot, pitch, 0.85f);
        }

        /// <summary>Crunch sgretolamento di nemici con variazione di tono.</summary>
        public void PlayEnemyDie()
        {
            float pitch = Random.Range(0.92f, 1.08f);
            PlaySfx(_clipEnemyDie, pitch, 0.95f);
        }

        /// <summary>Impatto scudo / danno subito dal nanobot.</summary>
        public void PlayPlayerHit() => PlaySfx(_clipPlayerHit, 1.0f, 1.0f);

        /// <summary>Spegnimento reattore Game Over.</summary>
        public void PlayGameOver()
        {
            StopMusic();
            PlaySfx(_clipGameOver, 1.0f, 1.0f);
        }

        /// <summary>Ricarica vita (Health Cross).</summary>
        public void PlayHeal() => PlaySfx(_clipHeal, 1.0f, 0.95f);

        /// <summary>Raccolta gemma XP: il tono sale con ogni gemma raccolta in rapida successione.</summary>
        public void PlayXPPickup()
        {
            float now = Time.unscaledTime;
            if (now - _lastXpPickupTime < 0.45f)
            {
                _xpComboCount = Mathf.Min(_xpComboCount + 1, XP_PITCH_STEPS.Length - 1);
            }
            else
            {
                _xpComboCount = 0;
            }
            _lastXpPickupTime = now;

            float pitch = XP_PITCH_STEPS[_xpComboCount];
            PlaySfx(_clipXpPickup, pitch, 0.75f);
        }

        /// <summary>Fanfare trionfale Level Up.</summary>
        public void PlayLevelUp() => PlaySfx(_clipLevelUp, 1.0f, 1.0f);

        /// <summary>Click UI digitale pulito.</summary>
        public void PlayButtonClick() => PlaySfx(_clipButtonClick, 1.0f, 0.70f);

        // ── API Riproduzione BGM (Musica) ──────────────────────────────────────

        public void PlayGameplayMusic()
        {
            if (_musicSource == null || _clipGameplayMusic == null) return;
            if (_musicSource.clip == _clipGameplayMusic && _musicSource.isPlaying) return;

            _musicSource.clip = _clipGameplayMusic;
            _musicSource.volume = _isMusicMuted ? 0f : _musicVolume;
            _musicSource.Play();
        }

        public void PlayMenuMusic()
        {
            if (_musicSource == null || _clipMenuMusic == null) return;
            if (_musicSource.clip == _clipMenuMusic && _musicSource.isPlaying) return;

            _musicSource.clip = _clipMenuMusic;
            _musicSource.volume = _isMusicMuted ? 0f : (_musicVolume * 0.85f);
            _musicSource.Play();
        }

        public void StopMusic()
        {
            if (_musicSource != null)
                _musicSource.Stop();
        }

        // ── Gestione Eventi Gioco ──────────────────────────────────────────────

        private void HandleGameStarted() => PlayGameplayMusic();
        private void HandleGameOver()    => PlayMenuMusic(); // o StopMusic() seguito da menu music

        // ── Metodi Privati Riproduzione & Settings ──────────────────────────────

        private void PlaySfx(AudioClip clip, float pitch = 1.0f, float volumeScale = 1.0f)
        {
            if (_isSfxMuted || clip == null || _sources == null) return;

            _sourceIndex = (_sourceIndex + 1) % _poolSize;
            var src = _sources[_sourceIndex];
            src.clip   = clip;
            src.pitch  = pitch;
            src.volume = _sfxVolume * volumeScale;
            src.Play();
        }

        public void SetSfxMuted(bool muted)
        {
            _isSfxMuted = muted;
            PlayerPrefs.SetInt(PREFS_SFX_MUTED, muted ? 1 : 0);
            PlayerPrefs.Save();

            if (_sources != null)
            {
                for (int i = 0; i < _sources.Length; i++)
                {
                    if (_sources[i] != null)
                        _sources[i].volume = muted ? 0f : _sfxVolume;
                }
            }
        }

        public void SetMusicMuted(bool muted)
        {
            _isMusicMuted = muted;
            PlayerPrefs.SetInt(PREFS_MUSIC_MUTED, muted ? 1 : 0);
            PlayerPrefs.Save();

            if (_musicSource != null)
            {
                _musicSource.volume = muted ? 0f : _musicVolume;
            }
        }

        public void ToggleSfx()   => SetSfxMuted(!_isSfxMuted);
        public void ToggleMusic() => SetMusicMuted(!_isMusicMuted);

        public void SetSfxVolume(float volume)
        {
            _sfxVolume = Mathf.Clamp01(volume);
            if (!_isSfxMuted && _sources != null)
            {
                for (int i = 0; i < _sources.Length; i++)
                {
                    if (_sources[i] != null) _sources[i].volume = _sfxVolume;
                }
            }
        }

        public void SetMusicVolume(float volume)
        {
            _musicVolume = Mathf.Clamp01(volume);
            if (!_isMusicMuted && _musicSource != null)
            {
                _musicSource.volume = _musicVolume;
            }
        }

        private void LoadSettings()
        {
            _isSfxMuted   = PlayerPrefs.GetInt(PREFS_SFX_MUTED, 0) == 1;
            _isMusicMuted = PlayerPrefs.GetInt(PREFS_MUSIC_MUTED, 0) == 1;
        }
    }
}
