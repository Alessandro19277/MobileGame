using UnityEngine;

namespace MobileGame.Audio
{
    /// <summary>
    /// Generatore procedurale di effetti sonori e musica Synthwave / Cyberpunk (Sintesi DSP in C#).
    /// Genera AudioClip PCM ad alta fedeltà a 44.1 kHz interamente in memoria senza file esterni.
    /// Eseguito una tantum all'avvio (zero allocazioni nei frame di gioco, AGENTS.md §4).
    /// </summary>
    public static class AudioSynthesizer
    {
        private const int SAMPLE_RATE = 44100;
        private const float TWO_PI = Mathf.PI * 2f;

        // ── Effetti Sonori SFX ─────────────────────────────────────────────────

        /// <summary>Laser ionico al plasma: sweep rapido ad alta energia con decadimento secco.</summary>
        public static AudioClip GenerateShoot()
        {
            float duration = 0.12f;
            int samples = (int)(SAMPLE_RATE * duration);
            float[] data = new float[samples];
            float phase = 0f;

            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)SAMPLE_RATE;
                float progress = t / duration;

                // Sweep esponenziale da 960 Hz a 160 Hz
                float freq = Mathf.Lerp(960f, 160f, Mathf.Pow(progress, 0.45f));
                phase += freq / SAMPLE_RATE;

                // 70% sinusoide + 30% onda a dente di sega per corpo laser
                float saw = (phase % 1f) * 2f - 1f;
                float sine = Mathf.Sin(phase * TWO_PI);
                float wave = (sine * 0.70f) + (saw * 0.30f);

                float env = Mathf.Exp(-progress * 7.5f);
                data[i] = wave * env * 0.85f;
            }

            var clip = AudioClip.Create("SFX_Shoot_Procedural", samples, 1, SAMPLE_RATE, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>Crunch / Sgretolamento biologico di virus e batteri sconfitti.</summary>
        public static AudioClip GenerateEnemyDie()
        {
            float duration = 0.20f;
            int samples = (int)(SAMPLE_RATE * duration);
            float[] data = new float[samples];
            float phase = 0f;

            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)SAMPLE_RATE;
                float progress = t / duration;

                float freq = Mathf.Lerp(280f, 45f, progress);
                phase += freq / SAMPLE_RATE;

                float noise = (Random.value * 2f - 1f);
                float sine  = Mathf.Sin(phase * TWO_PI);

                // Mix rumore bianco + sub-bass per sensazione di schiacciamento
                float wave = (sine * 0.45f) + (noise * 0.55f);
                float env  = Mathf.Pow(1f - progress, 2.2f);

                data[i] = wave * env * 0.90f;
            }

            var clip = AudioClip.Create("SFX_EnemyDie_Procedural", samples, 1, SAMPLE_RATE, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>Impatto scudo / colpo subito dal nanobot: glitch metallico allarmante.</summary>
        public static AudioClip GeneratePlayerHit()
        {
            float duration = 0.22f;
            int samples = (int)(SAMPLE_RATE * duration);
            float[] data = new float[samples];
            float phase = 0f;

            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)SAMPLE_RATE;
                float progress = t / duration;

                // Modulazione FM a 45 Hz per risonanza scafo metallico
                float mod = Mathf.Sin(t * 45f * TWO_PI) * 75f;
                float freq = 360f + mod;
                phase += freq / SAMPLE_RATE;

                float sine = Mathf.Sin(phase * TWO_PI);
                float square = Mathf.Sign(sine) * 0.35f;
                float wave = (sine * 0.65f) + square;

                float env = Mathf.Exp(-progress * 6.0f);
                data[i] = wave * env * 0.85f;
            }

            var clip = AudioClip.Create("SFX_PlayerHit_Procedural", samples, 1, SAMPLE_RATE, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>Chime di raccolta gemma XP: tono cristallino armonico a campana.</summary>
        public static AudioClip GenerateXpPickup()
        {
            float duration = 0.11f;
            int samples = (int)(SAMPLE_RATE * duration);
            float[] data = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)SAMPLE_RATE;
                float progress = t / duration;

                // Fondamentale 1046 Hz (Do6) + armonica brillante 2093 Hz (Do7)
                float sine1 = Mathf.Sin(t * 1046.5f * TWO_PI);
                float sine2 = Mathf.Sin(t * 2093.0f * TWO_PI);
                float wave  = (sine1 * 0.70f) + (sine2 * 0.30f);

                float env = Mathf.Exp(-progress * 8.5f);
                data[i] = wave * env * 0.75f;
            }

