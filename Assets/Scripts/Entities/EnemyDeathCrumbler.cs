using UnityEngine;

namespace MobileGame.Entities
{
    /// <summary>
    /// Gestore globale dell effetto di sgretolamento dei nemici (Death Crumble).
    /// Usa un unico ParticleSystem in World Space con ParticleSystem.Emit (struct zero GC).
    /// </summary>
    public class EnemyDeathCrumbler : MonoBehaviour
    {
        public static EnemyDeathCrumbler Instance { get; private set; }

        private ParticleSystem _ps;
        private ParticleSystem.EmitParams _emitParams;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            SetupParticleSystem();
        }

        private void SetupParticleSystem()
        {
            _ps = gameObject.AddComponent<ParticleSystem>();
            var main = _ps.main;
            main.playOnAwake       = false;
            main.loop              = false;
            main.simulationSpace   = ParticleSystemSimulationSpace.World;
            main.scalingMode       = ParticleSystemScalingMode.Hierarchy;
            main.gravityModifier   = 1.5f; // Effetto gravita/caduta dei frammenti
            main.startSpeed        = 0f; // La velocita viene data individualmente per frammento
            main.maxParticles      = 500;

            var emission = _ps.emission;
            emission.enabled = false; // Solo emit manuale

            var shape = _ps.shape;
            shape.enabled = false;

            // Rallentamento / attrito dell aria
            var limitVel = _ps.limitVelocityOverLifetime;
            limitVel.enabled = true;
            limitVel.dampen  = 0.35f;
            limitVel.drag    = 2.2f;

            // Riduzione dimensione nel tempo (i frammenti si sgretolano fino a dissolversi)
            var sizeOverLife = _ps.sizeOverLifetime;
            sizeOverLife.enabled = true;
            AnimationCurve sizeCurve = new AnimationCurve();
            sizeCurve.AddKey(0f, 1f);
            sizeCurve.AddKey(0.7f, 0.7f);
            sizeCurve.AddKey(1f, 0f);
            sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            // Dissolvenza alpha
            var colOverLife = _ps.colorOverLifetime;
            colOverLife.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.75f), new GradientAlphaKey(0f, 1f) }
            );
            colOverLife.color = grad;

            // Renderer
            var psr = GetComponent<ParticleSystemRenderer>();
            if (psr != null)
            {
                psr.sortingOrder = 6; // Sopra ai nemici
                
                // Texture quadrata pixel-art
                Texture2D chunkTex = CreatePixelChunkTexture();
                Material mat = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default") ?? Shader.Find("Sprites/Default"));
                mat.mainTexture = chunkTex;
                psr.material = mat;
            }

            _emitParams = new ParticleSystem.EmitParams();
        }

        private static Texture2D CreatePixelChunkTexture()
        {
            int size = 8;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            Color border = new Color(0.1f, 0.1f, 0.15f, 1f);
            Color fill = Color.white;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool isBorder = (x == 0 || x == size - 1 || y == 0 || y == size - 1);
                    tex.SetPixel(x, y, isBorder ? border : fill);
                }
            }
            tex.Apply();
            return tex;
        }

        /// <summary>
        /// Spara una raffica di frammenti di sgretolamento nella posizione del nemico.
        /// Zero allocazioni GC nei frame di gioco (AGENTS.md §4).
        /// </summary>
        public static void SpawnCrumble(Vector2 position, Color enemyColor, float scale = 1f)
        {
            if (Instance == null)
            {
                var go = new GameObject("EnemyDeathCrumbler");
                go.AddComponent<EnemyDeathCrumbler>();
            }

            Instance?.DoSpawnCrumble(position, enemyColor, scale);
        }

        private void DoSpawnCrumble(Vector2 position, Color enemyColor, float scale)
        {
            if (_ps == null) return;

            int count = Mathf.Clamp((int)(16 * scale), 10, 26);
            for (int i = 0; i < count; i++)
            {
                float angle = (i / (float)count) * Mathf.PI * 2f + Random.Range(-0.25f, 0.25f);
                float speed = Random.Range(1.8f, 4.5f) * Mathf.Max(0.6f, scale);
                Vector3 vel = new Vector3(Mathf.Cos(angle) * speed, Mathf.Sin(angle) * speed, 0f);

                // Alterna frammenti del colore del nemico, frammenti piu scuri ed evidenziazioni chiare
                Color chunkColor;
                int mod = i % 4;
                if (mod == 0) chunkColor = Color.white;
                else if (mod == 1) chunkColor = enemyColor * 0.75f;
                else chunkColor = enemyColor;
                chunkColor.a = 1f;

                _emitParams.position      = position + (Vector2)(Random.insideUnitCircle * 0.2f * scale);
                _emitParams.velocity      = vel;
                _emitParams.startColor    = chunkColor;
                _emitParams.startSize     = Random.Range(0.12f, 0.22f) * Mathf.Max(0.7f, scale);
                _emitParams.startLifetime = Random.Range(0.35f, 0.55f);

                _ps.Emit(_emitParams, 1);
            }
        }
    }
}
