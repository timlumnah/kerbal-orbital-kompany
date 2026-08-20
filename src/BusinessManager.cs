// New ScenarioModule: passive income from qualifying off-world vessels.
// Scans all non-Kerbin landed/orbiting vessels every 120 game-seconds.
// Qualifying conditions (all required):
//   - >= 2 crew present
//   - ModuleScienceLab part on vessel (MPL)
//   - ModuleResourceHarvester part on vessel (drill)
//   - ModuleResourceConverter part on vessel (convert-o-tron)
//   - Ore resource capacity > 0 (any ore tank)
//   - CommNet connected
//   - ElectricCharge >= 10
// Payout: FundsPerHour per qualifying vessel, scaled by elapsed game time.
// Handles both loaded and unloaded (packed) vessels via protoVessel fallback.

using System;
using UnityEngine;

namespace Koko
{
    [KSPScenario(
        ScenarioCreationOptions.AddToAllGames,
        GameScenes.SPACECENTER, GameScenes.FLIGHT, GameScenes.TRACKSTATION)]
    public class BusinessManager : ScenarioModule
    {
        private double _checkIntervalUT = 120.0;
        private double _fundsPerHour    = 500.0;
        private double _sciencePerHour  = 1.0;
        private double _minEC           = 10.0;
        private int    _minCrew         = 2;

        // Public statics so UI and PartModules can read config values for display
        // without needing to re-parse the config themselves.
        public static double FundsPerHour = 500.0;
        public static double SciencePerHour = 1.0;
        public static double MinEC = 10.0;
        public static int    MinCrew = 2;

        // Per-type income multipliers. Loaded from BUSINESS_SETTINGS.
        private double _securityMultiplier  = 0.8;
        private double _relayMultiplier     = 1.5;
        private double _refuelingMultiplier = 1.3;

        // Public statics for type multipliers so UI can read them without re-parsing config.
        public static double SecurityMultiplier  = 0.8;
        public static double RelayMultiplier     = 1.5;
        public static double RefuelingMultiplier = 1.3;

        // Per-body bonus multipliers. Static so GetIncomeBreakdown (static) can access them.
        // Separate tables for orbital and landed situations.
        // Landed falls back to orbital if not defined; orbital falls back to UnknownBodyMultiplier.
        public static double UnknownBodyMultiplier = 0.0;
        private static System.Collections.Generic.Dictionary<string, double> _orbitalMultipliers
            = new System.Collections.Generic.Dictionary<string, double>();
        private static System.Collections.Generic.Dictionary<string, double> _landedMultipliers
            = new System.Collections.Generic.Dictionary<string, double>();

        private double _lastCheckUT = -1.0;

        public void Start()
        {
            ConfigNode[] cfgs = GameDatabase.Instance.GetConfigNodes("BUSINESS_SETTINGS");
            if (cfgs != null && cfgs.Length > 0)
            {
                ConfigNode cfg = cfgs[0];
                double.TryParse(cfg.GetValue("fundsPerHour"),    out _fundsPerHour);
                double.TryParse(cfg.GetValue("sciencePerHour"),  out _sciencePerHour);
                double.TryParse(cfg.GetValue("checkIntervalUT"), out _checkIntervalUT);
                double.TryParse(cfg.GetValue("minEC"),           out _minEC);
                int.TryParse   (cfg.GetValue("minCrew"),         out _minCrew);
                double.TryParse(cfg.GetValue("securityMultiplier"),  out _securityMultiplier);
                double.TryParse(cfg.GetValue("relayMultiplier"),     out _relayMultiplier);
                double.TryParse(cfg.GetValue("refuelingMultiplier"), out _refuelingMultiplier);
                SecurityMultiplier  = _securityMultiplier;
                RelayMultiplier     = _relayMultiplier;
                RefuelingMultiplier = _refuelingMultiplier;
                float sm = 0f;
                if (cfg.TryGetValue("securitySabotageMultiplier", ref sm))
                    RandomPartExplosionUtil.securitySabotageMultiplier = sm;
                double.TryParse(cfg.GetValue("unknownBodyMultiplier"), out UnknownBodyMultiplier);
                foreach (ConfigNode bm in cfg.GetNodes("BODY_MULTIPLIER"))
                {
                    string bodyName = bm.GetValue("body");
                    if (string.IsNullOrEmpty(bodyName)) continue;
                    double orbital = 0.0;
                    if (double.TryParse(bm.GetValue("orbital"), out orbital))
                        _orbitalMultipliers[bodyName] = orbital;
                    string landedStr = bm.GetValue("landed");
                    double landed = 0.0;
                    if (!string.IsNullOrEmpty(landedStr) && double.TryParse(landedStr, out landed))
                        _landedMultipliers[bodyName] = landed;
                }
                Debug.Log("[TimMod] BusinessManager: loaded " + _orbitalMultipliers.Count + " orbital / " + _landedMultipliers.Count + " landed body multipliers.");
                FundsPerHour   = _fundsPerHour;
                SciencePerHour = _sciencePerHour;
                MinEC          = _minEC;
                MinCrew        = _minCrew;
                Debug.Log("[TimMod] BusinessManager: loaded settings — fundsPerHour=" + _fundsPerHour + " sciencePerHour=" + _sciencePerHour + " minCrew=" + _minCrew + " checkInterval=" + _checkIntervalUT + " minEC=" + _minEC);
            }
            else
            {
                Debug.LogWarning("[TimMod] BusinessManager: BUSINESS_SETTINGS not found, using defaults.");
            }
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            if (node.HasValue("lastCheckUT"))
                double.TryParse(node.GetValue("lastCheckUT"), out _lastCheckUT);
        }

