using UnityEngine;
using KSP;

namespace Koko
{
    // Reconstructed. Handles syndicate init and static state reset.
    // once=false: fires per scene so OnDestroy catches each scene exit.

    // Runs in every game scene. Ensures syndicates are loaded and resets
    // static state when returning to main menu so a fresh game starts clean.
    [KSPAddon(KSPAddon.Startup.AllGameScenes, false)]
    public class SyndicateBootstrap : MonoBehaviour
    {
        void Start()
        {
            // Load() is guarded by the loaded flag — safe to call from multiple addons.
            SyndicateManager.Load();
            Debug.Log("[TimMod] SyndicateBootstrap: syndicates ready.");
        }

        void OnDestroy()
        {
            // Fires when leaving a game scene (e.g. returning to main menu).
            // Clears static state so the next game load reads fresh data.
            SyndicateManager.Reset();
            Debug.Log("[TimMod] SyndicateBootstrap: syndicate state reset.");
        }
    }
}
