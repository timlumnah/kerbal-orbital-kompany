using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using KSP;

namespace Koko
{
    [KSPAddon(KSPAddon.Startup.AllGameScenes, false)]
    public class RandomFacilityDestroyer : MonoBehaviour
    {
        private float nextDestructionTime;
        private float interval     = 600f; // real seconds between checks (default 10 min)
        private float destroyChance = 0.15f; // probability per check (default 15%)
        private System.Random rng = new System.Random();

        // interval and destroyChance now read from SYNDICATE_SETTINGS.
        // Was: 45s interval + 100% chance (= one attack every 45s, way too frequent).
        // Default: 600s + 15% chance = one attack every ~67 min of real playtime.
        void Start()
        {
            LoadSettings();
            Debug.Log($"[TimMod] RandomFacilityDestroyer active. interval={interval}s chance={destroyChance:0.00}");
            nextDestructionTime = Time.realtimeSinceStartup + interval;
            SyndicateManager.Load();
        }

        private void LoadSettings()
        {
            try
            {
                var nodes = GameDatabase.Instance?.GetConfigNodes("SYNDICATE_SETTINGS");
                if (nodes == null || nodes.Length == 0) return;
                var cfg = nodes[0];
                float v = 0f;
                if (cfg.TryGetValue("facilityDestroyIntervalSeconds", ref v)) interval      = v;
                if (cfg.TryGetValue("facilityDestroyChance",          ref v)) destroyChance = v;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[TimMod] FacilityDestroyer: failed to load settings: " + ex.Message);
            }
        }

        void Update()
        {
            // Only run in real saves
            var mode = HighLogic.CurrentGame?.Mode;
            if (mode != Game.Modes.CAREER && mode != Game.Modes.SANDBOX) return;

            if (Time.realtimeSinceStartup < nextDestructionTime) return;
            nextDestructionTime = Time.realtimeSinceStartup + interval;

            if ((float)rng.NextDouble() > destroyChance) return; // probability gate
            TryDestroyRandomFacility();
        }

        // Guard: don't fire if syndicates never loaded. Without this, the
        // destruction timer runs unconditionally and shows phantom pop-ups.
        void TryDestroyRandomFacility()
        {
            if (SyndicateManager.AllSyndicates.Count == 0)
            {
                Debug.Log("[TimMod] FacilityDestroyer: no syndicates loaded, skipping.");
                return;
            }

            var suf = ScenarioUpgradeableFacilities.Instance;
            if (suf == null)
            {
                Debug.LogWarning("[TimMod] SUF not available yet.");
                return;
            }

            // pick a facility
            var allFacilities = Enum.GetValues(typeof(SpaceCenterFacility))
                                    .Cast<SpaceCenterFacility>()
                                    .ToList();
            var facilityEnum = allFacilities[rng.Next(allFacilities.Count)];

            // ---- Path A: best case — call SUF.SetFacilityLevel(SpaceCenterFacility, float) if present
            var miSetFacilityLevel = typeof(ScenarioUpgradeableFacilities).GetMethod(
                "SetFacilityLevel",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new[] { typeof(SpaceCenterFacility), typeof(float) },
                null);

            if (miSetFacilityLevel != null)
            {
                try
                {
                    miSetFacilityLevel.Invoke(suf, new object[] { facilityEnum, 0f });
                    ShowPopup($"⚠ {facilityEnum} destroyed!", "The Syndicate has struck again.");
                    Debug.Log($"[TimMod] Destroyed (Path A): {facilityEnum}");
                    return;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[TimMod] Path A failed: " + ex.Message);
                }
            }

            // ---- Path B: get the underlying UpgradeableFacility and SetLevel there.
            object uf = TryGetUpgradeableFacility(suf, facilityEnum);
            if (uf == null)
            {
                Debug.LogWarning($"[TimMod] Could not resolve UpgradeableFacility for {facilityEnum}");
                return;
            }

            var t = uf.GetType();
            var miSetInt = t.GetMethod("SetLevel", new[] { typeof(int) });
            var miSetFloat = t.GetMethod("SetLevel", new[] { typeof(float) });

            try
            {
                if (miSetInt != null) miSetInt.Invoke(uf, new object[] { 0 });
                else if (miSetFloat != null) miSetFloat.Invoke(uf, new object[] { 0f });
                else
                {
                    // fallback: call Downgrade() until zero if available
                    var piCur = t.GetProperty("CurrentLevel") ?? t.GetProperty("currentLevel");
                    var miDowngrade = t.GetMethod("Downgrade", Type.EmptyTypes);
                    int cur = Convert.ToInt32(piCur?.GetValue(uf, null) ?? 0);
                    while (miDowngrade != null && cur > 0) { miDowngrade.Invoke(uf, null); cur--; }
                }

                // optional best‑effort notifications (if they exist on this build)
                t.GetMethod("OnUpgradeLevelChanged", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                 ?.Invoke(uf, null);

                // fire game event if present
                var evField = typeof(GameEvents).GetField("OnKSCFacilityUpgraded",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                var ev = evField?.GetValue(null);
                var evType = ev?.GetType();
                evType?.GetMethod("Fire")?.Invoke(ev, new object[] { uf, 0 });

                ShowPopup($"⚠ {facilityEnum} destroyed!", "The Syndicate has struck again.");
                Debug.Log($"[TimMod] Destroyed (Path B): {facilityEnum}");
            }
            catch (Exception ex)
            {
                Debug.LogError("[TimMod] Failed to destroy facility: " + ex);
            }
        }

        // Try common SUF.GetFacility overloads and name lookups
        private object TryGetUpgradeableFacility(ScenarioUpgradeableFacilities suf, SpaceCenterFacility facilityEnum)
        {
            // 1) GetFacility(SpaceCenterFacility)
            var miEnum = typeof(ScenarioUpgradeableFacilities).GetMethod(
                "GetFacility",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new[] { typeof(SpaceCenterFacility) },
                null);
            if (miEnum != null)
            {
                try { return miEnum.Invoke(suf, new object[] { facilityEnum }); }
                catch { }
            }

            // 2) GetFacility(string)
            var miStr = typeof(ScenarioUpgradeableFacilities).GetMethod(
                "GetFacility",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new[] { typeof(string) },
                null);
            if (miStr != null)
            {
                try { return miStr.Invoke(suf, new object[] { facilityEnum.ToString() }); }
                catch { }
            }

            // 3) Fallback: brute‑force by type name match
            return Resources.FindObjectsOfTypeAll<UnityEngine.Object>()
                .FirstOrDefault(o => o != null &&
                                     o.GetType().Name == "UpgradeableFacility" &&
                                     string.Equals(o.name, facilityEnum.ToString(), StringComparison.OrdinalIgnoreCase));
        }

        void ShowPopup(string title, string message)
        {
            PopupDialog.SpawnPopupDialog(
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                "SyndicateFacilityPopup",
                title,
                message,
                "OK",
                true,
                HighLogic.UISkin);
        }
    }
}
