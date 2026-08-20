// PartModule placed on the mk1-3 pod of the syndicate outpost craft.
// Presence of this module identifies the vessel as a syndicate craft.
//
// Sabotage mechanic:
//   Engineer on EVA approaches within 5m, right-clicks -> "Sabotage Systems"
//   Three chain explosions (ExplodeOne x3) then vessel.Die().
//   SyndicateManager.LaunchAttack fires immediately on click so the
//   location is marked destroyed even if the vessel coroutine fails.

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Koko
{
    public class ModuleSyndicateCraft : PartModule
    {
        [KSPEvent(
            guiActive          = false,
            guiActiveUnfocused = true,
            externalToEVAOnly  = true,
            unfocusedRange     = 5f,
            guiName            = "Sabotage Systems")]
        public void SabotageEvent()
        {
            // Must be an EVA engineer
            Vessel eva = FlightGlobals.ActiveVessel;
            if (eva == null || eva.vesselType != VesselType.EVA)
            {
                ScreenMessages.PostScreenMessage(
                    "Must be on EVA to sabotage.", 3f, ScreenMessageStyle.UPPER_CENTER);
                return;
            }

            var crew = eva.GetVesselCrew();
            if (crew.Count == 0 || crew[0].trait != "Engineer")
            {
                ScreenMessages.PostScreenMessage(
                    "Requires an engineer Kerbal.", 3f, ScreenMessageStyle.UPPER_CENTER);
                return;
            }

            // Disable button immediately -- no double-firing
            Events["SabotageEvent"].active = false;

            // Mark location destroyed before the explosion coroutine
            // (in case coroutine fails, the game state is still correct)
            SyndicateManager.LaunchAttack(
                new Vector2d(vessel.latitude, vessel.longitude),
                vessel.mainBody.bodyName);

            ScreenMessages.PostScreenMessage(
                "Syndicate systems sabotaged.", 5f, ScreenMessageStyle.UPPER_CENTER);

            StartCoroutine(ExplodeAndDespawn());
        }

        // Chain three ExplodeOne pops then Die() the vessel.
        private IEnumerator ExplodeAndDespawn()
        {
            for (int i = 0; i < 3; i++)
            {
                if (vessel != null)
                    RandomPartExplosionUtil.ExplodeOne(vessel);
                yield return new WaitForSeconds(0.35f);
            }

            if (vessel != null && vessel != FlightGlobals.ActiveVessel)
                vessel.Die();
        }
    }
}