        public override void OnSave(ConfigNode node)
        {
            base.OnSave(node);
            node.SetValue("lastCheckUT", _lastCheckUT.ToString("G17"), true);
        }

        private void Update()
        {
            if (HighLogic.CurrentGame?.Mode != Game.Modes.CAREER) return;
            if (Funding.Instance == null) return;

            double now = Planetarium.GetUniversalTime();
            if (_lastCheckUT < 0) { _lastCheckUT = now; return; }
            if (now - _lastCheckUT < _checkIntervalUT) return;

            double elapsed = now - _lastCheckUT;
            _lastCheckUT = now;
            RunBusinessChecks(elapsed);
        }

        private void RunBusinessChecks(double elapsedUT)
        {
            double totalPayout  = 0.0;
            double totalScience = 0.0;
            int    count        = 0;

            // Clear secured bodies each tick; repopulate from Security businesses found.
            RandomPartExplosionUtil.securedBodies.Clear();

            foreach (Vessel v in FlightGlobals.Vessels)
            {
                if (v == null) continue;
                if (v.vesselType == VesselType.Debris || v.vesselType == VesselType.Flag) continue;
                // Kerbin exclusion removed. Kerbin bases qualify if they meet part/crew
                // requirements. Profitability tuned via BODY_MULTIPLIER in config.
                if (v.mainBody == null) continue;

                var sit = v.situation;
                if (sit != Vessel.Situations.LANDED &&
                    sit != Vessel.Situations.ORBITING &&
                    sit != Vessel.Situations.SPLASHED) continue;

                if (!MeetsBinaryConditions(v)) continue;
                if (!IsConnected(v)) continue;
                if (GetResourceAmount(v, "ElectricCharge") < _minEC) continue;

                // Read business type from vessel's ModuleBusinessType (on the MPL).
                // Default to Standard if no module present.
                string bType = GetBusinessType(v);
                double multiplier;
                if      (bType == "Security")  multiplier = _securityMultiplier;
                else if (bType == "Relay")     multiplier = _relayMultiplier;
                else if (bType == "Refueling") multiplier = _refuelingMultiplier;
                else                           multiplier = 1.0;

                // Security businesses protect their body from sabotage
                if (bType == "Security")
                    RandomPartExplosionUtil.securedBodies.Add(v.mainBody.bodyName);

                // Body bonus: additive multiplier on top of base rate.
                // Applies equally to funds and science.
                // Landed/splashed uses landed table; falls back to orbital; falls back to unknown.
                double bodyBonus;
                bool isLanded = v.situation == Vessel.Situations.LANDED
                             || v.situation == Vessel.Situations.SPLASHED;
                if (!(isLanded && _landedMultipliers.TryGetValue(v.mainBody.bodyName, out bodyBonus)))
                    if (!_orbitalMultipliers.TryGetValue(v.mainBody.bodyName, out bodyBonus))
                        bodyBonus = UnknownBodyMultiplier;
                // Floor at 0 -- prevents negative income if bodyBonus < -1.0.
                double bodyScale = Math.Max(0.0, 1.0 + bodyBonus);

                totalPayout   += (_fundsPerHour   / 3600.0) * elapsedUT * multiplier * bodyScale;
                totalScience  += (_sciencePerHour / 3600.0) * elapsedUT * bodyScale;
                count++;
            }

            if (count == 0) return;

            Funding.Instance.AddFunds(totalPayout, TransactionReasons.ContractReward);

            if (totalScience > 0 && ResearchAndDevelopment.Instance != null)
                ResearchAndDevelopment.Instance.AddScience((float)totalScience, TransactionReasons.None);

            Debug.Log("[TimMod] BusinessManager: " + count + " vessel(s) — +" + totalPayout.ToString("F0") + " funds, +" + totalScience.ToString("F2") + " science (" + elapsedUT.ToString("F0") + "s elapsed).");
            ScreenMessages.PostScreenMessage(
                "[Business] +" + totalPayout.ToString("F0") + " funds  +" + totalScience.ToString("F1") + " sci  (" + count + " op)",
                5f, ScreenMessageStyle.UPPER_LEFT);
        }

