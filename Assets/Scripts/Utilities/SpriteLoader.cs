using System.Collections.Generic;
using UnityEngine;

namespace MobileGame.Utilities
{
    /// <summary>
    /// Carica gli sprite da Resources/Sprites/ e gestisce l'animazione ciclica a più frame
    /// (2, 3 o più frame) a cadenza temporale.
    /// Zero allocazioni GC nei loop (AGENTS.md §4).
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class SpriteLoader : MonoBehaviour
    {
        [Tooltip("Nome del file sprite principale in Resources/Sprites/ (senza estensione)")]
        [SerializeField] private string _spriteName;

        [Tooltip("Nome del secondo frame opzionale (retrocompatibilità)")]
        [SerializeField] private string _spriteNameFrame2;

        [Tooltip("Ulteriori frame per animazioni a 3 o più fotogrammi")]
        [SerializeField] private string[] _additionalFrames;

        [Tooltip("Velocità di alternanza tra frame in secondi")]
        [SerializeField] private float _animationInterval = 0.35f;

        private SpriteRenderer _spriteRenderer;
        private Sprite[]       _frames;
        private float          _timer;
        private int            _currentFrameIndex;

        private void Awake()
        {
            _spriteRenderer = GetComponent<SpriteRenderer>();

            var list = new List<Sprite>(4);

            AddSpriteIfFound(LoadSpriteAsset(_spriteName), list);
            AddSpriteIfFound(LoadSpriteAsset(_spriteNameFrame2), list);

            if (_additionalFrames != null && _additionalFrames.Length > 0)
            {
                for (int i = 0; i < _additionalFrames.Length; i++)
                {
                    AddSpriteIfFound(LoadSpriteAsset(_additionalFrames[i]), list);
                }
            }

            _frames = list.ToArray();

            if (_frames.Length > 0)
            {
                _spriteRenderer.sprite = _frames[0];
            }

            if (_frames.Length > 1)
            {
                Debug.Log($"[SpriteLoader] {gameObject.name}: configurata animazione a {_frames.Length} frame con intervallo {_animationInterval}s.");
            }
        }

        private void OnEnable()
        {
            // Reset animazione quando l'entità viene estratta dalla pool
            _timer = 0f;
            _currentFrameIndex = 0;
            if (_frames != null && _frames.Length > 0 && _spriteRenderer != null)
            {
                _spriteRenderer.sprite = _frames[0];
            }
        }

        private static void AddSpriteIfFound(Sprite sprite, List<Sprite> list)
        {
            if (sprite != null && !list.Contains(sprite))
            {
                list.Add(sprite);
            }
        }

        private Sprite LoadSpriteAsset(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;

            // 1. Prova caricamento diretto
            var s = Resources.Load<Sprite>($"Sprites/{name}");
            if (s != null) return s;

            // 2. Prova caricamento sub-assets (fondamentale per SpriteMode Multiple!)
            var all = Resources.LoadAll<Sprite>($"Sprites/{name}");
            if (all != null && all.Length > 0) return all[0];

            // 3. Fallback per discrepanze comuni di nomenclatura (_2 vs _frame2, _3 vs _frame3)
            string altName = null;
            if (name.EndsWith("_2")) altName = name.Substring(0, name.Length - 2) + "_frame2";
            else if (name.EndsWith("_3")) altName = name.Substring(0, name.Length - 3) + "_frame3";
            else if (name.Contains("_frame2")) altName = name.Replace("_frame2", "_2");
            else if (name.Contains("_frame3")) altName = name.Replace("_frame3", "_3");

            if (!string.IsNullOrEmpty(altName))
            {
                s = Resources.Load<Sprite>($"Sprites/{altName}");
                if (s != null) return s;

                all = Resources.LoadAll<Sprite>($"Sprites/{altName}");
                if (all != null && all.Length > 0) return all[0];
            }

            Debug.LogWarning($"[SpriteLoader] {gameObject.name}: impossibile trovare sprite per '{name}' (tentato anche '{altName}') in Assets/Resources/Sprites/");
            return null;
        }

        private void Update()
        {
            // Se non ci sono almeno 2 frame, esci subito (zero overhead per entità statiche)
            if (_frames == null || _frames.Length <= 1) return;

            _timer += Time.deltaTime;
            if (_timer >= _animationInterval)
            {
                _timer = 0f;
                _currentFrameIndex = (_currentFrameIndex + 1) % _frames.Length;
                _spriteRenderer.sprite = _frames[_currentFrameIndex];
            }
        }
    }
}
