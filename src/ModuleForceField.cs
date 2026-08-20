// New file. Force field projector: blocks all Syndicate sabotage on the
// vessel while active, at the cost of high EC drain.
//
// ExplodeOne() in RandomPartExplosionUtil checks for an active ModuleForceField
// on the vessel before rolling sabotage. If found, it returns false immediately.
//
// Intended as a late-game, high-cost defensive upgrade. Default drain is
// 50 EC/s -- the player needs substantial power generation to sustain it.
//
// ecDrainPerSecond is configurable in the part CFG.

using UnityEngine;

namespace Koko
{
    public class ModuleForceField : PartModule
    {
        [KSPField(isPersistant = true)]
        public bool isActive = false;

        [KSPField]
        public float ecDrainPerSecond = 50f;

        [KSPEvent(guiActive = true, guiActiveEditor = false,
                  guiName = "Activate Force Field")]
        public void ActivateField()
        {
            isActive = true;
            UpdateGUI();
            ScreenMessages.PostScreenMessage(
                "Force field active. Syndicate sabotage suppressed.",
                3f, ScreenMessageStyle.UPPER_CENTER);
        }

        [KSPEvent(guiActive = true, guiActiveEditor = false,
                  guiName = "Deactivate Force Field", active = false)]
        public void DeactivateField()
        {
            isActive = false;
            UpdateGUI();
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            UpdateGUI();
        }

        void Update()
        {
            if (!isActive) return;
            if (!HighLogic.LoadedSceneIsFlight) return;

            // Drain EC. If we can't get at least half of what we need, shut down.
            double needed  = ecDrainPerSecond * TimeWarp.deltaTime;
            double received = part.RequestResource("ElectricCharge", needed);
            if (received < needed * 0.5)
            {
                isActive = false;
                UpdateGUI();
                ScreenMessages.PostScreenMessage(
                    "Force field offline -- insufficient power.",
                    4f, ScreenMessageStyle.UPPER_CENTER);
            }
        }

        private void UpdateGUI()
        {
            Events["ActivateField"].active   = !isActive;
            Events["DeactivateField"].active = isActive;
        }
    }
}
