// New file. Draws syndicate zone rings on the KSP map view.
//
// Rendering approach:
//   OnRenderObject() -- GL.LINES circles in scaled space, filtered to
//   PlanetariumCamera only. Circle vertices computed from lat/lon using
//   flat-earth angular offset approximation (accurate for small radii).
//
//   OnGUI() -- name labels for fully-revealed locations, projected from
//   scaled space to screen space via PlanetariumCamera.WorldToScreenPoint.
//
// What is drawn:
//   discovered && !revealed  -- orange ring at innerRadius (uncertain area)
//   revealed                 -- red ring at revealRadius  + name label
//   destroyed                -- nothing
//   not discovered           -- nothing (player hasn't found it yet)

using System;
using System.Linq;
using UnityEngine;

namespace Koko
{
    [KSPAddon(KSPAddon.Startup.AllGameScenes, false)]
    public class SyndicateMapOverlay : MonoBehaviour
    {
        private static Material lineMat;
        private GUIStyle labelStyle;

        private const int SEGMENTS = 48; // circle smoothness

        // Colors
        private static readonly Color colorApproxDiscovered = new Color(1f, 1f, 0f, 0.55f); // yellow ring outline
        private static readonly Color colorApproxFill = new Color(1f, 1f, 0f, 0.08f); // yellow filled zone (very transparent)
        private static readonly Color colorDiscovered = new Color(1f, 0.55f, 0f, 0.75f); // orange
        private static readonly Color colorRevealed   = new Color(1f, 0.15f, 0.15f, 0.9f); // red

        // Eeloo fortress ring: radius in meters, drawn at equatorial coordinates.
        private const double FORTRESS_RING_RADIUS = 25000.0; // 25km visual ring on Eeloo

        void Start()
        {
            InitLineMaterial();
        }

        private static void InitLineMaterial()
        {
            if (lineMat != null) return;

            // Hidden/Internal-Colored is a Unity built-in, always available.
            // Falls back to Particles/Additive if missing (shouldn't happen).
            Shader shader = Shader.Find("Hidden/Internal-Colored")
                         ?? Shader.Find("Particles/Additive")
                         ?? Shader.Find("Sprites/Default");

            if (shader == null)
            {
                Debug.LogWarning("[TimMod] SyndicateMapOverlay: could not find line shader.");
                return;
            }

            lineMat = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            lineMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            lineMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            lineMat.SetInt("_Cull",     (int)UnityEngine.Rendering.CullMode.Off);
            lineMat.SetInt("_ZWrite",   0);
        }

