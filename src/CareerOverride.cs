using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using KSP;
using Contracts;

namespace Koko
{
    /// <summary>
    /// Utilities applied once per save (invoked by CareerBootstrap).
    /// Keeps to public/safer reflection surfaces so it compiles across KSP1 variants.
    /// </summary>
    public static class CareerOverrides
    {
        // -----------------------------
        // 1) Disable / clear contracts
        // -----------------------------
        public static void DisableAllStockContracts()
        {
            try
            {
                var cs = ContractSystem.Instance;
                if (cs == null)
                {
                    Debug.Log("[TimMod] DisableAllStockContracts: ContractSystem not ready in this scene.");
                    return;
                }

                var list = cs.Contracts != null ? cs.Contracts.ToList() : new List<Contract>();
                int removed = 0;

                foreach (var c in list)
                {
                    if (c == null) continue;

                    // Kill anything the player could accept or is already running.
                    if (c.ContractState == Contract.State.Offered ||
                        c.ContractState == Contract.State.Active)
                    {
                        try { c.Cancel(); removed++; } catch { /* best effort */ }
                    }
                }

                Debug.Log($"[TimMod] DisableAllStockContracts: cleared ~{removed} offered/active contracts.");

                // NOTE: We intentionally *do not* rely on any “ContractKiller” hook here.
                // If you later add a background hook to suppress generation, call it here.
            }
            catch (Exception ex)
            {
                Debug.LogError("[TimMod] DisableAllStockContracts failed: " + ex);
            }
        }

