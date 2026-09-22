using System.Collections;
using UnityEngine;
using MobileGame.Audio;
using MobileGame.Core;
using MobileGame.Utilities;

namespace MobileGame.Player
{
    /// <summary>
    /// Orchestratore dell'animazione cinematografica di morte del Player (Proposta 1: Cyber-Detonation).
    /// Gestisce bullet-time, sovraccarico reattore (jitter + flash cromatico), detonazione al plasma
    /// con shockwave e frammenti naniti a 360°, camera shake e feedback aptico.
    /// Zero allocazioni heap a runtime (AGENTS.md §4).
    /// </summary>
    public class PlayerDeathFX : MonoBehaviour
    {
        private SpriteRenderer _spriteRenderer;
        private Coroutine      _deathRoutine;

        // Particle systems procedurali
        private ParticleSystem _shockwavePs;
        private ParticleSystem _debrisPs;
        private ParticleSystem _sparksPs;

        private ParticleSystem.EmitParams _shockwaveEmitParams;
        private ParticleSystem.EmitParams _debrisEmitParams;
        private ParticleSystem.EmitParams _sparksEmitParams;

        // Colori sovraccarico reattore pre-allocati (zero boxing/alloc in loop)
        private static readonly Color COLOR_OVERLOAD_WHITE  = Color.white;
        private static readonly Color COLOR_OVERLOAD_CYAN   = new Color(0.0f, 1.0f, 1.0f, 1.0f);
        private static readonly Color COLOR_OVERLOAD_RED    = new Color(1.0f, 0.15f, 0.15f, 1.0f);
        private static readonly Color COLOR_OVERLOAD_ORANGE = new Color(1.0f, 0.75f, 0.1f, 1.0f);

        // Colori frammenti scafo nanobot
        private static readonly Color COLOR_DEBRIS_CYAN      = new Color(0.0f, 0.95f, 1.0f, 1.0f);
        private static readonly Color COLOR_DEBRIS_METAL     = new Color(0.40f, 0.50f, 0.60f, 1.0f);
        private static readonly Color COLOR_DEBRIS_DARKMETAL = new Color(0.15f, 0.20f, 0.28f, 1.0f);

        private void Awake()
        {
            _spriteRenderer = GetComponent<SpriteRenderer>();

            SetupParticleSystems();

            _shockwaveEmitParams = new ParticleSystem.EmitParams();
            _debrisEmitParams    = new ParticleSystem.EmitParams();
            _sparksEmitParams    = new ParticleSystem.EmitParams();
        }

        private void OnDestroy()
        {
            // Ripristino di sicurezza del timeScale se la scena viene distrutta durante la cutscene
            if (Time.timeScale < 1.0f)
            {
                Time.timeScale = 1.0f;
            }
        }

        // ── Creazione Particle Systems Procedurali (Zero Dipendenze da Prefab) ──

        private void SetupParticleSystems()
        {
            var fxRoot = new GameObject("PlayerDeathFX_Systems");
            fxRoot.transform.SetParent(transform);
            fxRoot.transform.localPosition = Vector3.zero;

            // 1. Shockwave Particle System
            _shockwavePs = CreateParticleSystem(fxRoot, "Shockwave_PS", 20);
            ConfigureShockwavePS(_shockwavePs);

            // 2. Nanobot Hull Debris Particle System
            _debrisPs = CreateParticleSystem(fxRoot, "Debris_PS", 200);
            ConfigureDebrisPS(_debrisPs);

            // 3. Plasma Sparks Particle System
            _sparksPs = CreateParticleSystem(fxRoot, "Sparks_PS", 150);
            ConfigureSparksPS(_sparksPs);
        }

        private ParticleSystem CreateParticleSystem(GameObject parent, string name, int maxParticles)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform);
            go.transform.localPosition = Vector3.zero;

            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.playOnAwake     = false;
            main.loop            = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode     = ParticleSystemScalingMode.Hierarchy;
            main.maxParticles    = maxParticles;

            var emission = ps.emission;
            emission.enabled = false;

            var shape = ps.shape;
            shape.enabled = false;

