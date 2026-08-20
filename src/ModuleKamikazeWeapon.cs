// New file. Early-game kamikaze weapon: pilot or drone flies directly into
// a Syndicate location. No WeaponManager on-rails system required.
//
// Arm via right-click before launch. On collision the part fires
// SyndicateManager.LaunchAttack() at the vessel's current coordinates.
//
// Requires a revealed location within strikeRangeM at the moment of impact.
// This prevents accidental triggers from random crashes far from any target.
//
// Uses GameEvents.onCrash (KSP event, no PhysicsModule dependency).

using UnityEngine;

namespace Koko
{
    public class ModuleKamikazeWeapon : PartModule
    {
        [KSPField(isPersistant = true)]
        public bool isArmed = false;

        // Max distance to nearest revealed location for impact to count as a strike
        [KSPField]
        public float strikeRangeM = 15000f;

        [KSPEvent(guiActive = true, guiActiveEditor = false,
                  guiName = "Arm (Kamikaze)", guiActiveUnfocused = false)]
        public void ArmWeapon()
        {
            isArmed = true;
            UpdateGUI();
            ScreenMessages.PostScreenMessage(
                "Kamikaze warhead armed. Impact will trigger strike.",
                3f, ScreenMessageStyle.UPPER_CENTER);
            Debug.Log("[TimMod] ModuleKamikazeWeapon: armed.");
        }

        [KSPEvent(guiActive = true, guiActiveEditor = false,
                  guiName = "Disarm", active = false)]
        public void DisarmWeapon()
        {
            isArmed = false;
            UpdateGUI();
            Debug.Log("[TimMod] ModuleKamikazeWeapon: disarmed.");
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            UpdateGUI();
            GameEvents.onCrash.Add(OnCrash);
        }

        void OnDestroy()
        {
            GameEvents.onCrash.Remove(OnCrash);
        }

        private void UpdateGUI()
        {
            Events["ArmWeapon"].active    = !isArmed;
            Events["DisarmWeapon"].active = isArmed;
            Events["ArmWeapon"].guiName   = isArmed ? "ARMED" : "Arm (Kamikaze)";
        }

        private void OnCrash(EventReport e)
        {
            if (!isArmed) return;
            if (e.origin != part) return; // only fire for this specific part
            if (vessel == null || vessel.mainBody == null) return;

            string bodyName = vessel.mainBody.bodyName;
            var coords = new Vector2d(vessel.latitude, vessel.longitude);

            var nearest = SyndicateManager.FindNearestLocation(coords, bodyName);
            if (nearest == null || !nearest.revealed)
            {
                Debug.Log("[TimMod] ModuleKamikazeWeapon: impact -- no revealed location in range.");
                return;
            }

            Vector3d locWorld = vessel.mainBody.GetWorldSurfacePosition(
                nearest.lat, nearest.lon, 0);
            double dist = Vector3d.Distance(vessel.GetWorldPos3D(), locWorld);

            if (dist > strikeRangeM)
            {
                Debug.Log($"[TimMod] ModuleKamikazeWeapon: impact too far from target " +
                          $"({dist:F0}m > {strikeRangeM}m). No strike.");
                return;
            }

            SyndicateManager.LaunchAttack(coords, bodyName);
            Debug.Log($"[TimMod] ModuleKamikazeWeapon: strike confirmed -- {nearest.name}");
            ScreenMessages.PostScreenMessage(
                $"KAMIKAZE STRIKE CONFIRMED: {nearest.name}",
                6f, ScreenMessageStyle.UPPER_CENTER);
        }
    }
}