        // -----------------------------
        // 2) Max all facilities
        // -----------------------------
        public static void MaxAllFacilities()
        {
            try
            {
                var suf = ScenarioUpgradeableFacilities.Instance;
                if (suf == null)
                {
                    Debug.Log("[TimMod] MaxAllFacilities: SUF.Instance not ready.");
                    return;
                }

                int touched = 0;

                // Path A: direct SUF.SetFacilityLevel(SpaceCenterFacility, float)
                var miSet = typeof(ScenarioUpgradeableFacilities).GetMethod(
                    "SetFacilityLevel",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    null,
                    new[] { typeof(SpaceCenterFacility), typeof(float) },
                    null);

                if (miSet != null)
                {
                    foreach (SpaceCenterFacility f in Enum.GetValues(typeof(SpaceCenterFacility)))
                    {
                        try { miSet.Invoke(suf, new object[] { f, 1.0f }); touched++; }
                        catch (Exception ex) { Debug.LogWarning($"[TimMod] MaxAllFacilities A fail {f}: {ex.Message}"); }
                    }

                    Debug.Log($"[TimMod] MaxAllFacilities: Path A set {touched} facilities to max.");
                    return;
                }

                // Path B: generic reflection over UpgradeableFacility instances
                var allUfs = Resources.FindObjectsOfTypeAll<UnityEngine.Object>()
                    .Where(o => o != null && o.GetType().Name == "UpgradeableFacility")
                    .ToList();

                foreach (var uf in allUfs)
                {
                    var t = uf.GetType();

                    var piMax = t.GetProperty("MaxLevel", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                              ?? t.GetProperty("maxLevel", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    var piCur = t.GetProperty("CurrentLevel", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                              ?? t.GetProperty("currentLevel", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                    int maxLevel = 0, curLevel = 0;
                    try { maxLevel = Convert.ToInt32(piMax?.GetValue(uf, null) ?? 0); } catch { }
                    try { curLevel = Convert.ToInt32(piCur?.GetValue(uf, null) ?? 0); } catch { }

                    var miSetInt = t.GetMethod("SetLevel", new[] { typeof(int) });
                    var miSetFloat = t.GetMethod("SetLevel", new[] { typeof(float) });
                    var miUpgrade = t.GetMethod("Upgrade", Type.EmptyTypes);

                    try
                    {
                        if (miSetInt != null) { miSetInt.Invoke(uf, new object[] { Math.Max(maxLevel, 1) }); touched++; continue; }
                        if (miSetFloat != null) { miSetFloat.Invoke(uf, new object[] { 1.0f }); touched++; continue; }

                        if (miUpgrade != null && maxLevel > 0)
                        {
                            for (int i = curLevel; i < maxLevel; i++) miUpgrade.Invoke(uf, null);
                            touched++;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning("[TimMod] MaxAllFacilities B fail on " + t.Name + ": " + ex.Message);
                    }
                }

                Debug.Log($"[TimMod] MaxAllFacilities: Path B touched {touched} facilities.");
            }
            catch (Exception ex)
            {
                Debug.LogError("[TimMod] MaxAllFacilities failed: " + ex);
            }
        }

        // -----------------------------
        // 3) Unlock entire tech tree
        // -----------------------------
        public static void UnlockEntireTechTree()
        {
            try
            {
                var rnd = ResearchAndDevelopment.Instance;
                if (rnd == null)
                {
                    Debug.Log("[TimMod] UnlockEntireTechTree: RnD not ready; will skip this scene.");
                    return;
                }

                // Collect tech IDs. Prefer reading TechTree.cfg (stable across versions).
                var techIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                try
                {
                    var trees = GameDatabase.Instance.GetConfigNodes("TechTree");
                    foreach (var tree in trees)
                    {
                        foreach (var rdNode in tree.GetNodes("RDNode"))
                        {
                            var id = rdNode.GetValue("id");
                            if (!string.IsNullOrEmpty(id)) techIds.Add(id);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[TimMod] UnlockEntireTechTree: failed to parse TechTree.cfg: " + ex.Message);
                }

                // Fallback: try internal dictionaries (best‑effort reflection).
                if (techIds.Count == 0)
                {
                    try
                    {
                        var fields = new[] { "protoTechNodes", "techStates", "TechStates", "TechTree" };
                        foreach (var fname in fields)
                        {
                            var fi = typeof(ResearchAndDevelopment).GetField(fname, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                            if (fi == null) continue;

                            var obj = fi.GetValue(rnd);
                            if (obj is System.Collections.IDictionary dict)
                            {
                                foreach (var k in dict.Keys)
                                {
                                    if (k is string s && !string.IsNullOrEmpty(s)) techIds.Add(s);
                                }
                            }
                        }
                    }
                    catch { /* ignore */ }
                }

                if (techIds.Count == 0)
                {
                    Debug.LogWarning("[TimMod] UnlockEntireTechTree: no tech IDs discovered.");
                    return;
                }

                int unlocked = 0;

                foreach (var techId in techIds)
                {
                    try
                    {
                        // Ensure a TechState exists
                        var st = rnd.GetTechState(techId);
                        if (st == null)
                        {
                            // Coerce creation in some builds
                            rnd.AddScience(0f, TransactionReasons.ScienceTransmission);
                            st = rnd.GetTechState(techId);
                        }

                        if (st == null) continue;

                        // Make the tech available
                        if (st.state != RDTech.State.Available)
                        {
                            st.state = RDTech.State.Available;
                            unlocked++;
                        }

                        // Ensure partsPurchased is a list, then add all parts in this node
                        if (st.partsPurchased == null)
                            st.partsPurchased = new List<AvailablePart>();

                        var all = PartLoader.LoadedPartsList;
                        if (all != null)
                        {
                            foreach (var ap in all.Where(p =>
                                     string.Equals(p.TechRequired, techId, StringComparison.OrdinalIgnoreCase)))
                            {
                                if (!st.partsPurchased.Contains(ap))
                                    st.partsPurchased.Add(ap);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[TimMod] Unlock tech failed for {techId}: {ex.Message}");
                    }
                }


                // IMPORTANT: Do not call Editor-only or non‑public refreshers here.
                // Players can swap scenes (or re-open the editor) to see newly unlocked parts.
                Debug.Log($"[TimMod] UnlockEntireTechTree: unlocked ~{unlocked} tech nodes.");
            }
            catch (Exception ex)
            {
                Debug.LogError("[TimMod] UnlockEntireTechTree failed: " + ex);
            }
        }
    }
}
