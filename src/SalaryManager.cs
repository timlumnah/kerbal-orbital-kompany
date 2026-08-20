// ScenarioModule: recurring Kerbal salary + hire-time overrides.
//
// On hire (onKerbalAdded):
//   - Refunds KSP's hiring fee so effective hire cost = 0 (if zeroCostOnHire=true)
//   - Sets Kerbal to experience level 5 instantly (if maxXPOnHire=true)
//   Both apply to TRP-Hire bulk hires since they use the same KSP hire flow.
//
// Periodic Update():
//   - Deducts fundsPerKerbalPerHour for every Available/Assigned crew member
//   - Tourists and unowned Kerbals are excluded
//   - Same checkIntervalUT as BusinessManager
//
// Start() also maxes XP for any pre-existing crew (Jeb/Bill/Bob/Val etc.)
// so level cap applies uniformly regardless of when they joined.
//
// Settings loaded from BUSINESS_SETTINGS node in GameDatabase (BusinessSettings.cfg).

using System;
using UnityEngine;

namespace Koko
{
    [KSPScenario(
        ScenarioCreationOptions.AddToAllGames,
        GameScenes.SPACECENTER, GameScenes.FLIGHT, GameScenes.TRACKSTATION)]
    public class SalaryManager : ScenarioModule
    {
        private double _fundsPerKerbalPerHour = 100.0;
        private double _checkIntervalUT       = 120.0;
        private bool   _maxXPOnHire           = true;
        private bool   _zeroCostOnHire        = true;

        private double _lastCheckUT = -1.0;

        public void Start()
        {
            LoadSettings();

            if (_maxXPOnHire)
                MaxExistingCrew();

            GameEvents.onKerbalAdded.Add(OnKerbalAdded);
        }

        public void OnDestroy()
        {
            GameEvents.onKerbalAdded.Remove(OnKerbalAdded);
        }

        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            if (node.HasValue("lastSalaryCheckUT"))
                double.TryParse(node.GetValue("lastSalaryCheckUT"), out _lastCheckUT);
        }

        public override void OnSave(ConfigNode node)
        {
            base.OnSave(node);
            node.SetValue("lastSalaryCheckUT", _lastCheckUT.ToString("G17"), true);
        }

        private void LoadSettings()
        {
            ConfigNode[] cfgs = GameDatabase.Instance.GetConfigNodes("BUSINESS_SETTINGS");
            if (cfgs == null || cfgs.Length == 0)
            {
                Debug.LogWarning("[TimMod] SalaryManager: BUSINESS_SETTINGS not found, using defaults.");
                return;
            }
            ConfigNode cfg = cfgs[0];
            double.TryParse(cfg.GetValue("fundsPerKerbalPerHour"), out _fundsPerKerbalPerHour);
            double.TryParse(cfg.GetValue("checkIntervalUT"),       out _checkIntervalUT);
            bool.TryParse  (cfg.GetValue("maxXPOnHire"),           out _maxXPOnHire);
            bool.TryParse  (cfg.GetValue("zeroCostOnHire"),        out _zeroCostOnHire);
            Debug.Log("[TimMod] SalaryManager: loaded — fundsPerKerbalPerHour=" + _fundsPerKerbalPerHour
                + " maxXP=" + _maxXPOnHire + " zeroCost=" + _zeroCostOnHire);
        }

        private void OnKerbalAdded(ProtoCrewMember pcm)
        {
            if (pcm.type != ProtoCrewMember.KerbalType.Crew) return;

            if (_zeroCostOnHire && Funding.Instance != null)
            {
                try
                {
                    float normLevel   = ScenarioUpgradeableFacilities.GetFacilityLevel(SpaceCenterFacility.AstronautComplex);
                    int   upgradeLevel = Mathf.RoundToInt(normLevel * 2f); // 0/1/2
                    float cost         = GameVariables.Instance.GetRecruitHireCost(upgradeLevel);
                    if (cost > 0)
                    {
                        Funding.Instance.AddFunds(cost, TransactionReasons.ContractReward);
                        Debug.Log("[TimMod] SalaryManager: refunded hire fee of " + cost + " for " + pcm.name);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[TimMod] SalaryManager: could not refund hire cost — " + ex.Message);
                }
            }

            if (_maxXPOnHire)
                SetMaxXP(pcm);
        }

        // Called once at startup to catch Jeb/Bill/Bob/Val and any pre-existing crew.
        private void MaxExistingCrew()
        {
            if (HighLogic.CurrentGame?.CrewRoster == null) return;
            foreach (ProtoCrewMember pcm in HighLogic.CurrentGame.CrewRoster.Crew)
                SetMaxXP(pcm);
        }

        private static void SetMaxXP(ProtoCrewMember pcm)
        {
            if (pcm.experienceLevel >= 5) return;
            pcm.experienceLevel = 5;
            pcm.experience      = 99999f;
            Debug.Log("[TimMod] SalaryManager: set " + pcm.name + " to level 5.");
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
            DeductSalaries(elapsed);
        }

        private void DeductSalaries(double elapsedUT)
        {
            if (HighLogic.CurrentGame?.CrewRoster == null) return;

            int count = 0;
            foreach (ProtoCrewMember pcm in HighLogic.CurrentGame.CrewRoster.Crew)
            {
                if (pcm.rosterStatus == ProtoCrewMember.RosterStatus.Assigned)
                    count++;
            }

            if (count == 0) return;

            double cost = (_fundsPerKerbalPerHour / 3600.0) * elapsedUT * count;
            Funding.Instance.AddFunds(-cost, TransactionReasons.ContractPenalty);
            Debug.Log("[TimMod] SalaryManager: payroll -" + cost.ToString("F0") + " funds for " + count + " Kerbals (" + elapsedUT.ToString("F0") + "s elapsed).");
        }
    }
}