            return ps;
        }

        private void ConfigureShockwavePS(ParticleSystem ps)
        {
            var main = ps.main;
            main.startSpeed      = 0f;
            main.gravityModifier = 0f;

            // Espansione rapida dell'anello da 0.4f a 4.2f
            var sizeOverLife = ps.sizeOverLifetime;
            sizeOverLife.enabled = true;
            AnimationCurve sizeCurve = new AnimationCurve();
            sizeCurve.AddKey(0f, 0.4f);
            sizeCurve.AddKey(0.4f, 2.2f);
            sizeCurve.AddKey(1f, 4.4f);
            sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            // Dissolvenza trasparenza rapida
            var colOverLife = ps.colorOverLifetime;
            colOverLife.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(new Color(0.2f, 1f, 1f), 0f),
                    new GradientColorKey(new Color(0.0f, 0.6f, 1f), 0.7f),
                    new GradientColorKey(new Color(0.0f, 0.2f, 0.8f), 1f)
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(0.9f, 0f),
                    new GradientAlphaKey(0.6f, 0.4f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            colOverLife.color = grad;

            var psr = ps.GetComponent<ParticleSystemRenderer>();
            if (psr != null)
            {
                psr.sortingOrder = 90; // Sopra al gameplay
                Texture2D ringTex = CreateShockwaveRingTexture();
                Material mat = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default") ?? Shader.Find("Sprites/Default"));
                mat.mainTexture = ringTex;
                psr.material = mat;
            }
        }

        private void ConfigureDebrisPS(ParticleSystem ps)
        {
            var main = ps.main;
            main.startSpeed      = 0f;
            main.gravityModifier = 0f;

            // Resistenza e attrito che frenano i frammenti nell'arena biologica
            var limitVel = ps.limitVelocityOverLifetime;
            limitVel.enabled = true;
            limitVel.dampen  = 0.40f;
            limitVel.drag    = 2.8f;

            // Rotazione casuale continua
            var rotOverLife = ps.rotationOverLifetime;
            rotOverLife.enabled = true;
            rotOverLife.z       = new ParticleSystem.MinMaxCurve(-360f, 360f);

            // Riduzione dimensione progressiva fino a dissolversi
            var sizeOverLife = ps.sizeOverLifetime;
            sizeOverLife.enabled = true;
            AnimationCurve sizeCurve = new AnimationCurve();
            sizeCurve.AddKey(0f, 1f);
            sizeCurve.AddKey(0.65f, 0.85f);
            sizeCurve.AddKey(1f, 0f);
            sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            // Dissolvenza alpha
            var colOverLife = ps.colorOverLifetime;
            colOverLife.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) }
            );
            colOverLife.color = grad;

            var psr = ps.GetComponent<ParticleSystemRenderer>();
            if (psr != null)
            {
                psr.sortingOrder = 92;
                Texture2D chunkTex = CreateHullChunkTexture();
                Material mat = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default") ?? Shader.Find("Sprites/Default"));
                mat.mainTexture = chunkTex;
                psr.material = mat;
            }
        }

        private void ConfigureSparksPS(ParticleSystem ps)
        {
            var main = ps.main;
            main.startSpeed      = 0f;
            main.gravityModifier = 0f;

            var limitVel = ps.limitVelocityOverLifetime;
            limitVel.enabled = true;
            limitVel.dampen  = 0.30f;
            limitVel.drag    = 3.2f;

            var sizeOverLife = ps.sizeOverLifetime;
            sizeOverLife.enabled = true;
            AnimationCurve sizeCurve = new AnimationCurve();
            sizeCurve.AddKey(0f, 1f);
            sizeCurve.AddKey(1f, 0.1f);
            sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            var colOverLife = ps.colorOverLifetime;
            colOverLife.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(new Color(0.0f, 1f, 1f), 0.5f),
                    new GradientColorKey(new Color(0.0f, 0.4f, 1f), 1f)
                },
                new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.9f, 0.6f), new GradientAlphaKey(0f, 1f) }
            );
            colOverLife.color = grad;

            var psr = ps.GetComponent<ParticleSystemRenderer>();
            if (psr != null)
            {
                psr.sortingOrder = 93;
                Texture2D sparkTex = CreateSparkDotTexture();
                Material mat = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default") ?? Shader.Find("Sprites/Default"));
                mat.mainTexture = sparkTex;
                psr.material = mat;
            }
        }

        // ── Textures Procedurali Pixel-Perfect (generate solo una volta in Awake) ──

        private static Texture2D CreateShockwaveRingTexture()
        {
            int size = 64;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            float center = (size - 1) / 2f;
            float maxR   = center;

            Color transparent = new Color(1f, 1f, 1f, 0f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center)) / maxR;
                    // Anello cavo: raggio tra 0.65 e 0.95 con picco di intensità a 0.85
                    if (dist >= 0.55f && dist <= 1.0f)
                    {
                        float ringDist = Mathf.Abs(dist - 0.82f);
                        float alpha = Mathf.Clamp01(1f - (ringDist / 0.18f));
                        alpha = Mathf.Pow(alpha, 1.5f);
                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                    }
                    else
                    {
                        tex.SetPixel(x, y, transparent);
                    }
                }
            }
            tex.Apply();
            return tex;
        }

        private static Texture2D CreateHullChunkTexture()
        {
            int size = 8;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            Color border = new Color(0.05f, 0.08f, 0.12f, 1f);
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

        private static Texture2D CreateSparkDotTexture()
        {
            int size = 16;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            float center = (size - 1) / 2f;
            float maxR = center;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center)) / maxR;
                    float alpha = Mathf.Clamp01(1f - dist);
                    alpha = Mathf.Pow(alpha, 2f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
            tex.Apply();
            return tex;
        }

        // ── Sequenza Cinematografica di Morte ──────────────────────────────────

        /// <summary>
        /// Avvia la sequenza cinematografica di morte:
        /// 1. Bullet-time & Jitter sovraccarico cromatico reattore (0.38s)
        /// 2. Detonazione al plasma: shockwave ad anello, frammenti naniti a 360°, sparks, camera shake & haptic
        /// 3. Dispersione detriti (0.75s) e transizione alla schermata di Game Over.
        /// </summary>
        public void PlayDeathSequence(System.Action onComplete)
        {
            if (_deathRoutine != null)
                StopCoroutine(_deathRoutine);

            _deathRoutine = StartCoroutine(DeathRoutine(onComplete));
        }

        private IEnumerator DeathRoutine(System.Action onComplete)
        {
            Vector3 initialLocalPos = _spriteRenderer != null ? _spriteRenderer.transform.localPosition : Vector3.zero;

            // ══════════════════════════════════════════════════════════════════
            // FASE 1: BULLET-TIME & REATTORE IN SOVRACCARICO (0.38s unscaled)
            // ══════════════════════════════════════════════════════════════════
            Time.timeScale = 0.12f; // Bullet-time drammatico (mondo quasi congelato)

            // Scuotimento crescente e haptic medio
            CameraFollow.Instance?.TriggerShake(0.38f, 0.16f);
            HapticManager.MediumImpact();
            AudioManager.Instance?.PlayPlayerHit();

            float overloadDuration = 0.38f;
            float elapsed = 0f;

            while (elapsed < overloadDuration)
            {
                float dt = Time.unscaledDeltaTime;
                elapsed += dt;

                float progress = Mathf.Clamp01(elapsed / overloadDuration);

                // Jitter violento crescente sullo sprite del nanobot
                if (_spriteRenderer != null)
                {
                    float jitterMagnitude = Mathf.Lerp(0.04f, 0.22f, progress);
                    Vector2 jitterOffset  = Random.insideUnitCircle * jitterMagnitude;
                    _spriteRenderer.transform.localPosition = initialLocalPos + new Vector3(jitterOffset.x, jitterOffset.y, 0f);

                    // Flash cromatico a 28 Hz: bianco -> ciano -> rosso allarme -> arancione plasma
                    int colorPhase = Mathf.FloorToInt(elapsed * 28f) % 4;
                    switch (colorPhase)
                    {
                        case 0: _spriteRenderer.color = COLOR_OVERLOAD_WHITE;  break;
                        case 1: _spriteRenderer.color = COLOR_OVERLOAD_CYAN;   break;
                        case 2: _spriteRenderer.color = COLOR_OVERLOAD_RED;    break;
                        case 3: _spriteRenderer.color = COLOR_OVERLOAD_ORANGE; break;
                    }
                }

                yield return null;
            }

            // ══════════════════════════════════════════════════════════════════
            // FASE 2: DETONAZIONE AL PLASMA (ESPLOSIONE NANITI)
            // ══════════════════════════════════════════════════════════════════
            Time.timeScale = 1.0f; // Ripristino tempo a velocità piena per sprigionare l'energia!

            Vector3 deathPos = transform.position;

            // Nasconde lo sprite del nanobot e resetta posizione
            if (_spriteRenderer != null)
            {
                _spriteRenderer.transform.localPosition = initialLocalPos;
                _spriteRenderer.enabled = false;
            }

            // Camera shake sismico ad alto impatto e vibrazione pesante
            CameraFollow.Instance?.TriggerShake(0.55f, 0.44f);
            HapticManager.HeavyImpact();
            AudioManager.Instance?.PlayPlayerHit();

            // Emissione burst particellari procedurali
            EmitPlasmaDetonation(deathPos);

            // ══════════════════════════════════════════════════════════════════
            // FASE 3: DISPERSIONE FRAMMENTI E APERTURA GAME OVER (0.75s)
            // ══════════════════════════════════════════════════════════════════
            yield return new WaitForSecondsRealtime(0.75f);

            // Garanzia che timeScale sia esattamente a 1.0f
            Time.timeScale = 1.0f;

            _deathRoutine = null;
            onComplete?.Invoke();
        }

        private void EmitPlasmaDetonation(Vector3 pos)
        {
            // 1. Shockwave ad anello al plasma (onde d'urto concentriche)
            if (_shockwavePs != null)
            {
                // Anello principale
                _shockwaveEmitParams.position      = pos;
                _shockwaveEmitParams.startSize     = 0.5f;
                _shockwaveEmitParams.startLifetime = 0.42f;
                _shockwaveEmitParams.startColor    = new Color(0.0f, 1.0f, 1.0f, 1.0f);
                _shockwavePs.Emit(_shockwaveEmitParams, 1);

                // Secondo anello bianco più rapido
                _shockwaveEmitParams.startSize     = 0.3f;
                _shockwaveEmitParams.startLifetime = 0.30f;
                _shockwaveEmitParams.startColor    = Color.white;
                _shockwavePs.Emit(_shockwaveEmitParams, 1);
            }

            // 2. Frammenti dello scafo del nanobot a 360° (Debris)
            if (_debrisPs != null)
            {
                int debrisCount = 52;
                for (int i = 0; i < debrisCount; i++)
                {
                    float angle = (i / (float)debrisCount) * Mathf.PI * 2f + Random.Range(-0.15f, 0.15f);
                    float speed = Random.Range(2.8f, 7.8f);
                    Vector3 vel = new Vector3(Mathf.Cos(angle) * speed, Mathf.Sin(angle) * speed, 0f);

                    Color chunkCol;
                    int colMod = i % 5;
                    if (colMod == 0)      chunkCol = Color.white;
                    else if (colMod == 1) chunkCol = COLOR_DEBRIS_CYAN;
                    else if (colMod == 2) chunkCol = COLOR_DEBRIS_METAL;
                    else if (colMod == 3) chunkCol = COLOR_DEBRIS_DARKMETAL;
                    else                  chunkCol = new Color(0.1f, 0.85f, 1.0f, 1f);

                    _debrisEmitParams.position      = pos + (Vector3)(Random.insideUnitCircle * 0.18f);
                    _debrisEmitParams.velocity      = vel;
                    _debrisEmitParams.startColor    = chunkCol;
                    _debrisEmitParams.startSize     = Random.Range(0.14f, 0.28f);
                    _debrisEmitParams.startLifetime = Random.Range(0.55f, 0.80f);
                    _debrisEmitParams.rotation      = Random.Range(0f, 360f);

                    _debrisPs.Emit(_debrisEmitParams, 1);
                }
            }

            // 3. Scintille al plasma ad altissima velocità (Sparks)
            if (_sparksPs != null)
            {
                int sparksCount = 38;
                for (int i = 0; i < sparksCount; i++)
                {
                    float angle = Random.Range(0f, Mathf.PI * 2f);
                    float speed = Random.Range(5.5f, 11.5f);
                    Vector3 vel = new Vector3(Mathf.Cos(angle) * speed, Mathf.Sin(angle) * speed, 0f);

                    _sparksEmitParams.position      = pos;
                    _sparksEmitParams.velocity      = vel;
                    _sparksEmitParams.startColor    = (i % 2 == 0) ? Color.white : new Color(0.1f, 1.0f, 1.0f, 1.0f);
                    _sparksEmitParams.startSize     = Random.Range(0.08f, 0.15f);
                    _sparksEmitParams.startLifetime = Random.Range(0.30f, 0.50f);

                    _sparksPs.Emit(_sparksEmitParams, 1);
                }
            }
        }

        /// <summary>
        /// Resetta lo stato visivo al riavvio della partita.
        /// </summary>
        public void ResetDeathFX()
        {
            if (_deathRoutine != null)
            {
                StopCoroutine(_deathRoutine);
                _deathRoutine = null;
            }

            Time.timeScale = 1.0f;

            if (_spriteRenderer != null)
            {
                _spriteRenderer.transform.localPosition = Vector3.zero;
                _spriteRenderer.color                   = Color.white;
                _spriteRenderer.enabled                 = true;
            }
        }
    }
}
