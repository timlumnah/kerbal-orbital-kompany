// New file. ScenarioModule that tracks in-flight weapons and fires strikes
// when their travel time elapses.
//
// Flow:
//   GameEvents.onVesselGoOnRails fires when a vessel leaves physics range.
//   If that vessel has ModuleWeapon + isArmed + a valid target:
//     -- calculate straight-line distance to target world position
//     -- travelTime = dist / estimatedSpeedMS
//     -- record arrivalUT = now + travelTime
//   OnUpdate checks each tracked weapon against current UT.
//   On arrival: SyndicateManager.LaunchAttack() marks location destroyed,
//   and a screen message confirms the strike.
//
// Works across time warp because Planetarium.GetUniversalTime() advances
// with warp. A weapon launched then time-warped past its ETA fires
// correctly on the next OnUpdate tick.

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Koko
{
    internal class InFlightWeapon
    {
        public Guid   vesselId;
        public double targetLat;
        public double targetLon;
        public string targetBody;
        public double arrivalUT;
        public string targetName; // display name for HUD; looked up at capture time
    }

    [KSPScenario(ScenarioCreationOptions.AddToAllGames,
        GameScenes.FLIGHT, GameScenes.SPACECENTER, GameScenes.TRACKSTATION)]
    public class WeaponManager : ScenarioModule
    {
        private readonly List<InFlightWeapon> inFlightWeapons = new List<InFlightWeapon>();

        public override void OnAwake()
        {
            base.OnAwake();
            GameEvents.onVesselGoOnRails.Add(OnVesselGoOnRails);
            Debug.Log("[TimMod] WeaponManager: active.");
        }

        void OnDestroy()
        {
            GameEvents.onVesselGoOnRails.Remove(OnVesselGoOnRails);
        }

        // Fires when a vessel leaves physics range and goes on rails.
        private void OnVesselGoOnRails(Vessel v)
        {
            if (v == null || v.Parts == null) return;

            // Find ModuleWeapon on any part
            ModuleWeapon mw = null;
            foreach (var p in v.Parts)
            {
                mw = p.FindModuleImplementing<ModuleWeapon>();
                if (mw != null) break;
            }

            if (mw == null || !mw.isArmed) return;
            if (string.IsNullOrEmpty(mw.targetBody)) return;

            // Avoid capturing the same vessel twice
            if (inFlightWeapons.Any(w => w.vesselId == v.id))
            {
                Debug.Log($"[TimMod] WeaponManager: {v.vesselName} already tracked, skipping.");
                return;
            }

            CelestialBody body = FlightGlobals.Bodies.FirstOrDefault(b =>
                string.Equals(b.bodyName, mw.targetBody, StringComparison.OrdinalIgnoreCase));
            if (body == null)
            {
                Debug.LogWarning($"[TimMod] WeaponManager: target body '{mw.targetBody}' not found.");
                return;
            }

            // Straight-line distance from weapon to target surface position.
            // Good approximation when weapon is nearby and target is on-surface.
            Vector3d targetWorld = body.GetWorldSurfacePosition(mw.targetLat, mw.targetLon, 0);
            double dist          = Vector3d.Distance(v.GetWorldPos3D(), targetWorld);
            double travelTime    = mw.estimatedSpeedMS > 0 ? dist / mw.estimatedSpeedMS : 60.0;
            double arrivalUT     = Planetarium.GetUniversalTime() + travelTime;

            // Look up location name for HUD display.
            var targetLoc = SyndicateManager.FindNearestLocation(
                new Vector2d(mw.targetLat, mw.targetLon), mw.targetBody);
            string locName = targetLoc?.name
                ?? $"{mw.targetBody} ({mw.targetLat:F2}, {mw.targetLon:F2})";

            inFlightWeapons.Add(new InFlightWeapon
            {
                vesselId   = v.id,
                targetLat  = mw.targetLat,
                targetLon  = mw.targetLon,
                targetBody = mw.targetBody,
                arrivalUT  = arrivalUT,
                targetName = locName
            });

            Debug.Log($"[TimMod] WeaponManager: weapon in flight -- " +
                      $"{mw.targetBody} ({mw.targetLat:F2}, {mw.targetLon:F2}) " +
                      $"dist={dist:F0}m travel={travelTime:F0}s arrivalUT={arrivalUT:F0}");

            ScreenMessages.PostScreenMessage(
                $"Weapon in flight. ETA: {FormatDuration(travelTime)}",
                5f, ScreenMessageStyle.UPPER_CENTER);
        }

        void Update()
        {
            if (inFlightWeapons.Count == 0) return;

            double now = Planetarium.GetUniversalTime();

            for (int i = inFlightWeapons.Count - 1; i >= 0; i--)
            {
                var w = inFlightWeapons[i];
                if (now < w.arrivalUT) continue;

                // Get name before LaunchAttack marks the location destroyed
                var nearest = SyndicateManager.FindNearestLocation(
                    new Vector2d(w.targetLat, w.targetLon), w.targetBody);
                string locName = nearest?.name ?? "target";

                SyndicateManager.LaunchAttack(
                    new Vector2d(w.targetLat, w.targetLon), w.targetBody);

                Debug.Log($"[TimMod] WeaponManager: strike confirmed -- {locName}");
                ScreenMessages.PostScreenMessage(
                    $"STRIKE CONFIRMED: {locName}", 6f, ScreenMessageStyle.UPPER_CENTER);

                // Despawn the weapon vessel after strike. Skip if it somehow became
                // the active vessel (safety guard -- shouldn't happen in normal play).
                Vessel strikeVessel = FlightGlobals.Vessels.FirstOrDefault(
                    v => v.id == w.vesselId);
                if (strikeVessel != null && strikeVessel != FlightGlobals.ActiveVessel)
                {
                    Debug.Log($"[TimMod] WeaponManager: despawning weapon vessel {strikeVessel.vesselName}");
                    strikeVessel.Die();
                }
                else if (strikeVessel == null)
                {
                    Debug.Log($"[TimMod] WeaponManager: weapon vessel already gone (vesselId={w.vesselId})");
                }

                inFlightWeapons.RemoveAt(i);
            }
        }

        public override void OnSave(ConfigNode node)
        {
            var root = node.AddNode("IN_FLIGHT_WEAPONS");
            foreach (var w in inFlightWeapons)
            {
                var wn = root.AddNode("WEAPON");
                wn.AddValue("vesselId",   w.vesselId.ToString());
                wn.AddValue("targetLat",  w.targetLat);
                wn.AddValue("targetLon",  w.targetLon);
                wn.AddValue("targetBody", w.targetBody);
                wn.AddValue("arrivalUT",  w.arrivalUT);
                wn.AddValue("targetName", w.targetName ?? "");
            }
        }

        public override void OnLoad(ConfigNode node)
        {
            inFlightWeapons.Clear();
            if (!node.HasNode("IN_FLIGHT_WEAPONS")) return;

            foreach (ConfigNode wn in node.GetNode("IN_FLIGHT_WEAPONS").GetNodes("WEAPON"))
            {
                try
                {
                    string tname = wn.GetValue("targetName") ?? "";
                    inFlightWeapons.Add(new InFlightWeapon
                    {
                        vesselId   = new Guid(wn.GetValue("vesselId")),
                        targetLat  = double.Parse(wn.GetValue("targetLat")),
                        targetLon  = double.Parse(wn.GetValue("targetLon")),
                        targetBody = wn.GetValue("targetBody"),
                        arrivalUT  = double.Parse(wn.GetValue("arrivalUT")),
                        targetName = string.IsNullOrEmpty(tname)
                            ? wn.GetValue("targetBody") : tname
                    });
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[TimMod] WeaponManager: failed to load weapon record: {ex.Message}");
                }
            }

            Debug.Log($"[TimMod] WeaponManager: loaded {inFlightWeapons.Count} in-flight weapon(s).");
        }

        // Persistent missile status panel. Shown in flight, SC, and TS so the
        // player can see ETA while time-warping from the space center.
        private GUIStyle _missileStyle;

        void OnGUI()
        {
            if (inFlightWeapons.Count == 0) return;
            if (Event.current.type != EventType.Repaint) return;

            var scene = HighLogic.LoadedScene;
            if (scene != GameScenes.FLIGHT &&
                scene != GameScenes.SPACECENTER &&
                scene != GameScenes.TRACKSTATION) return;

            // Repositioned to top-right below the stealth+fortress bars.
            // cx = Screen.width-198, barW=180, so left edge = Screen.width-288.
            // Stealth bar ~y=8-30, fortress bar ~y=39-61 -> missile starts at y=70.
            if (_missileStyle == null)
            {
                _missileStyle = new GUIStyle { fontSize = 12 };
                _missileStyle.normal.textColor = new Color(1f, 0.82f, 0.2f, 0.95f);
            }

            double now = Planetarium.GetUniversalTime();
            float barX = Screen.width - 288f;
            float y    = 70f;

            foreach (var w in inFlightWeapons)
            {
                double etaSec = w.arrivalUT - now;
                string etaStr = etaSec > 0 ? FormatDuration(etaSec) : "IMPACT IMMINENT";
                GUI.Label(new Rect(barX, y,      180f, 16f), "MISSILE IN ROUTE",         _missileStyle);
                GUI.Label(new Rect(barX, y + 16, 180f, 16f), $"Target: {w.targetName}",  _missileStyle);
                GUI.Label(new Rect(barX, y + 32, 180f, 16f), $"ETA: {etaStr}",           _missileStyle);
                y += 54f;
            }
        }

        private static string FormatDuration(double seconds)
        {
            int s = (int)seconds;
            if (s < 60)   return $"{s}s";
            if (s < 3600) return $"{s / 60}m {s % 60}s";
            return $"{s / 3600}h {(s % 3600) / 60}m";
        }
    }
}
