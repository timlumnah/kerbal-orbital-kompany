// New file. In-flight HUD feedback for syndicate zone proximity.
//
// Detection zone (orange):
//   - Orange vignette on screen edges, slow pulse
//   - Filled orange triangle icon, top-center
//   - Entry arpeggio: C5 -> F5 -> C6 played in ~0.5s
//   - Exit arpeggio:  C6 -> F5 -> C5 (reverse)
//
// Danger zone (red):
//   - Red vignette, faster pulse
//   - Filled red hexagon icon replaces triangle
//   - D5 + F5 + G#5 chord loops, volume pulses with vignette
//
// No assets required. All audio is synthesized (sine waves).
// All visuals are procedural GL + GUI.DrawTexture.
//
// Only active in flight scene. No map-view rendering.

using System.Linq;
using UnityEngine;

namespace Koko
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class SyndicateHUD : MonoBehaviour
    {
        // Zone state
        private bool inDetection;
        private bool inDanger;
        private bool prevDetection;
        private bool prevDanger;

        // Pulse state (radians, accumulated in Update)
        private float pulsePhase;
        private const float PULSE_SPEED_DETECTION = 1.2f; // rad/s -- slow
        private const float PULSE_SPEED_DANGER    = 3.8f; // rad/s -- urgent

        // Audio -- two sources so arpeggio can overlap chord
        private AudioSource arpeggioSource;
        private AudioSource chordSource;
        private AudioClip   clipEntry;  // C5->F5->C6
        private AudioClip   clipExit;   // C6->F5->C5
        private AudioClip   clipChord;  // D5+F5+G#5 loop

        // GL material (shared with SyndicateMapOverlay if loaded first)
        private static Material glMat;

        // Note frequencies (equal temperament, A4=440 Hz)
        private const float C5  = 523.25f;
        private const float F5  = 698.46f;
        private const float C6  = 1046.50f;
        private const float D5  = 587.33f;
        private const float GS5 = 830.61f;

        void Start()
        {
            InitGLMaterial();
            BuildClips();

            arpeggioSource = gameObject.AddComponent<AudioSource>();
            arpeggioSource.spatialBlend = 0f;
            arpeggioSource.volume = 0.65f;
            arpeggioSource.playOnAwake = false;

            chordSource = gameObject.AddComponent<AudioSource>();
            chordSource.spatialBlend = 0f;
            chordSource.playOnAwake = false;
            chordSource.loop = true;
            chordSource.volume = 0f;
            chordSource.clip = clipChord;

            Debug.Log($"[TimMod] SyndicateHUD: ready. AllSyndicates.Count={SyndicateManager.AllSyndicates.Count}");
        }

        private float _diagTimer   = 0f;
        private float _stealthPct  = 1f;  // 1 = fully stealthy, 0 = fully detected
        private bool  _hasVessel   = false;

        void Update()
        {
            if (!HighLogic.LoadedSceneIsFlight) return;

            var v = FlightGlobals.ActiveVessel;
            if (v == null || v.mainBody == null)
            {
                _hasVessel = false;
                SetState(false, false);
                return;
            }
            _hasVessel = true;

            bool newDanger = false, newDetect = false;
            Vector3d vPos = v.GetWorldPos3D();

            // Diagnostic: log nearest location distance + effective radii every 20s.
            // Remove once HUD is confirmed working.
            _diagTimer += Time.deltaTime;
            if (_diagTimer >= 20f)
            {
                _diagTimer = 0f;
                var locs = SyndicateManager.GetLocationsForBody(v.mainBody.bodyName)
                    .Where(l => !l.destroyed).ToList();
                if (locs.Count == 0)
                {
                    Debug.Log($"[TimMod] SyndicateHUD diag: no locations on {v.mainBody.bodyName}. AllSyndicates.Count={SyndicateManager.AllSyndicates.Count}");
                }
                else
                {
                    SyndicateLocation nearest = null;
                    double nearDist = double.MaxValue;
                    foreach (var l in locs)
                    {
                        double tAlt = v.mainBody.TerrainAltitude(l.lat, l.lon, false);
                        double d = Vector3d.Distance(vPos, v.mainBody.GetWorldSurfacePosition(l.lat, l.lon, tAlt));
                        if (d < nearDist) { nearDist = d; nearest = l; }
                    }
                    Debug.Log($"[TimMod] SyndicateHUD diag: {locs.Count} loc(s) on {v.mainBody.bodyName} | " +
                              $"nearest={nearest.name} dist={nearDist:F0}m " +
                              $"detectR={nearest.EffectiveDetectionRadius:F0}m dangerR={nearest.EffectiveDangerRadius:F0}m | " +
                              $"inDetect={inDetection} inDanger={inDanger}");
                }
            }

            foreach (var loc in SyndicateManager.GetLocationsForBody(v.mainBody.bodyName))
            {
                if (loc.destroyed) continue;
                double tAlt2 = v.mainBody.TerrainAltitude(loc.lat, loc.lon, false);
                double dist = Vector3d.Distance(vPos, v.mainBody.GetWorldSurfacePosition(loc.lat, loc.lon, tAlt2));
                if (dist <= loc.EffectiveDangerRadius)    { newDanger = true; break; }
                if (dist <= loc.EffectiveDetectionRadius) newDetect = true;
            }

            _stealthPct = 1f - RandomPartExplosionUtil.ComputeSabotageChance(v);

            HandleTransitions(newDetect, newDanger);

            // Pulse advance
            if (inDetection || inDanger)
            {
                float speed = inDanger ? PULSE_SPEED_DANGER : PULSE_SPEED_DETECTION;
                pulsePhase += Time.deltaTime * speed;
            }
            else
            {
                pulsePhase = 0f;
            }

            // Danger chord volume follows pulse
            if (inDanger && chordSource.isPlaying)
            {
                float p = Pulse();
                chordSource.volume = 0.18f + p * 0.42f;
            }
        }

        private void HandleTransitions(bool newDetect, bool newDanger)
        {
            // Entered detection zone from outside (not danger)
            if (newDetect && !newDanger && !prevDetection && !prevDanger)
            {
                Debug.Log("[TimMod] SyndicateHUD: entered detection zone.");
                arpeggioSource.PlayOneShot(clipEntry);
            }

            // Exited all zones
            if (!newDetect && !newDanger && (prevDetection || prevDanger))
            {
                Debug.Log("[TimMod] SyndicateHUD: exited all zones.");
                arpeggioSource.PlayOneShot(clipExit);
                if (chordSource.isPlaying) chordSource.Stop();
            }

            // Entered danger zone
            if (newDanger && !prevDanger)
            {
                Debug.Log("[TimMod] SyndicateHUD: entered danger zone.");
                if (!chordSource.isPlaying) chordSource.Play();
            }

            // Left danger zone but still in detection
            if (!newDanger && prevDanger && newDetect)
            {
                if (chordSource.isPlaying) chordSource.Stop();
            }

            SetState(newDetect, newDanger);
        }

        private void SetState(bool detect, bool danger)
        {
            prevDetection = inDetection;
            prevDanger    = inDanger;
            inDetection   = detect;
            inDanger      = danger;
        }

        private float Pulse() => (Mathf.Sin(pulsePhase) + 1f) * 0.5f;

        void OnGUI()
        {
            if (!HighLogic.LoadedSceneIsFlight) return;
            if (Event.current.type != EventType.Repaint) return;

            // Stealth bar always visible in flight -- lets player check rating
            // before entering a zone, not just after.
            if (_hasVessel)
                DrawStealthBar(Screen.width - 198f, 8f, _stealthPct);

            // Eeloo fortress progress bar. On victory/defeat replace with status label.
            var cs = CampaignScenario.Instance;
            if (cs != null && cs.fortressStartUT > 0)
            {
                if (cs.Phase == CampaignPhase.Victory || cs.Phase == CampaignPhase.Defeat)
                    DrawFortressStatusLabel(Screen.width - 198f, 39f, cs.Phase);
                else
                    DrawFortressBar(Screen.width - 198f, 39f, cs.FortressProgressPct);
            }

            if (!inDetection && !inDanger) return;

            float p = Pulse();

            // -- Vignette --
            Color vc = inDanger
                ? new Color(1f, 0.05f, 0.05f, 0.08f + p * 0.20f)
                : new Color(1f, 0.55f, 0f,    0.04f + p * 0.12f);
            DrawVignette(vc);

            // -- Icon --
            float cx = Screen.width * 0.5f;
            float cy = 68f;
            float r  = 34f;
            Color ic = inDanger
                ? new Color(1f, 0.12f, 0.12f, 0.65f + p * 0.35f)
                : new Color(1f, 0.55f, 0f,    0.65f + p * 0.35f);

            if (inDanger)
                DrawFilledHexagon(cx, cy, r, ic);
            else
                DrawFilledTriangle(cx, cy, r, ic);

            // Label below icon
            string label = inDanger ? "DANGER ZONE" : "SYNDICATE DETECTED";
            DrawHUDLabel(cx, cy + r + 6f, label, ic);
        }

        // ---- Drawing helpers ----

        // Gradient vignette: GL quads with per-vertex alpha.
        // Edge vertices = full color, inner vertices = transparent.
        // Creates a smooth fade from screen border toward center.
        // GL.LoadPixelMatrix() coordinate system: (0,0)=bottom-left, Y increases up.
        private static void DrawVignette(Color edgeColor)
        {
            if (glMat == null) return;
            Color clear = new Color(edgeColor.r, edgeColor.g, edgeColor.b, 0f);
            float w = Screen.width, h = Screen.height, t = 150f;

            glMat.SetPass(0);
            GL.PushMatrix();
            GL.LoadPixelMatrix();
            GL.Begin(GL.QUADS);

            // Top strip (edge at y=h, fades down to y=h-t)
            GL.Color(edgeColor); GL.Vertex3(0, h,   0);
            GL.Color(edgeColor); GL.Vertex3(w, h,   0);
            GL.Color(clear);     GL.Vertex3(w, h-t, 0);
            GL.Color(clear);     GL.Vertex3(0, h-t, 0);

            // Bottom strip (edge at y=0, fades up to y=t)
            GL.Color(clear);     GL.Vertex3(0, t, 0);
            GL.Color(clear);     GL.Vertex3(w, t, 0);
            GL.Color(edgeColor); GL.Vertex3(w, 0, 0);
            GL.Color(edgeColor); GL.Vertex3(0, 0, 0);

            // Left strip (edge at x=0, fades right to x=t)
            GL.Color(edgeColor); GL.Vertex3(0, h-t, 0);
            GL.Color(edgeColor); GL.Vertex3(0, t,   0);
            GL.Color(clear);     GL.Vertex3(t, t,   0);
            GL.Color(clear);     GL.Vertex3(t, h-t, 0);

            // Right strip (edge at x=w, fades left to x=w-t)
            GL.Color(clear);     GL.Vertex3(w-t, h-t, 0);
            GL.Color(clear);     GL.Vertex3(w-t, t,   0);
            GL.Color(edgeColor); GL.Vertex3(w,   t,   0);
            GL.Color(edgeColor); GL.Vertex3(w,   h-t, 0);

            GL.End();
            GL.PopMatrix();
        }

        // Equilateral triangle pointing up, center at (guiX, guiY) GUI coords
        private void DrawFilledTriangle(float guiX, float guiY, float r, Color c)
        {
            if (glMat == null) return;
            float glY = Screen.height - guiY;
            glMat.SetPass(0);
            GL.PushMatrix();
            GL.LoadPixelMatrix();
            GL.Begin(GL.TRIANGLES);
            GL.Color(c);
            // Vertices at 90°, 210°, 330° from center
            float[] ang = { 90f, 210f, 330f };
            foreach (float a in ang)
            {
                float rad = a * Mathf.Deg2Rad;
                GL.Vertex3(guiX + r * Mathf.Cos(rad), glY + r * Mathf.Sin(rad), 0);
            }
            GL.End();
            GL.PopMatrix();
        }

        // Regular hexagon, center at (guiX, guiY) GUI coords
        private void DrawFilledHexagon(float guiX, float guiY, float r, Color c)
        {
            if (glMat == null) return;
            float glY = Screen.height - guiY;
            glMat.SetPass(0);
            GL.PushMatrix();
            GL.LoadPixelMatrix();
            GL.Begin(GL.TRIANGLES);
            GL.Color(c);
            for (int i = 0; i < 6; i++)
            {
                float a1 = i       * 60f * Mathf.Deg2Rad;
                float a2 = (i + 1) * 60f * Mathf.Deg2Rad;
                GL.Vertex3(guiX, glY, 0);
                GL.Vertex3(guiX + r * Mathf.Cos(a1), glY + r * Mathf.Sin(a1), 0);
                GL.Vertex3(guiX + r * Mathf.Cos(a2), glY + r * Mathf.Sin(a2), 0);
            }
            GL.End();
            GL.PopMatrix();
        }

        // stealthPct: 0 = fully detected, 1 = fully stealthy.
        // Bar: green > 0.6, yellow 0.3-0.6, red < 0.3.
        private static void DrawStealthBar(float cx, float guiY, float stealthPct)
        {
            const float barW = 180f;
            const float barH = 7f;
            const float labelH = 14f;
            float barX = cx - barW * 0.5f;

            // "STEALTH: 75%" label
            if (_stealthLabelStyle == null)
                _stealthLabelStyle = new GUIStyle { fontSize = 10 };
            _stealthLabelStyle.normal.textColor = new Color(1f, 1f, 1f, 0.85f);
            int pct = Mathf.RoundToInt(stealthPct * 100f);
            GUI.Label(new Rect(barX, guiY, barW, labelH), $"STEALTH: {pct}%", _stealthLabelStyle);

            float barTop = guiY + labelH + 2f;

            // Background
            GUI.color = new Color(0.15f, 0.15f, 0.15f, 0.75f);
            GUI.DrawTexture(new Rect(barX, barTop, barW, barH), Texture2D.whiteTexture);

            // Fill — color based on stealth level
            Color fill = stealthPct > 0.6f
                ? new Color(0.1f, 0.9f, 0.2f, 0.85f)   // green
                : stealthPct > 0.3f
                    ? new Color(1.0f, 0.8f, 0.0f, 0.85f) // yellow
                    : new Color(1.0f, 0.15f, 0.1f, 0.85f); // red

            GUI.color = fill;
            GUI.DrawTexture(new Rect(barX, barTop, barW * stealthPct, barH), Texture2D.whiteTexture);

            GUI.color = Color.white;
        }

        private static GUIStyle _stealthLabelStyle;

        // Static label shown after victory or defeat instead of the live progress bar.
        private static void DrawFortressStatusLabel(float cx, float guiY, CampaignPhase phase)
        {
            const float barW = 180f;
            float barX = cx - barW * 0.5f;
            if (_fortressLabelStyle == null)
                _fortressLabelStyle = new GUIStyle { fontSize = 10 };
            bool won = phase == CampaignPhase.Victory;
            _fortressLabelStyle.normal.textColor = won
                ? new Color(0.4f, 1f, 0.4f, 0.9f)
                : new Color(1f, 0.2f, 0.2f, 0.9f);
            GUI.Label(new Rect(barX, guiY, barW, 14f),
                won ? "SYNDICATE DEFEATED" : "FORTRESS COMPLETE",
                _fortressLabelStyle);
        }

        // Eeloo fortress progress bar. fortressPct: 0 = safe, 1 = fortress complete.
        // Color: green when low (good), yellow mid, red when high (danger).
        private static void DrawFortressBar(float cx, float guiY, float fortressPct)
        {
            const float barW   = 180f;
            const float barH   = 7f;
            const float labelH = 14f;
            float barX = cx - barW * 0.5f;

            if (_fortressLabelStyle == null)
                _fortressLabelStyle = new GUIStyle { fontSize = 10 };
            _fortressLabelStyle.normal.textColor = new Color(1f, 1f, 1f, 0.85f);
            int pct = Mathf.RoundToInt(fortressPct * 100f);
            GUI.Label(new Rect(barX, guiY, barW, labelH),
                      $"EELOO FORTRESS: {pct}%", _fortressLabelStyle);

            float barTop = guiY + labelH + 2f;

            // Background
            GUI.color = new Color(0.15f, 0.15f, 0.15f, 0.75f);
            GUI.DrawTexture(new Rect(barX, barTop, barW, barH), Texture2D.whiteTexture);

            // Fill -- color inverted from stealth bar (high % is bad)
            Color fill = fortressPct < 0.33f
                ? new Color(0.1f, 0.9f, 0.2f, 0.85f)    // green: fortress far from done
                : fortressPct < 0.66f
                    ? new Color(1.0f, 0.8f, 0.0f, 0.85f) // yellow: getting closer
                    : new Color(1.0f, 0.15f, 0.1f, 0.85f); // red: almost complete

            GUI.color = fill;
            GUI.DrawTexture(new Rect(barX, barTop, barW * fortressPct, barH), Texture2D.whiteTexture);

            GUI.color = Color.white;
        }

        private static GUIStyle _fortressLabelStyle;

        private static GUIStyle _hudLabelStyle;
        private static void DrawHUDLabel(float cx, float guiY, string text, Color c)
        {
            if (_hudLabelStyle == null)
                _hudLabelStyle = new GUIStyle { fontSize = 11 };
            _hudLabelStyle.normal.textColor = c;
            // Rect is centered on cx manually since TextAnchor requires TextRenderingModule
            GUI.Label(new Rect(cx - 80f, guiY, 160f, 18f), text, _hudLabelStyle);
        }

        // ---- Audio generation ----

        private void BuildClips()
        {
            // Entry/exit arpeggios: 30% faster (0.7x durations).
            // Was noteDur=0.10 noteSep=0.17; now 0.07 / 0.12.
            clipEntry = BuildArpeggio(new[]{ C5, F5, C6 }, noteDur: 0.07f, noteSep: 0.12f);
            clipExit  = BuildArpeggio(new[]{ C6, F5, C5 }, noteDur: 0.07f, noteSep: 0.12f);
            // Danger chord: D5 + F5 + G#5, 1.5s loop with clean endpoints
            clipChord = BuildChord(new[]{ D5, F5, GS5 }, duration: 1.5f);
        }

        // Generates a clip with sequential notes.
        // noteDur: length of each note in seconds.
        // noteSep: time between note starts (gap = noteSep - noteDur).
        private static AudioClip BuildArpeggio(float[] freqs, float noteDur, float noteSep, int rate = 44100)
        {
            float total = noteSep * (freqs.Length - 1) + noteDur;
            int   totalSamples = (int)(rate * total);
            float[] data = new float[totalSamples];

            int attackLen  = (int)(0.005f * rate); // 5ms attack
            int releaseLen = (int)(0.015f * rate); // 15ms release

            for (int n = 0; n < freqs.Length; n++)
            {
                int startS = (int)(n * noteSep * rate);
                int noteS  = (int)(noteDur * rate);

                for (int i = 0; i < noteS && startS + i < totalSamples; i++)
                {
                    float env = 1f;
                    if (i < attackLen)                env = (float)i / attackLen;
                    else if (i > noteS - releaseLen)  env = (float)(noteS - i) / releaseLen;

                    float t = i / (float)rate;
                    data[startS + i] += Mathf.Sin(2f * Mathf.PI * freqs[n] * t) * 0.5f * env;
                }
            }

            // Normalize to avoid clipping
            float peak = 0f;
            foreach (float s in data) { float a = Mathf.Abs(s); if (a > peak) peak = a; }
            if (peak > 1e-6f) for (int i = 0; i < data.Length; i++) data[i] /= peak;

            var clip = AudioClip.Create("arpeggio", totalSamples, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        // Generates a chord clip with all freqs mixed simultaneously.
        // 50ms fade in/out so loop point is click-free.
        private static AudioClip BuildChord(float[] freqs, float duration, int rate = 44100)
        {
            int   samples = (int)(rate * duration);
            float[] data  = new float[samples];
            int   fadeLen = (int)(0.05f * rate); // 50ms fade

            for (int i = 0; i < samples; i++)
            {
                float env = 1f;
                if (i < fadeLen)             env = (float)i / fadeLen;
                else if (i > samples - fadeLen) env = (float)(samples - i) / fadeLen;

                float t = i / (float)rate;
                float sum = 0f;
                foreach (float f in freqs)
                    sum += Mathf.Sin(2f * Mathf.PI * f * t);

                data[i] = (sum / freqs.Length) * 0.5f * env;
            }

            var clip = AudioClip.Create("chord", samples, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        // ---- GL material ----

        private static void InitGLMaterial()
        {
            if (glMat != null) return;
            Shader s = Shader.Find("Hidden/Internal-Colored")
                    ?? Shader.Find("Sprites/Default");
            if (s == null) { Debug.LogWarning("[TimMod] SyndicateHUD: no GL shader found."); return; }
            glMat = new Material(s) { hideFlags = HideFlags.HideAndDontSave };
            glMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            glMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            glMat.SetInt("_Cull",     (int)UnityEngine.Rendering.CullMode.Off);
            glMat.SetInt("_ZWrite",   0);
        }
    }
}