        void OnRenderObject()
        {
            if (!MapView.MapIsEnabled) return;
            if (Camera.current != PlanetariumCamera.Camera) return;
            if (lineMat == null) return;
            if (FlightGlobals.Bodies == null) return;
            if (SyndicateManager.AllSyndicates == null || SyndicateManager.AllSyndicates.Count == 0) return;

            lineMat.SetPass(0);

            // Pass 1: filled approx zone (TRIANGLES, under outlines).
            // Semi-transparent yellow shade = "signals detected somewhere in here".
            GL.PushMatrix();
            GL.Begin(GL.TRIANGLES);
            foreach (var syn in SyndicateManager.AllSyndicates)
            {
                foreach (var loc in syn.locations)
                {
                    if (loc.destroyed || !loc.approxDiscovered || loc.discovered) continue;
                    CelestialBody body = FlightGlobals.Bodies.FirstOrDefault(b =>
                        string.Equals(b.bodyName, loc.body, StringComparison.OrdinalIgnoreCase));
                    if (body == null) continue;
                    GL.Color(colorApproxFill);
                    DrawFilledSurfaceCircle(body, loc.approxLat, loc.approxLon, 100000.0);
                }
            }
            GL.End();
            GL.PopMatrix();

            // Pass 2: ring outlines (LINES).
            GL.PushMatrix();
            GL.Begin(GL.LINES);

            foreach (var syn in SyndicateManager.AllSyndicates)
            {
                foreach (var loc in syn.locations)
                {
                    if (loc.destroyed) continue;

                    CelestialBody body = FlightGlobals.Bodies.FirstOrDefault(b =>
                        string.Equals(b.bodyName, loc.body, StringComparison.OrdinalIgnoreCase));
                    if (body == null) continue;

                    // Yellow outline ring at edge of approx zone (100km, offset center).
                    // Disappears once orange/red takes over on physical approach.
                    if (loc.approxDiscovered && !loc.discovered)
                    {
                        GL.Color(colorApproxDiscovered);
                        DrawSurfaceCircle(body, loc.approxLat, loc.approxLon, 100000.0);
                        continue;
                    }

                    if (!loc.discovered) continue;

                    if (loc.revealed)
                    {
                        // Solid red ring at revealRadius -- exact location confirmed
                        GL.Color(colorRevealed);
                        DrawSurfaceCircle(body, loc.lat, loc.lon, loc.EffectiveRevealRadius);
                    }
                    else
                    {
                        // Orange ring at innerRadius -- general area known, exact spot not yet
                        GL.Color(colorDiscovered);
                        DrawSurfaceCircle(body, loc.lat, loc.lon, loc.EffectiveDetectionRadius);
                    }
                }
            }

            // Eeloo fortress ring: shown once the fortress timer has started.
            // Color fades from gray (safe) through orange to red (critical).
            var cs = CampaignScenario.Instance;
            // Hide ring after victory (Syndicate defeated, no deadline remains).
            if (cs != null && cs.fortressStartUT > 0 && cs.Phase != CampaignPhase.Victory)
            {
                CelestialBody eeloo = FlightGlobals.Bodies.FirstOrDefault(b =>
                    string.Equals(b.bodyName, "Eeloo", StringComparison.OrdinalIgnoreCase));
                if (eeloo != null)
                {
                    float p = cs.FortressProgressPct;
                    Color fc = p < 0.5f
                        ? Color.Lerp(new Color(0.5f, 0.5f, 0.5f, 0.55f),
                                     new Color(1.0f, 0.55f, 0.0f, 0.8f), p * 2f)
                        : Color.Lerp(new Color(1.0f, 0.55f, 0.0f, 0.8f),
                                     new Color(1.0f, 0.1f,  0.1f, 1.0f), (p - 0.5f) * 2f);
                    GL.Color(fc);
                    DrawSurfaceCircle(eeloo, 0.0, 0.0, FORTRESS_RING_RADIUS);
                }
            }

            GL.End();
            GL.PopMatrix();
        }

        void OnGUI()
        {
            if (!MapView.MapIsEnabled) return;
            if (Event.current.type != EventType.Repaint) return;
            if (PlanetariumCamera.Camera == null) return;
            if (FlightGlobals.Bodies == null) return;
            if (SyndicateManager.AllSyndicates == null) return;

            foreach (var syn in SyndicateManager.AllSyndicates)
            {
                foreach (var loc in syn.locations)
                {
                    if (loc.destroyed || !loc.revealed) continue;

                    CelestialBody body = FlightGlobals.Bodies.FirstOrDefault(b =>
                        string.Equals(b.bodyName, loc.body, StringComparison.OrdinalIgnoreCase));
                    if (body == null) continue;

                    Vector3d worldPos  = body.GetWorldSurfacePosition(loc.lat, loc.lon, 0);
                    Vector3  scaledPos = ScaledSpace.LocalToScaledSpace(worldPos);
                    Vector3  screenPos = PlanetariumCamera.Camera.WorldToScreenPoint(scaledPos);

                    if (screenPos.z < 0) continue; // behind camera

                    // KSP screen origin is bottom-left; GUI origin is top-left
                    float guiX = screenPos.x + 8f;
                    float guiY = Screen.height - screenPos.y - 8f;

                    GUI.Label(new Rect(guiX, guiY, 220f, 20f), loc.name, GetLabelStyle());
                }
            }

            // Eeloo fortress progress label -- hidden after victory.
            var cs = CampaignScenario.Instance;
            if (cs != null && cs.fortressStartUT > 0 && cs.Phase != CampaignPhase.Victory)
            {
                CelestialBody eeloo = FlightGlobals.Bodies.FirstOrDefault(b =>
                    string.Equals(b.bodyName, "Eeloo", StringComparison.OrdinalIgnoreCase));
                if (eeloo != null)
                    DrawFortressLabel(eeloo, cs.FortressProgressPct);
            }
        }

