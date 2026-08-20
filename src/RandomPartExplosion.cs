using System.Linq;
using UnityEngine;
using KSP;

namespace Koko
{
    public static class RandomPartExplosionUtil
    {
        private static System.Random rng = new System.Random();

        // Detection score weights. Set by SyndicateZonesManager.Start() via
        // SYNDICATE_SETTINGS config. Defaults used if config not present.
        //
        // Formula:
        //   baseDetection  = (partCount * partCountWeight)
        //                  + (totalMass  * massWeight)
        //                  + (maxPartTemp / tempDivisor)
        //   stealthScore   = sum(ModuleStealth.stealthValue) * stealthMultiplier
        //   netDetection   = baseDetection - stealthScore  (clamped >= 0)
        //   sabotageChance = Clamp01(netDetection / maxDetectionScore)
        public static float partCountWeight   = 0.1f;
        public static float massWeight        = 0.2f;
        public static float tempDivisor       = 2000f;
        public static float stealthMultiplier = 1.0f;
        public static float maxDetectionScore = 10.0f;

        // Bodies with an active Security business. Populated by BusinessManager
        // each income tick. Sabotage chance is halved for vessels on these bodies.
        public static System.Collections.Generic.HashSet<string> securedBodies
            = new System.Collections.Generic.HashSet<string>(
                System.StringComparer.OrdinalIgnoreCase);

        // Multiplier applied to sabotage chance when a Security business is active.
        // 0.5 = 50% reduction. Configurable via BUSINESS_SETTINGS.
        public static float securitySabotageMultiplier = 0.5f;

        // Returns sabotage chance 0-1 without triggering any explosion.
        // Used by SyndicateHUD to show the player their stealth rating.
        // 0 = fully stealthy, 1 = fully detected.
        public static float ComputeSabotageChance(Vessel v)
        {
            if (v == null || v.parts == null || v.parts.Count == 0) return 0f;

            float partScore = v.parts.Count * partCountWeight;
            float massScore = (float)v.GetTotalMass() * massWeight;
            double maxTemp = 300.0;
            try { maxTemp = v.parts.Max(p => p.temperature); } catch { }
            float tempScore = (float)(maxTemp / tempDivisor);

            float totalStealth = 0f;
            foreach (var p in v.parts)
            {
                var sm = p.FindModuleImplementing<ModuleStealth>();
                if (sm != null) totalStealth += sm.stealthValue;
            }

            float net = Mathf.Max(0f, partScore + massScore + tempScore - totalStealth * stealthMultiplier);
            return Mathf.Clamp01(net / maxDetectionScore);
        }

        public static bool ExplodeOne(Vessel v)
        {
            if (v == null || v.parts == null || v.parts.Count == 0) return false;

            // Force field: if any part on this vessel has an active ModuleForceField,
            // sabotage is fully blocked regardless of detection score.
            foreach (var p in v.parts)
            {
                var ff = p.FindModuleImplementing<ModuleForceField>();
                if (ff != null && ff.isActive)
                {
                    Debug.Log("[TimMod] Sabotage blocked by force field.");
                    return false;
                }
            }

            // Replaced flat stealthValue sum with composite detection score.
            // Larger, hotter vessels are more detectable regardless of stealth parts.

            // Base detection: physical signature of the vessel
            float partScore = v.parts.Count * partCountWeight;
            float massScore = (float)v.GetTotalMass() * massWeight;

            double maxTemp = 300.0; // ambient fallback
            try { maxTemp = v.parts.Max(p => p.temperature); } catch { }
            float tempScore = (float)(maxTemp / tempDivisor);

            // Stealth reduction: sum all ModuleStealth values on vessel
            float totalStealth = 0f;
            foreach (var p in v.parts)
            {
                var sm = p.FindModuleImplementing<ModuleStealth>();
                if (sm != null)
                    totalStealth += sm.stealthValue;
            }
            float stealthScore = totalStealth * stealthMultiplier;

            float netDetection   = Mathf.Max(0f, partScore + massScore + tempScore - stealthScore);
            float sabotageChance = Mathf.Clamp01(netDetection / maxDetectionScore);

            // Security business: halve sabotage chance on protected bodies.
            if (v.mainBody != null &&
                securedBodies.Contains(v.mainBody.bodyName))
            {
                sabotageChance *= securitySabotageMultiplier;
                Debug.Log($"[TimMod] Security business active on {v.mainBody.bodyName} -- " +
                          $"sabotage chance reduced to {sabotageChance:F2}");
            }

            float roll = UnityEngine.Random.value;

            Debug.Log($"[TimMod] Detection -- parts={partScore:0.00} mass={massScore:0.00} " +
                      $"temp={tempScore:0.00} stealth={stealthScore:0.00} " +
                      $"net={netDetection:0.00} chance={sabotageChance:0.00} roll={roll:0.00}");

            if (roll > sabotageChance)
            {
                Debug.Log("[TimMod] Sabotage blocked by stealth.");
                return false;
            }

            // Pick a non-root part and explode it
            var parts = v.parts.Where(p => p != null && p != v.rootPart).ToList();
            if (parts.Count == 0) return false;

            var part = parts[rng.Next(parts.Count)];
            Debug.Log($"[TimMod] Syndicate sabotage: exploding {part.partInfo?.title ?? part.name}");
            ScreenMessages.PostScreenMessage(
                $"Sabotage! {part.partInfo?.title ?? part.name}", 5f, ScreenMessageStyle.UPPER_CENTER);
            part.explode();
            return true;
        }
    }
}
