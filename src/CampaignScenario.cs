// New file. ScenarioModule tracking campaign state and progression events.
//
// Responsibilities:
//   - Persist campaign phase and one-time event flags across saves.
//   - Drive opening/mid-twist/penultimate/victory/defeat dialogue triggers.
//   - Track Eeloo fortress progress via Planetarium.GetUniversalTime().
//
// All dialogue triggers fire only from SpaceCenter or TrackStation scenes
// so they never interrupt active flight.
//
// Fortress timer: game-time (Planetarium UT), advances with time warp.
// Duration configurable in SYNDICATE_SETTINGS (fortressDurationSeconds).
// Default: 2 Kerbin years = 18,407,090 seconds.

using System;
using System.Linq;
using UnityEngine;

namespace Koko
{
    public enum CampaignPhase
    {
        Opening     = 0,
        Active      = 1,
        MidTwist    = 2,
        Penultimate = 3,
        Victory     = 4,
        Defeat      = 5
    }

    [KSPScenario(ScenarioCreationOptions.AddToAllGames,
        GameScenes.SPACECENTER, GameScenes.FLIGHT, GameScenes.TRACKSTATION)]
    public class CampaignScenario : ScenarioModule
    {
        public static CampaignScenario Instance { get; private set; }

        public CampaignPhase Phase = CampaignPhase.Opening;

        private bool openingPlayed;
        private bool midTwistPlayed;
        private bool penultimatePlayed;

        // Fortress timer: Planetarium UT when the timer started (-1 = not yet started)
        public double fortressStartUT = -1.0;

        // Config-driven defaults; overridden by SYNDICATE_SETTINGS on load
        private double fortressDurationSeconds = 18_407_090.0; // 2 Kerbin years
        private float  midTwistThreshold       = 0.40f;        // 40% locations destroyed

        // Each destroyed non-Eeloo location adds 10% to the effective duration.
        // Computed live -- no new persisted state needed.
        // Respawn shrinks bonusCount, which shortens effectiveDuration (timer tightens).
        private int ComputeBonusCount()
        {
            int n = 0;
            if (SyndicateManager.AllSyndicates == null) return 0;
            foreach (var syn in SyndicateManager.AllSyndicates)
                foreach (var loc in syn.locations)
                    if (loc.destroyed && !string.Equals(loc.body, "Eeloo",
                            StringComparison.OrdinalIgnoreCase))
                        n++;
            return n;
        }

        public float FortressProgressPct
        {
            get
            {
                if (fortressStartUT <= 0) return 0f;
                double elapsed    = Planetarium.GetUniversalTime() - fortressStartUT;
                double effective  = fortressDurationSeconds * (1.0 + 0.10 * ComputeBonusCount());
                return Mathf.Clamp01((float)(elapsed / effective));
            }
        }