            var clip = AudioClip.Create("SFX_XpPickup_Procedural", samples, 1, SAMPLE_RATE, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>Ricarica nanobot / Health Cross: due toni armonici ascendenti con shimmer.</summary>
        public static AudioClip GenerateHeal()
        {
            float duration = 0.38f;
            int samples = (int)(SAMPLE_RATE * duration);
            float[] data = new float[samples];
            float phase1 = 0f;
            float phase2 = 0f;

            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)SAMPLE_RATE;
                float progress = t / duration;

                // Accordo che sale di tono: La4/Mi5 -> Do#5/Sol#5
                float f1 = Mathf.Lerp(440f, 554.37f, progress);
                float f2 = Mathf.Lerp(659.25f, 830.61f, progress);

                phase1 += f1 / SAMPLE_RATE;
                phase2 += f2 / SAMPLE_RATE;

                float wave = (Mathf.Sin(phase1 * TWO_PI) + Mathf.Sin(phase2 * TWO_PI)) * 0.5f;
                // Inviluppo a campana morbida
                float env = Mathf.Sin(progress * Mathf.PI);

                data[i] = wave * env * 0.80f;
            }

            var clip = AudioClip.Create("SFX_Heal_Procedural", samples, 1, SAMPLE_RATE, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>Level Up: arpeggio trionfale stile arcade a 4 note brillanti.</summary>
        public static AudioClip GenerateLevelUp()
        {
            float duration = 0.48f;
            int samples = (int)(SAMPLE_RATE * duration);
            float[] data = new float[samples];

            float[] freqs = { 523.25f, 659.25f, 783.99f, 1046.50f }; // C5, E5, G5, C6
            float noteDur = duration / freqs.Length;

            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)SAMPLE_RATE;
                int noteIdx = Mathf.Clamp((int)(t / noteDur), 0, freqs.Length - 1);
                float noteT = t - (noteIdx * noteDur);

                float freq = freqs[noteIdx];
                float sine = Mathf.Sin(noteT * freq * TWO_PI);
                float pulse = Mathf.Sign(sine) * 0.25f; // Tocco chiptune
                float wave = (sine * 0.75f) + pulse;

                float env = Mathf.Exp(-(noteT / noteDur) * 4.5f);
                data[i] = wave * env * 0.85f;
            }

            var clip = AudioClip.Create("SFX_LevelUp_Procedural", samples, 1, SAMPLE_RATE, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>Game Over: spegnimento energetico con caduta drammatica dei sistemi.</summary>
        public static AudioClip GenerateGameOver()
        {
            float duration = 0.85f;
            int samples = (int)(SAMPLE_RATE * duration);
            float[] data = new float[samples];
            float phase = 0f;

            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)SAMPLE_RATE;
                float progress = t / duration;

                float freq = Mathf.Lerp(340f, 40f, Mathf.Pow(progress, 0.7f));
                phase += freq / SAMPLE_RATE;

                float wave = Mathf.Sin(phase * TWO_PI) * 0.80f + ((phase % 1f) * 2f - 1f) * 0.20f;
                float env = Mathf.Pow(1f - progress, 1.8f);

                data[i] = wave * env * 0.90f;
            }

            var clip = AudioClip.Create("SFX_GameOver_Procedural", samples, 1, SAMPLE_RATE, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>Click UI / Beep digitale per i pulsanti.</summary>
        public static AudioClip GenerateButtonClick()
        {
            float duration = 0.035f;
            int samples = (int)(SAMPLE_RATE * duration);
            float[] data = new float[samples];
            float phase = 0f;

            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)SAMPLE_RATE;
                float progress = t / duration;

                float freq = Mathf.Lerp(1600f, 650f, progress);
                phase += freq / SAMPLE_RATE;

                float wave = Mathf.Sin(phase * TWO_PI);
                float env  = Mathf.Exp(-progress * 8f);

                data[i] = wave * env * 0.65f;
            }

