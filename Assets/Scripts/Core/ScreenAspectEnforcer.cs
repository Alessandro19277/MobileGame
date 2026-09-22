using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MobileGame.Core
{
    /// <summary>
    /// Blocca rigidamente il gioco e l'interfaccia nel formato arcade 9:16 (1080x1920),
    /// esattamente identico alla visualizzazione su PC.
    /// Su smartphone più allungati (20:9, 19.5:9, ecc.) inserisce eleganti bande nere
    /// in alto e in basso (letterbox), garantendo zero distorsioni, zero stretching
    /// e posizionamento perfetto di tutti i pulsanti e grafiche.
    /// Mantiene attive le bande nere anche dopo il ricaricamento della scena o ritorno al menu.
    /// Zero allocazioni GC nel loop di Update (AGENTS.md §4).
    /// </summary>
    [DefaultExecutionOrder(-990)]
    public class ScreenAspectEnforcer : MonoBehaviour
    {
        private const float TARGET_ASPECT = 9f / 16f; // 0.5625 (1080x1920)

        private static ScreenAspectEnforcer _instance;
        public static ScreenAspectEnforcer Instance => _instance;

        private Camera _mainCamera;
        private Camera _backgroundCamera;
        private int    _lastScreenWidth  = -1;
        private int    _lastScreenHeight = -1;

        public static void EnsureInstance()
        {
            if (_instance != null) return;

            var existing = FindAnyObjectByType<ScreenAspectEnforcer>();
            if (existing != null)
            {
                _instance = existing;
                return;
            }

            var go = new GameObject("ScreenAspectEnforcer");
            _instance = go.AddComponent<ScreenAspectEnforcer>();
            DontDestroyOnLoad(go);
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);

            SetupCamerasAndCanvas();
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // Reset dei riferimenti dopo reload scena per riagganciare la nuova camera e canvas
            _mainCamera = null;
            _lastScreenWidth  = -1;
            _lastScreenHeight = -1;
            SetupCamerasAndCanvas();
            ApplyLetterbox();
        }

        private void Start()
        {
            ApplyLetterbox();
        }

        private void Update()
        {
            // Se la camera è stata distrutta (es. ricaricamento scena) o cambia la risoluzione dello schermo
            if (_mainCamera == null || Screen.width != _lastScreenWidth || Screen.height != _lastScreenHeight)
            {
                SetupCamerasAndCanvas();
                ApplyLetterbox();
            }
        }

        public void SetupCamerasAndCanvas()
        {
            if (_mainCamera == null)
            {
                _mainCamera = Camera.main;
                if (_mainCamera == null)
                {
                    _mainCamera = FindAnyObjectByType<Camera>();
                }
            }

            // Crea la telecamera di sfondo per bande nere pure senza artefatti GPU su Android
            if (_backgroundCamera == null)
            {
                var bgCamObj = new GameObject("LetterboxBackgroundCamera");
                bgCamObj.transform.SetParent(transform);
                _backgroundCamera = bgCamObj.AddComponent<Camera>();
                _backgroundCamera.depth = (_mainCamera != null ? _mainCamera.depth : 0) - 10;
                _backgroundCamera.clearFlags = CameraClearFlags.SolidColor;
                _backgroundCamera.backgroundColor = Color.black;
                _backgroundCamera.cullingMask = 0; // Non renderizza nessun oggetto
                _backgroundCamera.rect = new Rect(0f, 0f, 1f, 1f);
                _backgroundCamera.orthographic = true;
                _backgroundCamera.allowHDR = false;
                _backgroundCamera.allowMSAA = false;
            }
            else if (_mainCamera != null)
            {
                _backgroundCamera.depth = _mainCamera.depth - 10;
            }

            // Configura il Canvas per renderizzare dentro il viewport letterbox della telecamera
            ConfigureCanvas();
        }

        private void ConfigureCanvas()
        {
            if (_mainCamera == null)
            {
                _mainCamera = Camera.main;
                if (_mainCamera == null) _mainCamera = FindAnyObjectByType<Camera>();
            }

            var canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include);
            for (int i = 0; i < canvases.Length; i++)
            {
                var canvas = canvases[i];
                if (canvas.isRootCanvas)
                {
                    canvas.renderMode = RenderMode.ScreenSpaceCamera;
                    if (_mainCamera != null) canvas.worldCamera = _mainCamera;
                    canvas.planeDistance = 5f;
                    canvas.sortingOrder = 100;
                }

                var scaler = canvas.GetComponent<CanvasScaler>();
                if (scaler != null)
                {
                    scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                    scaler.referenceResolution = new Vector2(1080f, 1920f);
                    scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                    scaler.matchWidthOrHeight = 0f;
                }
            }
        }

        public void ApplyLetterbox()
        {
            _lastScreenWidth  = Screen.width;
            _lastScreenHeight = Screen.height;

            if (_mainCamera == null)
            {
                _mainCamera = Camera.main;
                if (_mainCamera == null)
                {
                    _mainCamera = FindAnyObjectByType<Camera>();
                    if (_mainCamera == null) return;
                }
            }

            float currentAspect = (float)Screen.width / (float)Screen.height;

            // Ripristina orthographicSize a 5.2f fisso (rapporto 9:16 nativo)
            _mainCamera.orthographic = true;
            _mainCamera.orthographicSize = 5.2f;

            if (currentAspect < TARGET_ASPECT - 0.001f)
            {
                // Schermo più allungato (es. 20:9 o 19.5:9): bande nere sopra e sotto
                float scaleHeight = currentAspect / TARGET_ASPECT;
                float yOffset = (1f - scaleHeight) * 0.5f;
                _mainCamera.rect = new Rect(0f, yOffset, 1f, scaleHeight);
            }
            else if (currentAspect > TARGET_ASPECT + 0.001f)
            {
                // Schermo più largo (es. tablet o iPad): bande nere ai lati
                float scaleWidth = TARGET_ASPECT / currentAspect;
                float xOffset = (1f - scaleWidth) * 0.5f;
                _mainCamera.rect = new Rect(xOffset, 0f, scaleWidth, 1f);
            }
            else
            {
                // Schermo nativo 9:16 esatto
                _mainCamera.rect = new Rect(0f, 0f, 1f, 1f);
            }

            // Ri-applica il collegamento Canvas
            ConfigureCanvas();
        }
    }
}