        // Public diagnostic result. Used by LogisticsMain UI and ModuleBusinessType
        // right-click display. Single source of truth for qualification logic.
        public class VesselDiagnostic
        {
            public bool   crew, sciLab, harvester, converter, ore, commNet, power;
            public int    crewCount;
            public double ecAmount;
            public string businessType = "Standard";
            public bool   IsQualifying => crew && sciLab && harvester && converter
                                       && ore && commNet && power;

            public string StatusLine => IsQualifying
                ? $"ACTIVE  ({businessType})"
                : $"INACTIVE -- {FirstFailure}";

            public string FirstFailure
            {
                get
                {
                    if (!crew)      return $"crew {crewCount}/{MinCrew}";
                    if (!sciLab)    return "Science Lab missing";
                    if (!harvester) return "Drill missing";
                    if (!converter) return "ISRU missing";
                    if (!ore)       return "Ore storage missing";
                    if (!commNet)   return "CommNet disconnected";
                    if (!power)     return $"low EC ({ecAmount:F0}/{MinEC})";
                    return "";
                }
            }
        }

        public static VesselDiagnostic DiagnoseVessel(Vessel v)
        {
            var d = new VesselDiagnostic();
            if (v == null) return d;

            d.crewCount  = v.GetCrewCount();
            d.ecAmount   = GetResourceAmount(v, "ElectricCharge");
            d.crew       = d.crewCount >= MinCrew;
            d.sciLab     = HasModule(v, "ModuleScienceLab");
            d.harvester  = HasModule(v, "ModuleResourceHarvester");
            d.converter  = HasModule(v, "ModuleResourceConverter");
            d.ore        = GetResourceCapacity(v, "Ore") > 0;
            d.commNet    = IsConnected(v);
            d.power      = d.ecAmount >= MinEC;
            d.businessType = GetBusinessType(v);
            return d;
        }

        // Proto-vessel qualification check. Used by editor-scene launch candidate
        // filter where FlightGlobals.Vessels is empty and only protoVessels exist.
        // Skips CommNet and EC -- runtime-only, not reliably in proto snapshot.
        public static bool IsQualifyingBase(ProtoVessel pv)
        {
            if (pv == null) return false;
            if (pv.GetVesselCrew().Count < MinCrew) return false;

            bool hasSciLab    = false;
            bool hasHarvester = false;
            bool hasConverter = false;
            double oreCapacity = 0;

            foreach (ProtoPartSnapshot pps in pv.protoPartSnapshots)
            {
                foreach (ProtoPartModuleSnapshot ppms in pps.modules)
                {
                    if (ppms.moduleName == "ModuleScienceLab")        hasSciLab    = true;
                    if (ppms.moduleName == "ModuleResourceHarvester") hasHarvester = true;
                    if (ppms.moduleName == "ModuleResourceConverter") hasConverter = true;
                }
                foreach (ProtoPartResourceSnapshot pprs in pps.resources)
                    if (pprs.resourceName == "Ore") oreCapacity += pprs.maxAmount;
            }

            return hasSciLab && hasHarvester && hasConverter && oreCapacity > 0;
        }

        // Full income breakdown for one vessel. Used by Ops UI detail panel.
        public class IncomeBreakdown
        {
            public double TypeMult;       // e.g. 1.5 for Relay, 1.0 for Standard
            public double BodyBonus;      // e.g. 0.03 for Duna; 0 = no bonus
            public string BodySituation;  // "landed" or "orbital" (which table was used)
            public double FundsPerHour;   // final effective value
            public double SciencePerHour; // final effective value
        }

