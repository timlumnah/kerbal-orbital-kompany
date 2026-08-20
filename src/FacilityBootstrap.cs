// KSPAddon that upgrades facilities every time the player visits KSC.
//
// Why not KSPAddon.Startup.SpaceCentre?
//   Causes KSP's addon loader to search for a root GameObject named
//   "SpaceCentre" -- fails in some versions, floods log with errors.
//   AllGameScenes + explicit scene check is safer.
//
// Why not CareerBootstrap (ScenarioModule)?
//   MaxAllFacilities Path B uses Resources.FindObjectsOfTypeAll to find
//   UpgradeableFacility MonoBehaviours. Those only exist in the SpaceCentre
//   scene. ScenarioModule.OnLoad fires too early and finds nothing.
//
// Visual duplication fix (2026-05-28):
//   Path B's SetLevel(int) re-instantiates the building model on every call,
//   even when already at the target level. This caused buildings to appear
//   twice -- once in place, once rotated 90 degrees. Guard: check
//   GetFacilityLevel for Mission Control + Tracking Station before running.
//   If both are already at 1.0 (max), all facilities were upgraded in a
//   prior visit and we skip entirely.
//
// Reference: https://wiki.kerbalspaceprogram.com/wiki/Tracking_Station

using System.Collections;
using UnityEngine;

namespace Koko
{
    [KSPAddon(KSPAddon.Startup.AllGameScenes, false)]
    public class FacilityBootstrap : MonoBehaviour
    {
        void Start()
        {
            if (HighLogic.CurrentGame?.Mode != Game.Modes.CAREER) return;
            if (HighLogic.LoadedScene != GameScenes.SPACECENTER) return;
            StartCoroutine(UpgradeWhenReady());
        }

        private IEnumerator UpgradeWhenReady()
        {
            // Wait two frames for SpaceCentre scene to finish populating
            // facility GameObjects before MaxAllFacilities searches for them.
            yield return null;
            yield return null;

            // If Mission Control and Tracking Station are already at max,
            // all facilities were upgraded on a prior KSC visit -- skip.
            // Calling SetLevel again triggers model re-instantiation even
            // at the same level, causing visual duplication.
            float mc = ScenarioUpgradeableFacilities.GetFacilityLevel(SpaceCenterFacility.MissionControl);
            float ts = ScenarioUpgradeableFacilities.GetFacilityLevel(SpaceCenterFacility.TrackingStation);
            if (mc >= 1.0f && ts >= 1.0f)
            {
                Debug.Log("[TimMod] FacilityBootstrap: facilities already at max, skipping.");
                yield break;
            }

            Debug.Log("[TimMod] FacilityBootstrap: upgrading facilities.");
            CareerOverrides.MaxAllFacilities();
        }
    }
}
