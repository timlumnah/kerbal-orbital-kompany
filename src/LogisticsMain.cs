using System;
using UnityEngine;
using KSP;
using System.Collections.Generic;
using System.Linq;


namespace Koko
{
    [KSPAddon(KSPAddon.Startup.AllGameScenes, false)]

    public class LogisticsMain : MonoBehaviour
    {
        // UI overhaul:
        //   - Routes / Create Route / History tabs commented out (not needed now)
        //   - Businesses tab rewritten: shows actual off-world vessels, why each
        //     does or doesn't qualify, and a detail checklist on click
        //   - "Start a New Business" buttons commented out

        // --- UI State ---
        private Rect _winRect = new Rect(60, 60, 540, 480);
        private bool _show = true;

        // ---- Commented-out tab infrastructure (Routes / Create Route / History) ----
        // private readonly string[] _tabNames = { "Routes", "Create Route", "History", "Businesses" };
        // private int _selectedTab = 0;

        // Tab bar: Operations | Launch To
        private readonly string[] _tabNames = { "Operations", "Launch To" };
        private int _selectedTab = 0;

        // --- Businesses tab state ---
        private Vector2 _bizScrollPos     = Vector2.zero;
        private Guid?   _selectedVesselId = null;

        // Launch To tab state
        private Vector2 _launchScrollPos    = Vector2.zero;
        private bool    _launchDiagLogged   = false; // auto-log once per editor session

        // --- Cached styles (lazy-init in OnGUI) ---
        private GUIStyle _styleGreen;
        private GUIStyle _styleRed;
        private GUIStyle _styleGray;
        private GUIStyle _styleDetail;
        private GUIStyle _styleGreenLabel;

        // --- Form fields (kept for commented-out Create Route tab) ---
        private string _newOrigin          = string.Empty;
        private string _newDestination     = string.Empty;
        private string _newResourceType    = string.Empty;
        private string _newFrequency       = string.Empty;
        private string _newQuantity        = string.Empty;
        private string _deliveryFrequencyText = "1";

        private List<Vessel> _vesselOptions;
        private string[]     _vesselNames;
        private int          _selectedVesselIndex;
        private bool         _sendFuel = false;
        private bool         _sendFood = false;
        private readonly string[] _frequencies = { "Every 1 month", "Every 2 months", "Every 3 months" };

        public static LogisticsMain Instance;

        // --- Data ---
        private readonly DeliveryManager _deliveryManager = new DeliveryManager();


        // ---- Lifecycle ----
        void Awake()
        {
            Instance = this;
            Debug.Log("[TimMod] LogisticsMain.Awake()");
        }

        void Start()
        {
            Debug.Log("[TimMod] LogisticsMain.Start()");
            _deliveryManager.LoadRoutesFromScenario();

            if (HighLogic.LoadedScene == GameScenes.SPACECENTER ||
                HighLogic.LoadedScene == GameScenes.FLIGHT ||
                HighLogic.LoadedScene == GameScenes.TRACKSTATION)
            {
                RefreshVesselList();
            }
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.L) &&
                Input.GetKey(KeyCode.LeftControl) &&
                Input.GetKey(KeyCode.LeftAlt))
            {
                _show = !_show;
                Debug.Log($"[TimMod] Toggled Logistics UI: {_show}");
            }

            if (Time.time % 300 < Time.deltaTime)
                CleanupInvalidRoutes();

