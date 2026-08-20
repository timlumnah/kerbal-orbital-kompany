// New file. Declares the business type of an off-world outpost.
// Added to the Mobile Processing Lab via ScienceLab.cfg patch.
//
// Player cycles the type via right-click on the MPL in the editor or in flight.
// BusinessManager reads the type during its vessel scan and applies the
// corresponding income multiplier from BUSINESS_SETTINGS.

// Added qualification status fields shown in right-click menu.
// Update() runs a diagnostic every 5 seconds and updates the display fields.

using UnityEngine;

namespace Koko
{
    public class ModuleBusinessType : PartModule
    {
        private static readonly string[] Types = { "Standard", "Security", "Relay", "Refueling" };

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true,
                  guiName = "Business Type")]
        public string businessType = "Standard";

        [KSPEvent(guiActive = true, guiActiveEditor = true,
                  guiName = "Change Business Type")]
        public void CycleType()
        {
            int idx = System.Array.IndexOf(Types, businessType);
            businessType = Types[(idx + 1) % Types.Length];
            Debug.Log($"[TimMod] ModuleBusinessType: set to {businessType}");
        }

        // Status fields -- shown in right-click menu in flight, updated periodically
        [KSPField(guiActive = true, guiName = "Business")]
        public string bizStatus = "Checking...";

        [KSPField(guiActive = true, guiName = "Issue")]
        public string bizIssue = "";

        private float _statusTimer = 0f;
        private const float STATUS_INTERVAL = 5f;

        void Update()
        {
            if (!HighLogic.LoadedSceneIsFlight) return;
            if (vessel == null) return;

            _statusTimer += Time.deltaTime;
            if (_statusTimer < STATUS_INTERVAL) return;
            _statusTimer = 0f;

            // Only meaningful for off-world vessels
            if (vessel.mainBody != null && vessel.mainBody.bodyName == "Kerbin")
            {
                bizStatus = "On Kerbin -- not eligible";
                bizIssue  = "";
                Fields["bizIssue"].guiActive = false;
                return;
            }

            var d = BusinessManager.DiagnoseVessel(vessel);
            bizStatus = d.IsQualifying ? "ACTIVE" : "INACTIVE";
            bizIssue  = d.IsQualifying ? "" : d.FirstFailure;
            Fields["bizIssue"].guiActive = !d.IsQualifying;
        }
    }
}