        public override void OnAwake()
        {
            base.OnAwake();
            Instance = this;
            LoadCampaignConfig();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void LoadCampaignConfig()
        {
            if (GameDatabase.Instance == null) return;
            var nodes = GameDatabase.Instance.GetConfigNodes("SYNDICATE_SETTINGS");
            if (nodes == null || nodes.Length == 0) return;
            var cfg = nodes[0];

            double d = 0;
            float  f = 0;
            if (cfg.TryGetValue("fortressDurationSeconds", ref d)) fortressDurationSeconds = d;
            if (cfg.TryGetValue("midTwistThreshold",       ref f)) midTwistThreshold       = f;
        }

        // Revised: opening still fires at SC/TS only. All other triggers fire in
        // FLIGHT too and stop time warp so the player doesn't miss them mid-warp.
        void Update()
        {
            if (CampaignDialogue.IsShowing) return;
            if (Phase == CampaignPhase.Victory || Phase == CampaignPhase.Defeat) return;

            var scene = HighLogic.LoadedScene;
            bool atBase   = scene == GameScenes.SPACECENTER || scene == GameScenes.TRACKSTATION;
            bool inFlight = scene == GameScenes.FLIGHT;
            if (!atBase && !inFlight) return;

            // Opening: SC/TS only -- intentional scene-setting
            if (!openingPlayed)
            {
                if (!atBase) return;
                openingPlayed = true;
                Phase = CampaignPhase.Active;
                CampaignDialogue.Show(CampaignDialogue.Slides.Opening, () =>
                {
                    if (fortressStartUT <= 0)
                        fortressStartUT = Planetarium.GetUniversalTime();
                    Debug.Log($"[TimMod] CampaignScenario: fortress timer started at UT={fortressStartUT:F0}");
                });
                return;
            }

            if (fortressStartUT <= 0) return;

            // Remaining triggers: fire anywhere, stop warp first
            // Defeat always takes priority (hard deadline)
            if (FortressProgressPct >= 1f)
            {
                Phase = CampaignPhase.Defeat;
                StopWarp();
                CampaignDialogue.Show(CampaignDialogue.Slides.Defeat, null);
                Debug.Log("[TimMod] CampaignScenario: DEFEAT -- fortress complete.");
                return;
            }

            // Narrative beats fire before victory so they can't be skipped by
            // destroying all locations in one session before returning to SC/TS.
            if (!midTwistPlayed && GetDestructionPct() >= midTwistThreshold)
            {
                midTwistPlayed = true;
                StopWarp();
                CampaignDialogue.Show(CampaignDialogue.Slides.MidTwist, null);
                Debug.Log("[TimMod] CampaignScenario: mid-twist triggered.");
                return;
            }

            if (!penultimatePlayed && AnyEelooRevealed())
            {
                penultimatePlayed = true;
                Phase = CampaignPhase.Penultimate;
                StopWarp();
                CampaignDialogue.Show(CampaignDialogue.Slides.Penultimate, null);
                Debug.Log("[TimMod] CampaignScenario: penultimate reveal triggered.");
                return;
            }

            // Victory only fires after all narrative beats have played
            if (AllLocationsDestroyed())
            {
                Phase = CampaignPhase.Victory;
                StopWarp();
                CampaignDialogue.Show(CampaignDialogue.Slides.Victory, null);
                Debug.Log("[TimMod] CampaignScenario: VICTORY -- all syndicates eliminated.");
            }
        }

        private static void StopWarp()
        {
            if (TimeWarp.CurrentRate > 1f)
                TimeWarp.SetRate(0, true);
        }

        public override void OnSave(ConfigNode node)
        {
            node.AddValue("phase",             (int)Phase);
            node.AddValue("openingPlayed",     openingPlayed);
            node.AddValue("midTwistPlayed",    midTwistPlayed);
            node.AddValue("penultimatePlayed", penultimatePlayed);
            node.AddValue("fortressStartUT",   fortressStartUT.ToString("R"));
        }

        public override void OnLoad(ConfigNode node)
        {
            string s;

            s = node.GetValue("phase");
            if (s != null && int.TryParse(s, out int pi)) Phase = (CampaignPhase)pi;

            s = node.GetValue("openingPlayed");
            if (s != null && bool.TryParse(s, out bool b)) openingPlayed = b;

            s = node.GetValue("midTwistPlayed");
            if (s != null && bool.TryParse(s, out b)) midTwistPlayed = b;

            s = node.GetValue("penultimatePlayed");
            if (s != null && bool.TryParse(s, out b)) penultimatePlayed = b;

            s = node.GetValue("fortressStartUT");
            if (s != null && double.TryParse(s, out double d)) fortressStartUT = d;

            Debug.Log($"[TimMod] CampaignScenario loaded: phase={Phase} " +
                      $"openingPlayed={openingPlayed} fortressStartUT={fortressStartUT:F0}");
        }

        // -- Condition helpers --

        private static bool AllLocationsDestroyed()
        {
            if (SyndicateManager.AllSyndicates == null ||
                SyndicateManager.AllSyndicates.Count == 0) return false;
            foreach (var syn in SyndicateManager.AllSyndicates)
                foreach (var loc in syn.locations)
                    if (!loc.destroyed) return false;
            return true;
        }

        private static float GetDestructionPct()
        {
            int total = 0, destroyed = 0;
            if (SyndicateManager.AllSyndicates == null) return 0f;
            foreach (var syn in SyndicateManager.AllSyndicates)
                foreach (var loc in syn.locations)
                {
                    total++;
                    if (loc.destroyed) destroyed++;
                }
            return total > 0 ? (float)destroyed / total : 0f;
        }

        private static bool AnyEelooRevealed()
        {
            if (SyndicateManager.AllSyndicates == null) return false;
            foreach (var syn in SyndicateManager.AllSyndicates)
                foreach (var loc in syn.locations)
                    if (string.Equals(loc.body, "Eeloo",
                            StringComparison.OrdinalIgnoreCase) && loc.revealed)
                        return true;
            return false;
        }
    }
}