            if (HighLogic.LoadedScene == GameScenes.FLIGHT && FlightGlobals.ActiveVessel != null)
                ApplyMissedDeliveries(FlightGlobals.ActiveVessel);
        }

        private void CleanupInvalidRoutes()
        {
            var activeVesselNames = new HashSet<string>(
                FlightGlobals.Vessels
                    .Where(v => v != null && !string.IsNullOrEmpty(v.vesselName))
                    .Select(v => v.vesselName)
            );
            var orphaned = _deliveryManager.GetActiveRoutes()
                .Where(r => !activeVesselNames.Contains(r.Destination)).ToList();
            foreach (var r in orphaned)
            {
                Debug.Log($"[TimMod] Removing route to missing vessel: {r.Destination}");
                _deliveryManager.RemoveRoute(r.Id);
                LogisticsScenario.Instance?.SavedRoutes.RemoveAll(sr => sr.Id == r.Id);
            }
        }


        // ---- IMGUI ----
        void OnGUI()
        {
            // Added EDITOR so Launch To tab works in VAB/SPH.
            if (HighLogic.LoadedScene != GameScenes.SPACECENTER &&
                HighLogic.LoadedScene != GameScenes.FLIGHT      &&
                HighLogic.LoadedScene != GameScenes.TRACKSTATION &&
                HighLogic.LoadedScene != GameScenes.EDITOR)
                return;

            if (!_show) return;

            InitStyles();

            _winRect = GUILayout.Window(
                GetInstanceID(),
                _winRect,
                DrawWindow,
                "Operations",
                HighLogic.Skin != null ? HighLogic.Skin.window : GUI.skin.window
            );
        }

        private void InitStyles()
        {
            if (_styleGreen != null) return;

            _styleGreen = new GUIStyle(GUI.skin.button);
            _styleGreen.normal.textColor  = new Color(0.4f, 1f, 0.4f);

            _styleRed = new GUIStyle(GUI.skin.button);
            _styleRed.normal.textColor    = new Color(1f, 0.45f, 0.45f);

            _styleGray = new GUIStyle(GUI.skin.label);
            _styleGray.normal.textColor   = new Color(0.7f, 0.7f, 0.7f);

            _styleDetail = new GUIStyle(GUI.skin.label);
            _styleDetail.normal.textColor = new Color(0.85f, 0.85f, 0.85f);
            _styleDetail.fontSize         = 11;

            _styleGreenLabel = new GUIStyle(GUI.skin.label);
            _styleGreenLabel.normal.textColor = new Color(0.4f, 1f, 0.4f);
            _styleGreenLabel.fontSize         = 11;
        }


        private void DrawWindow(int id)
        {
            _selectedTab = GUILayout.Toolbar(_selectedTab, _tabNames);
            GUILayout.Space(8);
            if (_selectedTab == 0)
                DrawBusinessesTab();
            else
                DrawLaunchToTab();

            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Ping Log"))
                Debug.Log("[TimMod] GUI ping.");

            GUI.DragWindow();
        }


        // ---- Businesses tab ----

        private void DrawBusinessesTab()
        {
            GUILayout.Space(4);

            // Defended bodies: all locations cleared + controllable vessel in orbit.
            if (SyndicateManager.AllSyndicates != null)
            {
                var allBodies = SyndicateManager.AllSyndicates
                    .SelectMany(s => s.locations)
                    .Select(l => l.body)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                var defended = allBodies
                    .Where(b => SyndicateManager.IsBodyDefended(b))
                    .ToList();
                if (defended.Count > 0)
                {
                    GUILayout.Label("PROTECTED: " + string.Join(", ", defended), _styleGreenLabel);
                    GUILayout.Space(4);
                }
            }

            // Collect candidates: off-Kerbin, non-debris vessels in a stable situation
            var candidates = new List<Vessel>();
            if (FlightGlobals.Vessels != null)
            {
                foreach (var v in FlightGlobals.Vessels)
                {
                    if (v == null) continue;
                    if (v.vesselType == VesselType.Debris   ||
                        v.vesselType == VesselType.Flag     ||
                        v.vesselType == VesselType.EVA      ||
                        v.vesselType == VesselType.SpaceObject) continue;
                    // Kerbin exclusion removed -- Kerbin bases now eligible for business income.
                    if (v.mainBody == null) continue;
                    var sit = v.situation;
                    if (sit != Vessel.Situations.LANDED  &&
                        sit != Vessel.Situations.ORBITING &&
                        sit != Vessel.Situations.SPLASHED) continue;
                    candidates.Add(v);
                }
            }

            if (candidates.Count == 0)
            {
                GUILayout.Label("No off-world vessels found.", _styleGray);
            }
            else
            {
                _bizScrollPos = GUILayout.BeginScrollView(_bizScrollPos, GUILayout.Height(320));

                foreach (var v in candidates)
                {
                    var d   = BusinessManager.DiagnoseVessel(v);
                    bool sel = _selectedVesselId.HasValue && _selectedVesselId.Value == v.id;

                    // Header button -- green if qualifying, red if not
                    string header = sel ? "▼  " : "▶  ";
                    header += $"{v.vesselName}  |  {v.mainBody.bodyName}  |  {v.situation}";
                    if (d.IsQualifying)
                        header += $"  |  {d.businessType}";

                    GUIStyle btnStyle = d.IsQualifying ? _styleGreen : _styleRed;
                    if (GUILayout.Button(header, btnStyle, GUILayout.ExpandWidth(true)))
                        _selectedVesselId = (sel ? (Guid?)null : v.id);

                    // Detail panel when selected
                    if (sel)
                        DrawVesselDetail(v, d);
                }

                GUILayout.EndScrollView();
            }

            // ---- Commented-out manual business creation buttons ----
            // Kept here in case we want to re-enable custom non-vessel businesses later.
            //
            // GUILayout.Space(10);
            // GUILayout.Label("Start a New Business:");
            // if (GUILayout.Button("Convenience Store ($50k startup / $10k/mo)", GUILayout.Width(350)))
            //     TryStartBusiness("Convenience Store", 50000, 10000);
            // if (GUILayout.Button("Grocery Store ($500k / $100k/mo)", GUILayout.Width(350)))
            //     TryStartBusiness("Grocery Store", 500000, 100000);
            // if (GUILayout.Button("High-Rise Apartments ($5M / $1M/mo)", GUILayout.Width(350)))
            //     TryStartBusiness("Residential Real Estate", 5000000, 1000000);
        }

        private void DrawVesselDetail(Vessel v, BusinessManager.VesselDiagnostic d)
        {
            GUILayout.BeginVertical(GUI.skin.box);

            if (GUILayout.Button("Switch To Vessel", GUILayout.Width(180)))
                SwitchToVessel(v);

            GUILayout.Space(4);

            // Qualification checklist
            DrawCheck("Crew",           d.crew,      $"{d.crewCount} present, {BusinessManager.MinCrew} needed");
            DrawCheck("Science Lab",    d.sciLab,    "ModuleScienceLab required");
            DrawCheck("Drill",          d.harvester, "ModuleResourceHarvester required");
            DrawCheck("ISRU",           d.converter, "ModuleResourceConverter required");
            DrawCheck("Ore Storage",    d.ore,       "ore tank with capacity > 0 required");
            DrawCheck("CommNet",        d.commNet,   "relay link required");
            DrawCheck("Power",          d.power,     $"{d.ecAmount:F0} EC  (min {BusinessManager.MinEC})");

            GUILayout.Space(4);

            if (d.IsQualifying)
            {
                var br = BusinessManager.GetIncomeBreakdown(v, d.businessType);

                // Base rate line
                GUILayout.Label(
                    $"  Base:  {BusinessManager.FundsPerHour:F0} ₦/hr   {BusinessManager.SciencePerHour:F2} sci/hr",
                    _styleDetail);

                // Always show modifier line so it's clear what's being applied even at 0%.
                {
                    string modLine = " ";
                    if (d.businessType != "Standard")
                        modLine += $" {d.businessType} ×{br.TypeMult:F2}  ·";
                    string bonusSign = br.BodyBonus >= 0 ? "+" : "";
                    modLine += $"  {v.mainBody.bodyName} ({br.BodySituation}) {bonusSign}{br.BodyBonus * 100:F0}%";
                    GUILayout.Label(modLine, _styleDetail);
                }

                // Final rate line
                GUILayout.Label(
                    $"  → {br.FundsPerHour:F0} ₦/hr   {br.SciencePerHour:F2} sci/hr",
                    _styleGreenLabel);

                if (d.businessType == "Security")
                    GUILayout.Label("  Security: sabotage chance halved on this body.", _styleDetail);
            }

            GUILayout.EndVertical();
        }

        private void SwitchToVessel(Vessel v)
        {
            if (v == null) return;

            if (HighLogic.LoadedSceneIsFlight)
            {
                // Already in flight -- switch active vessel directly
                FlightGlobals.SetActiveVessel(v);
            }
            else
            {
                // Save state then hand off to FlightDriver for the target vessel.
                // Second arg is the vessel's index in flightState.protoVessels.
                GamePersistence.SaveGame("persistent", HighLogic.SaveFolder, SaveMode.OVERWRITE);
                int idx = HighLogic.CurrentGame.flightState.protoVessels
                    .FindIndex(pv => pv.vesselID == v.id);
                if (idx >= 0)
                    FlightDriver.StartAndFocusVessel("persistent", idx);
                else
                    Debug.LogWarning($"[TimMod] SwitchToVessel: could not find protoVessel for {v.vesselName}");
            }
        }

        private void DrawCheck(string label, bool pass, string detail)
        {
            string mark  = pass ? "✓" : "✗";
            Color  color = pass ? new Color(0.4f, 1f, 0.4f) : new Color(1f, 0.45f, 0.45f);

            GUIStyle st = new GUIStyle(_styleDetail);
            st.normal.textColor = color;

            string text = pass
                ? $"  {mark}  {label}"
                : $"  {mark}  {label}  --  {detail}";

            GUILayout.Label(text, st);
        }


        // Launch To tab: VAB/SPH only for selection; other scenes show status only.
        private void DrawLaunchToTab()
        {
            bool inEditor = HighLogic.LoadedSceneIsEditor;

            // Auto-log once when tab first draws in editor — catches startup state.
            if (inEditor && !_launchDiagLogged)
            {
                _launchDiagLogged = true;
                LogLaunchDiagnostics("auto on first editor draw");
            }

            // Current destination status (visible in all scenes)
            if (LaunchDestination.IsPending)
            {
                GUILayout.Label(
                    $"Destination: {LaunchDestination.TargetVesselName}  ({LaunchDestination.BodyName})",
                    _styleGreenLabel);
                if (GUILayout.Button("Clear", GUILayout.Width(80)))
                    LaunchDestination.Clear();
                GUILayout.Space(8);
            }

            if (!inEditor)
            {
                GUILayout.Label("Open in VAB or SPH to pick a launch destination.", _styleGray);
                return;
            }

            GUILayout.Label("Pick a base to launch near:", HighLogic.Skin?.label ?? GUI.skin.label);
            GUILayout.Space(4);

            // Manual diagnostics button — dumps full vessel state to KSP.log on click.
            if (GUILayout.Button("Dump Vessel Diagnostics", GUILayout.Width(220)))
                LogLaunchDiagnostics("manual button press");
            GUILayout.Space(4);

            var candidates = GetLaunchCandidates();
            if (candidates.Count == 0)
            {
                GUILayout.Label("No off-world vessels found.", _styleGray);
            }
            else
            {
                _launchScrollPos = GUILayout.BeginScrollView(_launchScrollPos, GUILayout.Height(260));
                foreach (var c in candidates)
                {
                    string lbl = $"{c.Name}  |  {c.BodyName}  |  {c.Situation}";
                    bool   sel = LaunchDestination.IsPending &&
                                 LaunchDestination.TargetVesselName == c.Name;
                    GUIStyle st = sel ? _styleGreen : (GUI.skin?.button ?? new GUIStyle());
                    if (GUILayout.Button(lbl, st, GUILayout.ExpandWidth(true)))
                        LaunchDestination.Set(c);
                }
                GUILayout.EndScrollView();
            }

            GUILayout.Space(4);
            GUILayout.Label("Launch normally — craft will appear near the selected base.", _styleGray);
        }

        // Dumps full vessel state to KSP.log. Call from button or auto on first draw.
        private void LogLaunchDiagnostics(string trigger)
        {
            Debug.Log($"[TimMod] === LaunchTo Diagnostics ({trigger}) ===");
            Debug.Log($"[TimMod]   Scene:              {HighLogic.LoadedScene}");
            Debug.Log($"[TimMod]   LoadedSceneIsEditor: {HighLogic.LoadedSceneIsEditor}");

            // FlightGlobals.Vessels
            if (FlightGlobals.Vessels == null)
            {
                Debug.Log("[TimMod]   FlightGlobals.Vessels: NULL");
            }
            else
            {
                Debug.Log($"[TimMod]   FlightGlobals.Vessels.Count: {FlightGlobals.Vessels.Count}");
                foreach (var v in FlightGlobals.Vessels)
                {
                    if (v == null) { Debug.Log("[TimMod]     (null vessel entry)"); continue; }
                    Debug.Log($"[TimMod]     '{v.vesselName}'  type={v.vesselType}  body={v.mainBody?.bodyName ?? "NULL"}  sit={v.situation}  loaded={v.loaded}");
                }
            }

            // protoVessels — always in save state regardless of scene
            var fs = HighLogic.CurrentGame?.flightState;
            if (fs == null)
            {
                Debug.Log("[TimMod]   flightState: NULL");
            }
            else if (fs.protoVessels == null)
            {
                Debug.Log("[TimMod]   protoVessels: NULL");
            }
            else
            {
                Debug.Log($"[TimMod]   protoVessels.Count: {fs.protoVessels.Count}");
                foreach (var pv in fs.protoVessels)
                {
                    if (pv == null) { Debug.Log("[TimMod]     (null protoVessel)"); continue; }
                    Debug.Log($"[TimMod]     '{pv.vesselName}'  type={pv.vesselType}  body={pv.orbitSnapShot?.ReferenceBodyIndex}  sit={pv.situation}  landed={pv.landed}");
                }
            }

            Debug.Log("[TimMod] === end diagnostics ===");
        }

        // Editor: FlightGlobals.Vessels is always empty; fall back to protoVessels.
        // Other scenes: use live Vessels as before.
        private List<LaunchCandidate> GetLaunchCandidates()
        {
            var result = new List<LaunchCandidate>();

            if (HighLogic.LoadedSceneIsEditor)
            {
                var fs = HighLogic.CurrentGame?.flightState;
                if (fs?.protoVessels == null) return result;
                foreach (var pv in fs.protoVessels)
                {
                    if (pv == null) continue;
                    if (pv.vesselType == VesselType.Debris      ||
                        pv.vesselType == VesselType.Flag        ||
                        pv.vesselType == VesselType.EVA         ||
                        pv.vesselType == VesselType.SpaceObject) continue;
                    int bodyIdx = pv.orbitSnapShot?.ReferenceBodyIndex ?? -1;
                    if (bodyIdx < 0 || bodyIdx >= FlightGlobals.Bodies.Count) continue;
                    CelestialBody body = FlightGlobals.Bodies[bodyIdx];
                    if (body.bodyName == "Kerbin") continue;
                    var sit = pv.situation;
                    if (sit != Vessel.Situations.LANDED   &&
                        sit != Vessel.Situations.ORBITING  &&
                        sit != Vessel.Situations.SPLASHED) continue;
                    // Only show qualifying business bases as launch destinations.
                    if (!BusinessManager.IsQualifyingBase(pv)) continue;
                    result.Add(new LaunchCandidate {
                        Name      = pv.vesselName,
                        BodyName  = body.bodyName,
                        Situation = sit,
                        Lat       = pv.latitude,
                        Lon       = pv.longitude,
                        IsOrbital = sit == Vessel.Situations.ORBITING
                    });
                }
            }
            else
            {
                if (FlightGlobals.Vessels == null) return result;
                foreach (var v in FlightGlobals.Vessels)
                {
                    if (v == null) continue;
                    if (v.vesselType == VesselType.Debris      ||
                        v.vesselType == VesselType.Flag        ||
                        v.vesselType == VesselType.EVA         ||
                        v.vesselType == VesselType.SpaceObject) continue;
                    if (v.mainBody == null || v.mainBody.bodyName == "Kerbin") continue;
                    var sit = v.situation;
                    if (sit != Vessel.Situations.LANDED   &&
                        sit != Vessel.Situations.ORBITING  &&
                        sit != Vessel.Situations.SPLASHED) continue;
                    // Only show qualifying business bases as launch destinations.
                    if (!BusinessManager.DiagnoseVessel(v).IsQualifying) continue;
                    bool isOrbital = sit == Vessel.Situations.ORBITING;
                    var c = new LaunchCandidate {
                        Name      = v.vesselName,
                        BodyName  = v.mainBody.bodyName,
                        Situation = sit,
                        Lat       = v.latitude,
                        Lon       = v.longitude,
                        IsOrbital = isOrbital
                    };
                    if (isOrbital && v.orbit != null)
                    {
                        c.SMA      = v.orbit.semiMajorAxis;
                        c.Ecc      = v.orbit.eccentricity;
                        c.Inc      = v.orbit.inclination;
                        c.LAN      = v.orbit.LAN;
                        c.ArgPe    = v.orbit.argumentOfPeriapsis;
                        c.MeanAnom = v.orbit.meanAnomalyAtEpoch;
                        c.Epoch    = v.orbit.epoch;
                    }
                    result.Add(c);
                }
            }
            return result;
        }

        // ---- Kept intact but currently not routed to from the tab bar ----

        private void DrawRoutesTab()
        {
            GUILayout.Label("Active Routes:");
            var routes = _deliveryManager.GetActiveRoutes();
            if (routes == null || routes.Count == 0)
            {
                GUILayout.Label("(none yet)");
                return;
            }
            GUILayout.Space(10);
            if (GUILayout.Button("Clean Up Invalid Routes"))
                CleanupInvalidRoutes();
            foreach (var route in routes)
            {
                float freqDays = route.FrequencyHours / 6f;
                GUILayout.Label($"• {route.Origin} → {route.Destination} | {route.ResourceType} | Every {freqDays:F1}h");
            }
        }

        private void RefreshVesselList()
        {
            _vesselOptions = FlightGlobals.Vessels
                .Where(v => v != null &&
                       !string.IsNullOrEmpty(v.vesselName) &&
                       v.vesselType != VesselType.SpaceObject &&
                       v.vesselType != VesselType.Debris &&
                       v.vesselType != VesselType.EVA &&
                       v.vesselType != VesselType.Flag &&
                       v.vesselType != VesselType.Unknown)
                .ToList();
            _vesselNames = _vesselOptions.Select(v => v.vesselName).ToArray();
            if (_selectedVesselIndex >= _vesselNames.Length)
                _selectedVesselIndex = 0;
        }

        private void DrawCreateRouteTab()
        {
            GUILayout.Label("Create New Logistics Route:");
            if (_vesselOptions.Count == 0) RefreshVesselList();
            GUILayout.Space(10);
            GUILayout.Label("Destination Vessel:");
            _selectedVesselIndex = GUILayout.SelectionGrid(_selectedVesselIndex, _vesselNames, 1, GUILayout.Width(300));
            GUILayout.Space(10);
            GUILayout.Label("Resources to Deliver:");
            _sendFuel = GUILayout.Toggle(_sendFuel, "Fuel");
            _sendFood = GUILayout.Toggle(_sendFood, "Food");
            GUILayout.Space(10);
            GUILayout.Label("Delivery Frequency (in days):");
            _deliveryFrequencyText = GUILayout.TextField(_deliveryFrequencyText, GUILayout.Width(100));
            GUILayout.Space(10);
            if (GUILayout.Button("Create Route and Deliver Now", GUILayout.Width(220)))
            {
                if (!_sendFuel && !_sendFood)
                {
                    ScreenMessages.PostScreenMessage("No resources selected for delivery!", 4f, ScreenMessageStyle.UPPER_LEFT);
                    return;
                }
                Vessel target = _vesselOptions[_selectedVesselIndex];
                if (_sendFuel)
                {
                    float frequencyDays = 1f;
                    if (!float.TryParse(_deliveryFrequencyText, out frequencyDays)) frequencyDays = 1f;
                    float frequencyHours = frequencyDays * 6f;
                    var route = new Route(Guid.NewGuid().ToString(), "KSC", target.vesselName, "Fuel", 100, frequencyHours);
                    _deliveryManager.RegisterRoute(route);
                    LogisticsScenario.Instance?.SavedRoutes.Add(route);
                    TriggerDelivery("Fuel", target);
                }
                if (_sendFood) TriggerDelivery("Food", target);
            }
            GUILayout.Space(10);
            if (GUILayout.Button("Refresh Vessel List", GUILayout.Width(180)))
                RefreshVesselList();
        }

        private void DrawHistoryTab()
        {
            GUILayout.Label("Past Deliveries:");
            var history = _deliveryManager.GetDeliveryHistory();
            if (history == null || history.Count == 0) { GUILayout.Label("(none yet)"); return; }
            foreach (var record in history)
                GUILayout.Label($"• UT {record.GameTime:F1}: {record.ResourceType} {record.QuantityDelivered} from {record.Origin} → {record.Destination} | Success: {record.Success}");
        }

        public float GetDeliveryCost() => 1000f;

        private void TriggerDelivery(string resource, Vessel target)
        {
            float cost = GetDeliveryCost();
            if (Funding.CanAfford(cost))
            {
                Funding.Instance.AddFunds(-cost, TransactionReasons.None);
                foreach (Part p in target.parts)
                    foreach (PartResource r in p.Resources)
                        r.amount = r.maxAmount;
                ScreenMessages.PostScreenMessage($"{resource} delivered to {target.vesselName}.", 5f, ScreenMessageStyle.UPPER_LEFT);
            }
            else
            {
                ScreenMessages.PostScreenMessage($"Not enough funds to deliver {resource}!", 5f, ScreenMessageStyle.UPPER_LEFT);
            }
        }

        public void ProcessRecurringDeliveries() => _deliveryManager.ProcessDeliveries();

        private void TryStartBusiness(string type, float startupCost, float monthlyIncome)
        {
            if (!Funding.CanAfford(startupCost))
            {
                ScreenMessages.PostScreenMessage($"Not enough funds to start {type}", 4f, ScreenMessageStyle.UPPER_LEFT);
                return;
            }
            Funding.Instance.AddFunds(-startupCost, TransactionReasons.Structures);
            var biz = new Business(type, startupCost, monthlyIncome);
            LogisticsScenario.Instance.ActiveBusinesses.Add(biz);
            ScreenMessages.PostScreenMessage($"Started {type}! Income: ${monthlyIncome}/mo", 4f, ScreenMessageStyle.UPPER_LEFT);
        }

        private void ApplyMissedDeliveries(Vessel vessel)
        {
            var routes = _deliveryManager.GetActiveRoutes();
            double now  = Planetarium.GetUniversalTime();
            foreach (var route in routes)
            {
                double elapsed  = now - route.LastDeliveryTime;
                float  interval = route.FrequencyHours * 3600f;
                int    due      = (int)(elapsed / interval);
                if (due <= 0) continue;
                Debug.Log($"[TimMod] {due} deliveries due to {vessel.vesselName}");
                for (int i = 0; i < due; i++) TriggerDelivery(route.ResourceType, vessel);
                route.LastDeliveryTime += due * interval;
                var saved = LogisticsScenario.Instance?.SavedRoutes.FirstOrDefault(r => r.Id == route.Id);
                if (saved != null) saved.LastDeliveryTime = route.LastDeliveryTime;
            }
        }
    }
}