            var clip = AudioClip.Create("SFX_BtnClick_Procedural", samples, 1, SAMPLE_RATE, false);
            clip.SetData(data, 0);
            return clip;
        }

        // ── Tracce Musicali BGM Looping ────────────────────────────────────────

        /// <summary>
        /// Colonna sonora Gameplay Synthwave (124 BPM, 8 battute = 7.7419s loop seamless).
        /// Include grancassa a 4/4, linea di basso analogica FM in La minore e arpeggio sci-fi.
        /// </summary>
        public static AudioClip GenerateGameplayBGM()
        {
            float bpm = 124f;
            float beatDur = 60f / bpm; // ~0.48387s
            int totalBeats = 16;       // 4 battute in 4/4
            float duration = beatDur * totalBeats; // ~7.742s

            int totalSamples = (int)(SAMPLE_RATE * duration);
            float[] data = new float[totalSamples];

            // Sequenza note di basso (La, Fa, Do, Sol in ottavi)
            float[] bassNotes = { 110.00f, 110.00f, 87.31f, 87.31f, 130.81f, 130.81f, 98.00f, 98.00f };

            // Sequenza arpeggiatore in sedicesimi (La minore melodico)
            float[] arpNotes = {
                440.00f, 523.25f, 659.25f, 880.00f,
                523.25f, 659.25f, 880.00f, 1046.50f,
                349.23f, 440.00f, 523.25f, 698.46f,
                392.00f, 493.88f, 587.33f, 783.99f
            };

            for (int i = 0; i < totalSamples; i++)
            {
                float t = i / (float)SAMPLE_RATE;
                float loopT = t % duration;

                float sample = 0f;

                // 1. Kick Drum a 4/4 sui quarti (battito incalzante)
                float beatT = loopT % beatDur;
                if (beatT < 0.16f)
                {
                    float kickProgress = beatT / 0.16f;
                    float kickFreq = Mathf.Lerp(145f, 38f, Mathf.Pow(kickProgress, 0.4f));
                    float kickWave = Mathf.Sin(beatT * kickFreq * TWO_PI);
                    float kickEnv  = Mathf.Exp(-kickProgress * 5.5f);
                    sample += kickWave * kickEnv * 0.45f;
                }

                // 2. Hi-Hat in levare a 1/8
                float halfBeat = beatDur * 0.5f;
                float hatT = loopT % halfBeat;
                bool isOffBeat = ((int)(loopT / halfBeat) % 2) == 1;
                if (isOffBeat && hatT < 0.05f)
                {
                    float hatProgress = hatT / 0.05f;
                    float noise = (Random.value * 2f - 1f);
                    float hatEnv = Mathf.Exp(-hatProgress * 8.0f);
                    sample += noise * hatEnv * 0.14f;
                }

                // 3. Linea di basso in ottavi
                int bassIdx = ((int)(loopT / (beatDur * 0.5f))) % bassNotes.Length;
                float bassNoteT = loopT % (beatDur * 0.5f);
                float bassFreq  = bassNotes[bassIdx];
                float bassSine  = Mathf.Sin(bassNoteT * bassFreq * TWO_PI);
                float bassSub   = Mathf.Sin(bassNoteT * (bassFreq * 0.5f) * TWO_PI) * 0.5f;
                float bassEnv   = Mathf.Exp(-bassNoteT * 5.0f);
                sample += (bassSine + bassSub) * bassEnv * 0.35f;

                // 4. Arpeggiatore in sedicesimi (1/16)
                float sixteenth = beatDur * 0.25f;
                int arpIdx = ((int)(loopT / sixteenth)) % arpNotes.Length;
                float arpNoteT = loopT % sixteenth;
                float arpFreq  = arpNotes[arpIdx];
                float arpSine  = Mathf.Sin(arpNoteT * arpFreq * TWO_PI);
                float arpEnv   = Mathf.Exp(-arpNoteT * 12.0f);
                sample += arpSine * arpEnv * 0.20f;

                // Soft Limiter / Saturation per evitare clipping audio
                data[i] = Mathf.Clamp(sample * 0.85f, -0.95f, 0.95f);
            }

            var clip = AudioClip.Create("BGM_Gameplay_Synthwave_Loop", totalSamples, 1, SAMPLE_RATE, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>
        /// Colonna sonora Menu Principale (Dark Ambient Cyber a 90 BPM, 10.66s loop seamless).
        /// Pad caldi e fluttuanti, accordi al synth distesi per atmosfera futuristica rilassata.
        /// </summary>
        public static AudioClip GenerateMenuBGM()
        {
            float bpm = 90f;
            float beatDur = 60f / bpm; // ~0.6667s
            int totalBeats = 16;
            float duration = beatDur * totalBeats; // ~10.667s

            int totalSamples = (int)(SAMPLE_RATE * duration);
            float[] data = new float[totalSamples];

            // Due accordi lunghi alternati: La minore 9 e Fa maggiore 7
            float[] chordA = { 220.00f, 261.63f, 329.63f, 493.88f }; // A3, C4, E4, B4
            float[] chordB = { 174.61f, 220.00f, 261.63f, 329.63f }; // F3, A3, C4, E4

            for (int i = 0; i < totalSamples; i++)
            {
                float t = i / (float)SAMPLE_RATE;
                float loopT = t % duration;

                bool isFirstHalf = loopT < (duration * 0.5f);
                float[] activeChord = isFirstHalf ? chordA : chordB;
                float chordT = isFirstHalf ? loopT : (loopT - (duration * 0.5f));
                float chordProg = chordT / (duration * 0.5f);

                float sample = 0f;

                // Pad corale synth a 4 voci
                for (int c = 0; c < activeChord.Length; c++)
                {
                    float f = activeChord[c];
                    // Piccolo detune stereo-like
                    float detune = (c % 2 == 0) ? 0.35f : -0.35f;
                    sample += Mathf.Sin(t * (f + detune) * TWO_PI) * 0.12f;
                }

                // Drone sub-bass costante
                sample += Mathf.Sin(t * 55.0f * TWO_PI) * 0.18f;

                // Shimmer lento / LFO
                float lfo = 0.85f + (Mathf.Sin(t * 0.75f * TWO_PI) * 0.15f);
                float env = Mathf.Sin(chordProg * Mathf.PI);

                data[i] = Mathf.Clamp(sample * env * lfo * 0.70f, -0.90f, 0.90f);
            }

            var clip = AudioClip.Create("BGM_Menu_Ambient_Loop", totalSamples, 1, SAMPLE_RATE, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