        // Draw N line segments forming a circle of radiusM meters on the body surface.
        // Uses angular offset from center lat/lon -- accurate when radius << body radius.
        private static void DrawSurfaceCircle(CelestialBody body, double lat, double lon, double radiusM)
        {
            double bodyR  = body.Radius;
            if (bodyR <= 0 || radiusM <= 0) return;

            double angR   = radiusM / bodyR;          // angular radius (radians)
            double latRad = lat * (Math.PI / 180.0);
            double cosLat = Math.Cos(latRad);

            Vector3? prev = null;

            for (int i = 0; i <= SEGMENTS; i++)
            {
                double angle = 2.0 * Math.PI * i / SEGMENTS;
                double dlat  = angR * Math.Cos(angle);
                double dlon  = (Math.Abs(cosLat) > 1e-9)
                               ? angR * Math.Sin(angle) / cosLat
                               : 0.0;

                double ptLat = lat + dlat * (180.0 / Math.PI);
                double ptLon = lon + dlon * (180.0 / Math.PI);

                Vector3d worldPt  = body.GetWorldSurfacePosition(ptLat, ptLon, 0);
                Vector3  scaledPt = ScaledSpace.LocalToScaledSpace(worldPt);

                if (prev.HasValue)
                {
                    GL.Vertex(prev.Value);
                    GL.Vertex(scaledPt);
                }
                prev = scaledPt;
            }
        }

        // Filled circle using GL.TRIANGLES fan from center.
        // Must be called inside GL.Begin(GL.TRIANGLES) / GL.End().
        private static void DrawFilledSurfaceCircle(CelestialBody body, double lat, double lon, double radiusM)
        {
            double bodyR = body.Radius;
            if (bodyR <= 0 || radiusM <= 0) return;

            double angR   = radiusM / bodyR;
            double latRad = lat * (Math.PI / 180.0);
            double cosLat = Math.Cos(latRad);

            // Precompute edge points
            var edge = new Vector3[SEGMENTS + 1];
            for (int i = 0; i <= SEGMENTS; i++)
            {
                double angle = 2.0 * Math.PI * i / SEGMENTS;
                double dlat  = angR * Math.Cos(angle);
                double dlon  = Math.Abs(cosLat) > 1e-9 ? angR * Math.Sin(angle) / cosLat : 0.0;
                double ptLat = lat + dlat * (180.0 / Math.PI);
                double ptLon = lon + dlon * (180.0 / Math.PI);
                Vector3d worldPt = body.GetWorldSurfacePosition(ptLat, ptLon, 0);
                edge[i] = ScaledSpace.LocalToScaledSpace(worldPt);
            }

            Vector3d centerWorld  = body.GetWorldSurfacePosition(lat, lon, 0);
            Vector3  centerScaled = ScaledSpace.LocalToScaledSpace(centerWorld);

            for (int i = 0; i < SEGMENTS; i++)
            {
                GL.Vertex(centerScaled);
                GL.Vertex(edge[i]);
                GL.Vertex(edge[i + 1]);
            }
        }

        // Eeloo fortress label: percentage shown near Eeloo in map view.
        private void DrawFortressLabel(CelestialBody eeloo, float progress)
        {
            Vector3d worldPos  = eeloo.GetWorldSurfacePosition(0.0, 0.0, 0);
            Vector3  scaledPos = ScaledSpace.LocalToScaledSpace(worldPos);
            Vector3  screenPos = PlanetariumCamera.Camera.WorldToScreenPoint(scaledPos);
            if (screenPos.z < 0) return;

            float guiX = screenPos.x + 10f;
            float guiY = Screen.height - screenPos.y - 8f;
            int pct    = Mathf.RoundToInt(progress * 100f);

            Color lc = progress < 0.5f
                ? Color.Lerp(new Color(0.7f, 0.7f, 0.7f, 0.9f),
                             new Color(1.0f, 0.6f, 0.0f, 1.0f), progress * 2f)
                : Color.Lerp(new Color(1.0f, 0.6f, 0.0f, 1.0f),
                             new Color(1.0f, 0.2f, 0.2f, 1.0f), (progress - 0.5f) * 2f);

            if (_fortressLabelStyle == null)
                _fortressLabelStyle = new GUIStyle { fontSize = 11 };
            _fortressLabelStyle.normal.textColor = lc;

            GUI.Label(new Rect(guiX, guiY, 200f, 18f),
                      $"EELOO FORTRESS: {pct}%", _fortressLabelStyle);
        }

        private GUIStyle _fortressLabelStyle;

        // Lazy init -- GUIStyle must be built inside OnGUI, not Start()
        private GUIStyle GetLabelStyle()
        {
            if (labelStyle == null)
            {
                labelStyle = new GUIStyle
                {
                    fontSize = 12,
                    normal   = { textColor = colorRevealed }
                };
            }
            return labelStyle;
        }
    }
}
