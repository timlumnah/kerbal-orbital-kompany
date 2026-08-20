// New file. PartModule that tags a vessel as a weapon and stores target data.
//
// Workflow:
//   1. Build vessel with TE_Warhead_Small (includes this module) as the nose.
//   2. Right-click in flight: "Set Target" -- auto-targets nearest revealed,
//      non-destroyed syndicate location on the current body.
//   3. "Arm Weapon" to arm.
//   4. Launch. Once vessel leaves physics range (goes on-rails), WeaponManager
//      captures it and simulates the strike after calculated travel time.
//
// estimatedSpeedMS: set in part CFG, used by WeaponManager for travel time calc.

using System.Linq;
using UnityEngine;

namespace Koko
{
    public class ModuleWeapon : PartModule
    {
        // Target coordinates -- persisted so they survive scene changes
        [KSPField(isPersistant = true)] public double targetLat  = 0.0;
        [KSPField(isPersistant = true)] public double targetLon  = 0.0;
        [KSPField(isPersistant = true)] public string targetBody = "";
        [KSPField(isPersistant = true)] public bool   isArmed    = false;

        // Set in part CFG; not persisted (use CFG value always)
        [KSPField] public float estimatedSpeedMS = 500f;

        // Display fields shown in right-click menu
        [KSPField(guiActive = true, guiName = "Target")]
        public string targetDisplay = "None set";

        [KSPField(guiActive = true, guiName = "Status")]
        public string statusDisplay = "Unarmed";

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            RefreshDisplays();
        }

        // Set target to nearest revealed, undestroyed location on current body.
        [KSPEvent(guiActive = true, guiName = "Set Target (Nearest Revealed)")]
        public void SetNearestTarget()
        {
            if (vessel == null || vessel.mainBody == null)
            {
                ScreenMessages.PostScreenMessage("No valid vessel.", 3f, ScreenMessageStyle.UPPER_CENTER);
                return;
            }

            string body = vessel.mainBody.bodyName;
            var vesselCoords = new Vector2d(vessel.latitude, vessel.longitude);

            var loc = SyndicateManager.GetLocationsForBody(body)
                .Where(l => !l.destroyed && l.revealed)
                .OrderBy(l => Vector2d.Distance(vesselCoords, new Vector2d(l.lat, l.lon)))
                .FirstOrDefault();

            if (loc == null)
            {
                ScreenMessages.PostScreenMessage(
                    "No revealed syndicate locations on this body. Explore first.",
                    4f, ScreenMessageStyle.UPPER_CENTER);
                return;
            }

            targetLat  = loc.lat;
            targetLon  = loc.lon;
            targetBody = body;

            RefreshDisplays();
            ScreenMessages.PostScreenMessage(
                $"Target set: {loc.name} ({loc.lat:F2}, {loc.lon:F2})",
                4f, ScreenMessageStyle.UPPER_CENTER);
            Debug.Log($"[TimMod] ModuleWeapon: target set to {loc.name} on {body}");
        }

        [KSPEvent(guiActive = true, guiName = "Arm Weapon")]
        public void ToggleArmed()
        {
            if (!isArmed && string.IsNullOrEmpty(targetBody))
            {
                ScreenMessages.PostScreenMessage(
                    "Set a target before arming.", 3f, ScreenMessageStyle.UPPER_CENTER);
                return;
            }

            isArmed = !isArmed;
            RefreshDisplays();

            ScreenMessages.PostScreenMessage(
                isArmed ? "Weapon ARMED." : "Weapon disarmed.",
                3f, ScreenMessageStyle.UPPER_CENTER);
        }

        private void RefreshDisplays()
        {
            targetDisplay = string.IsNullOrEmpty(targetBody)
                ? "None set"
                : $"{targetBody} ({targetLat:F2}, {targetLon:F2})";

            statusDisplay = isArmed ? "ARMED" : "Unarmed";
            Events["ToggleArmed"].guiName = isArmed ? "Disarm Weapon" : "Arm Weapon";
        }
    }
}
