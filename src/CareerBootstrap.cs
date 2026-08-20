using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using KSP;

namespace Koko
{
    // Attach to career/sandbox/science saves (AddToAllGames) and run in key scenes.
    [KSPScenario(
        ScenarioCreationOptions.AddToAllGames,
        GameScenes.SPACECENTER, GameScenes.FLIGHT, GameScenes.TRACKSTATION, GameScenes.EDITOR, GameScenes.MAINMENU)]
    public class CareerBootstrap : ScenarioModule
    {
        private const string FlagKey = "InvestigativeCareerBootApplied";

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);

            // OnLoad fires before RnD/facilities are initialized so direct calls
            // to ResearchAndDevelopment.Instance and ScenarioUpgradeableFacilities
            // return null and silently skip. Defer via coroutine until RnD is ready.
            if (node != null && node.HasValue(FlagKey))
            {
                Debug.Log("[TimMod] CareerBootstrap: already applied for this save.");
                return;
            }

            StartCoroutine(ApplyWhenReady(node));
        }

        private IEnumerator ApplyWhenReady(ConfigNode node)
        {
            // Wait for ResearchAndDevelopment to initialize (up to ~300 frames / ~5s).
            int waited = 0;
            while (ResearchAndDevelopment.Instance == null && waited < 300)
            {
                yield return null;
                waited++;
            }

            if (ResearchAndDevelopment.Instance == null)
            {
                Debug.LogWarning("[TimMod] CareerBootstrap: RnD never became ready, giving up.");
                yield break;
            }

            // One extra frame for stability before touching facilities.
            yield return null;

            TryApplyOnce(node);
        }

        public override void OnSave(ConfigNode node)
        {
            base.OnSave(node);
            // keep our applied flag persisted
            if (!node.HasValue(FlagKey))
                node.AddValue(FlagKey, "true");
        }

        private void TryApplyOnce(ConfigNode node)
        {
            try
            {
                if (node != null && node.HasValue(FlagKey))
                {
                    Debug.Log("[TimMod] CareerBootstrap: already applied for this save.");
                    return;
                }

                // Only apply in Career (your choice: we can also apply in Science/Sandbox)
                if (HighLogic.CurrentGame?.Mode == Game.Modes.CAREER)
                {
                    Debug.Log("[TimMod] CareerBootstrap: applying overrides…");

                    CareerOverrides.DisableAllStockContracts();
                    CareerOverrides.MaxAllFacilities();
                    CareerOverrides.UnlockEntireTechTree();

                    Debug.Log("[TimMod] CareerBootstrap: overrides complete.");
                }
                else
                {
                    // If you want this behavior in all modes, remove this branch.
                    Debug.Log("[TimMod] CareerBootstrap: not Career mode; skipping.");
                }

                // mark applied
                if (node != null && !node.HasValue(FlagKey))
                    node.AddValue(FlagKey, "true");
            }
            catch (Exception ex)
            {
                Debug.LogError("[TimMod] CareerBootstrap: exception while applying overrides: " + ex);
            }
        }
    }
}
