using UnityEngine;

namespace MobileGame.Core
{
    /// <summary>
    /// Cronometro di sopravvivenza: conta il tempo trascorso (in avanti) a partire dall'avvio della partita.
    /// Il gioco è infinito e la partita termina solo alla morte del giocatore.
    /// Zero allocazioni GC nei loop (AGENTS.md §4).
    /// </summary>
    [DefaultExecutionOrder(-850)]
    public class SurvivalTimer : MonoBehaviour
    {
        private static SurvivalTimer _instance;
        private static bool          _isApplicationQuitting = false;

        public static bool HasInstance => _instance != null && !_isApplicationQuitting;

        public static SurvivalTimer Instance
        {
            get
            {
                if (_isApplicationQuitting) return null;

                if (_instance == null)
                {
                    _instance = FindAnyObjectByType<SurvivalTimer>();
                    if (_instance == null && Application.isPlaying && !_isApplicationQuitting)
                    {
                        var go = new GameObject("SurvivalTimer");
                        _instance = go.AddComponent<SurvivalTimer>();
                    }
                }
                return _instance;
            }
        }

        public float ElapsedTime { get; private set; }
        public bool  IsRunning   { get; private set; }

        // Evento zero-GC che notifica il tempo trascorso in secondi
        public event System.Action<float> OnTimeChanged;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            ElapsedTime = 0f;
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

        private void Update()
        {
            if (!IsRunning) return;

            ElapsedTime += Time.deltaTime;
            OnTimeChanged?.Invoke(ElapsedTime);
        }

        public void StartTimer()
        {
            ElapsedTime = 0f;
            IsRunning   = true;
            OnTimeChanged?.Invoke(ElapsedTime);
        }

        public void ResumeTimer()
        {
            IsRunning = true;
        }

        public void StopTimer()
        {
            IsRunning = false;
        }

        public void ResetTimer()
        {
            StopTimer();
            ElapsedTime = 0f;
            OnTimeChanged?.Invoke(ElapsedTime);
        }
    }
}