        // Returns income breakdown for a vessel given its business type.
        // Mirrors the payout logic in RunBusinessChecks exactly.
        public static IncomeBreakdown GetIncomeBreakdown(Vessel v, string businessType)
        {
            double typeMult;
            if      (businessType == "Security")  typeMult = SecurityMultiplier;
            else if (businessType == "Relay")     typeMult = RelayMultiplier;
            else if (businessType == "Refueling") typeMult = RefuelingMultiplier;
            else                                   typeMult = 1.0;

            bool   isLanded = v != null && (v.situation == Vessel.Situations.LANDED
                                         || v.situation == Vessel.Situations.SPLASHED);
            string bodyName = v?.mainBody?.bodyName ?? "";
            double bodyBonus;
            bool   usedLanded = false;
            if (isLanded && _landedMultipliers.TryGetValue(bodyName, out bodyBonus))
                usedLanded = true;
            else if (!_orbitalMultipliers.TryGetValue(bodyName, out bodyBonus))
                bodyBonus = UnknownBodyMultiplier;

            double bodyScale = Math.Max(0.0, 1.0 + bodyBonus);
            return new IncomeBreakdown {
                TypeMult       = typeMult,
                BodyBonus      = bodyBonus,
                BodySituation  = usedLanded ? "landed" : "orbital",
                FundsPerHour   = FundsPerHour   * typeMult * bodyScale,
                SciencePerHour = SciencePerHour * bodyScale
            };
        }

        // Returns the ModuleBusinessType.businessType string from the vessel,
        // or "Standard" if no module is present. Only checks loaded vessels --
        // packed vessels inherit last-saved state (acceptable for this use).
        private static string GetBusinessType(Vessel v)
        {
            if (!v.loaded) return "Standard";
            foreach (Part p in v.parts)
            {
                var m = p.FindModuleImplementing<ModuleBusinessType>();
                if (m != null) return m.businessType;
            }
            return "Standard";
        }

        private bool MeetsBinaryConditions(Vessel v)
        {
            if (v.GetCrewCount() < _minCrew)              return false;
            if (!HasModule(v, "ModuleScienceLab"))        return false;
            if (!HasModule(v, "ModuleResourceHarvester")) return false;
            if (!HasModule(v, "ModuleResourceConverter")) return false;
            if (GetResourceCapacity(v, "Ore") <= 0)      return false;
            return true;
        }

        private static bool IsConnected(Vessel v)
        {
            try   { return v.connection != null && v.connection.IsConnected; }
            catch { return false; }
        }

        private static bool HasModule(Vessel v, string moduleName)
        {
            if (v.loaded)
            {
                foreach (Part p in v.parts)
                    foreach (PartModule m in p.Modules)
                        if (m.moduleName == moduleName) return true;
                return false;
            }
            foreach (ProtoPartSnapshot pps in v.protoVessel.protoPartSnapshots)
                foreach (ProtoPartModuleSnapshot ppms in pps.modules)
                    if (ppms.moduleName == moduleName) return true;
            return false;
        }

        private static double GetResourceAmount(Vessel v, string resourceName)
        {
            double total = 0;
            if (v.loaded)
            {
                foreach (Part p in v.parts)
                    foreach (PartResource r in p.Resources)
                        if (r.resourceName == resourceName) total += r.amount;
                return total;
            }
            foreach (ProtoPartSnapshot pps in v.protoVessel.protoPartSnapshots)
                foreach (ProtoPartResourceSnapshot pprs in pps.resources)
                    if (pprs.resourceName == resourceName) total += pprs.amount;
            return total;
        }

        private static double GetResourceCapacity(Vessel v, string resourceName)
        {
            double total = 0;
            if (v.loaded)
            {
                foreach (Part p in v.parts)
                    foreach (PartResource r in p.Resources)
                        if (r.resourceName == resourceName) total += r.maxAmount;
                return total;
            }
            foreach (ProtoPartSnapshot pps in v.protoVessel.protoPartSnapshots)
                foreach (ProtoPartResourceSnapshot pprs in pps.resources)
                    if (pprs.resourceName == resourceName) total += pprs.maxAmount;
            return total;
        }
    }
}
