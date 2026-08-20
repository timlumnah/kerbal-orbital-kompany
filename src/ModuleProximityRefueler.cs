using System;
using System.Linq;
using UnityEngine;

namespace Koko
{
    // Proximity-based instant fuel transfer.
    // isSource=true: scans vessels in range and fills their tanks.
    // isSource=false: passive receiver, gets filled by nearby sources.
    // Toggle mode via right-click menu in flight or editor.
    [KSPModule("Proximity Refueler")]
    public class ModuleProximityRefueler : PartModule
    {
        // Configurable in part CFG.
        [KSPField(isPersistant = true)]
        public bool isSource = false;

        // Range in meters. Configurable in part CFG.
        [KSPField(isPersistant = true, guiActive = true, guiName = "Refuel Range (m)")]
        public float transferRange = 100f;

        // If true, source tanks are never drained. Toggle in CFG for sandbox vs realistic.
        [KSPField(isPersistant = true)]
        public bool infiniteFuel = false;

        // Comma-separated resource names. Default covers liquid-fueled rockets.
        [KSPField(isPersistant = true)]
        public string resourceNames = "LiquidFuel,Oxidizer";

        private float _nextCheck = 0f;
        private const float CheckInterval = 2f;

        // Right-click toggle — changes label to reflect current state.
        [KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "Enable Refueling Mode")]
        public void ToggleSourceMode()
        {
            isSource = !isSource;
            UpdateEventLabel();
            Debug.Log($"[TimMod] ProximityRefueler ({part.partName}): " +
                      $"{(isSource ? "SOURCE" : "RECEIVER")} mode.");
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            UpdateEventLabel();
        }

        private void UpdateEventLabel()
        {
            Events["ToggleSourceMode"].guiName =
                isSource ? "Disable Refueling Mode" : "Enable Refueling Mode";
        }

        void FixedUpdate()
        {
            if (!isSource) return;
            if (!HighLogic.LoadedSceneIsFlight) return;
            if (vessel == null) return;
            if (Time.fixedTime < _nextCheck) return;
            _nextCheck = Time.fixedTime + CheckInterval;

            ScanAndRefuel();
        }

        void ScanAndRefuel()
        {
            // Split(char) resolves to Split(char, StringSplitOptions) in .NET 8
            // Roslyn but that overload doesn't exist on KSP's Mono runtime.
            // Use Split(char[]) instead.
            var resources = resourceNames
                .Split(new char[] { ',' })
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrEmpty(s))
                .ToArray();

            foreach (Vessel other in FlightGlobals.Vessels)
            {
                if (other == vessel) continue;
                if (other.packed) continue;  // outside physics range, can't interact

                float dist = Vector3.Distance(
                    vessel.transform.position,
                    other.transform.position);
                if (dist > transferRange) continue;

                // Don't refuel other active sources — avoids source-vs-source loops.
                if (other.Parts.Any(p => {
                    var m = p.FindModuleImplementing<ModuleProximityRefueler>();
                    return m != null && m.isSource;
                })) continue;

                bool transferred = false;
                foreach (string res in resources)
                {
                    if (TransferResource(other, res) > 0.01)
                        transferred = true;
                }

                if (transferred)
                    Debug.Log($"[TimMod] ProximityRefueler: refueled {other.vesselName} " +
                              $"at {dist:F0}m.");
            }
        }

        // Pulls from source tanks (unless infiniteFuel) and fills target tanks.
        // Returns amount actually transferred.
        double TransferResource(Vessel target, string resourceName)
        {
            var def = PartResourceLibrary.Instance.GetDefinition(resourceName);
            if (def == null) return 0;

            // How much space does the target have?
            double spaceAvailable = 0;
            foreach (Part p in target.Parts)
                foreach (PartResource r in p.Resources)
                    if (r.resourceName == resourceName && r.flowState)
                        spaceAvailable += r.maxAmount - r.amount;

            if (spaceAvailable < 0.01) return 0;  // already full

            double toAdd = spaceAvailable;

            if (!infiniteFuel)
            {
                // Draw from this vessel's tanks using KSP's flow system.
                double drawn = vessel.RequestResource(part, def.id, toAdd, false);
                if (drawn < 0.01) return 0;  // source is empty
                toAdd = drawn;
            }

            // Distribute into target tanks.
            double remaining = toAdd;
            foreach (Part p in target.Parts)
            {
                foreach (PartResource r in p.Resources)
                {
                    if (r.resourceName != resourceName || !r.flowState) continue;
                    double space = r.maxAmount - r.amount;
                    double add = Math.Min(space, remaining);
                    r.amount += add;
                    remaining -= add;
                    if (remaining < 0.01) break;
                }
                if (remaining < 0.01) break;
            }

            return toAdd - remaining;
        }
    }
}
