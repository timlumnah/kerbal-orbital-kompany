using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Koko
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class SyndicateZonesManager : MonoBehaviour
    {
        private static readonly Dictionary<Guid, float> lastTriggerTime = new Dictionary<Guid, float>();
        private System.Random rng = new System.Random();

        // Respawn check runs every 60 real seconds (not every frame).
        // Planetarium UT comparison inside UpdateRespawn handles warp correctly.
        private float _respawnCheckTimer = 0f;
        private const float RESPAWN_CHECK_INTERVAL = 60f;

        // Orbital scan: check all orbiting vessels every 5 real seconds.
        private float _orbitalScanTimer = 0f;
        private const float ORBITAL_SCAN_INTERVAL = 5f;
        // Physics-range craft spawn threshold (meters).
        private const double PHYSICS_SPAWN_RANGE = 2500.0;
        // Tracks bodies currently defended (all cleared + orbiting defender).
        // Used to detect transitions and post notification messages.
        private HashSet<string> _defendedBodies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Removed debug sphere system and SpawnCheck log throttle (2026-06-04-1331).
        // Debug spheres were leftover from craft spawning work; caused visible red
        // sphere artifact near syndicate locations in flight.

        void Start()
        {
            SyndicateManager.Load();
            // Load detection score weights from SYNDICATE_SETTINGS config node.
            // Sets static fields on RandomPartExplosionUtil; defaults used if absent.
            LoadSyndicateSettings();
            Debug.Log("[TimMod] SyndicateZonesManager active.");
        }


        // Reads SYNDICATE_SETTINGS from GameDatabase and pushes values to util.
        private void LoadSyndicateSettings()
        {
            try
            {
                var nodes = GameDatabase.Instance.GetConfigNodes("SYNDICATE_SETTINGS");
                if (nodes == null || nodes.Length == 0)
                {
                    Debug.Log("[TimMod] SYNDICATE_SETTINGS not found -- using defaults.");
                    return;
                }

                var cfg = nodes[0];
                float v = 0f;

                if (cfg.TryGetValue("partCountWeight",   ref v)) RandomPartExplosionUtil.partCountWeight   = v;
                if (cfg.TryGetValue("massWeight",        ref v)) RandomPartExplosionUtil.massWeight        = v;
                if (cfg.TryGetValue("tempDivisor",       ref v)) RandomPartExplosionUtil.tempDivisor       = v;
                if (cfg.TryGetValue("stealthMultiplier", ref v)) RandomPartExplosionUtil.stealthMultiplier = v;
                if (cfg.TryGetValue("maxDetectionScore", ref v)) RandomPartExplosionUtil.maxDetectionScore = v;

                double rd = 0;
                if (cfg.TryGetValue("respawnDurationSeconds", ref rd))
                    SyndicateManager.respawnDurationSeconds = rd;

                Debug.Log($"[TimMod] SYNDICATE_SETTINGS loaded -- " +
                          $"partW={RandomPartExplosionUtil.partCountWeight} " +
                          $"massW={RandomPartExplosionUtil.massWeight} " +
                          $"tempDiv={RandomPartExplosionUtil.tempDivisor} " +
                          $"stealthMult={RandomPartExplosionUtil.stealthMultiplier} " +
                          $"maxScore={RandomPartExplosionUtil.maxDetectionScore}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[TimMod] Failed to load SYNDICATE_SETTINGS: " + ex.Message);
            }
        }

        // Rewrote Update() to handle three detection rings per location.
        // Discovery state (discovered/revealed) checked for all locations each frame.
        // Sabotage still fires at most once per frame.
        void Update()
        {
            if (!HighLogic.LoadedSceneIsFlight) return;

            var v = FlightGlobals.ActiveVessel;
            if (v == null || v.mainBody == null) return;

            if (!lastTriggerTime.TryGetValue(v.id, out var last))
                last = -9999f;

            var now = Time.realtimeSinceStartup;
            string bodyName = v.mainBody.bodyName;
            Vector3d vesselWorld = v.GetWorldPos3D();

            bool sabotageThisFrame = false;

            foreach (var loc in SyndicateManager.GetLocationsForBody(bodyName))
            {
                if (loc.destroyed) continue;

                // Bug fix: use terrain altitude so distance is measured to the actual
                // surface, not sea level. Eeloo terrain can be 3+km above reference sphere,
                // causing reveals to miss even when the vessel is directly above the location.
                double terrainAlt = v.mainBody.TerrainAltitude(loc.lat, loc.lon, false);
                Vector3d locWorld = v.mainBody.GetWorldSurfacePosition(loc.lat, loc.lon, terrainAlt);
                double dist = Vector3d.Distance(vesselWorld, locWorld);

                // -- Reveal ring (smallest): full location info --
                if (dist <= loc.EffectiveRevealRadius && !loc.revealed)
                {
                    loc.revealed   = true;
                    loc.discovered = true;
                    Debug.Log($"[TimMod] Syndicate location revealed: {loc.name}");
                    ScreenMessages.PostScreenMessage(
                        $"Syndicate outpost located: {loc.name}", 4f, ScreenMessageStyle.UPPER_CENTER);
                }
                // -- Inner ring: map discovery (no message if already revealed) --
                else if (dist <= loc.EffectiveDetectionRadius && !loc.discovered)
                {
                    loc.discovered = true;
                    Debug.Log($"[TimMod] Syndicate location discovered: {loc.name}");
                    ScreenMessages.PostScreenMessage(
                        "Syndicate activity detected nearby.", 4f, ScreenMessageStyle.UPPER_CENTER);
                }

                // -- Outer ring: sabotage trigger (one per frame max) --
                if (sabotageThisFrame) continue;
                if (dist > loc.EffectiveDangerRadius) continue;

                float cd = loc.cooldownSeconds > 0 ? loc.cooldownSeconds : 10f;
                if (now - last < cd) continue;

                float chance = loc.explodeChance > 0 ? loc.explodeChance : 1.0f;
                if (rng.NextDouble() >= chance) continue;

                // ---- SABOTAGE DISABLED -- uncomment to re-enable ----
                // bool kaboom = RandomPartExplosionUtil.ExplodeOne(v);
                bool kaboom = false;

                lastTriggerTime[v.id] = now;
                sabotageThisFrame = true;

                if (kaboom)
                    ScreenMessages.PostScreenMessage(
                        $"⚠ {loc.name}: Syndicate sabotage!", 3f, ScreenMessageStyle.UPPER_CENTER);
            }


            _respawnCheckTimer += Time.deltaTime;
            if (_respawnCheckTimer >= RESPAWN_CHECK_INTERVAL)
            {
                _respawnCheckTimer = 0f;
                SyndicateManager.UpdateRespawn();
            }

            // Orbital scan: find all controllable orbiting vessels and approx-discover
            // syndicate locations on their body. Runs every 5 real seconds.
            _orbitalScanTimer += Time.deltaTime;
            if (_orbitalScanTimer >= ORBITAL_SCAN_INTERVAL)
            {
                _orbitalScanTimer = 0f;
                RunOrbitalScan();
            }
        }

        // Orbital scan: check all orbiting vessels for syndicate signals.
        // Controllable = command module + CommNet signal (v.IsControllable covers both).
        // Two scan modes:
        //   Telescope (ModuleSyndicateTelescope): full reveal on that body.
        //   Standard controllable vessel: approx-discover (yellow ring).
        // Also tracks per-body defender status and notifies on transition.
        private void RunOrbitalScan()
        {
            if (FlightGlobals.Vessels == null) return;

            foreach (Vessel vessel in FlightGlobals.Vessels)
            {
                if (vessel.situation != Vessel.Situations.ORBITING) continue;
                if (!vessel.IsControllable) continue;

                string bodyName   = vessel.mainBody.name;
                double bodyRadius = vessel.mainBody.Radius;

                // Telescope: full reveal for all undestroyed locations on this body
                bool hasTelescope = vessel.Parts != null && vessel.Parts.Any(
                    p => p.FindModuleImplementing<ModuleSyndicateTelescope>() != null);

                if (hasTelescope)
                {
                    bool anyRevealed = false;
                    foreach (var syn in SyndicateManager.AllSyndicates)
                    {
                        foreach (var loc in syn.locations)
                        {
                            if (loc.destroyed || loc.revealed) continue;
                            if (!string.Equals(loc.body, bodyName,
                                    StringComparison.OrdinalIgnoreCase)) continue;
                            loc.discovered = true;
                            loc.revealed   = true;
                            anyRevealed    = true;
                            Debug.Log($"[TimMod] Telescope full-reveal: {loc.name} on {loc.body}");
                        }
                    }
                    if (anyRevealed)
                        ScreenMessages.PostScreenMessage(
                            $"Syndicate detector: scan complete. Locations revealed on {bodyName}.",
                            6f, ScreenMessageStyle.UPPER_CENTER);
                }

                // Standard probe: approx-discover undiscovered locations (yellow ring)
                foreach (var syn in SyndicateManager.AllSyndicates)
                {
                    foreach (var loc in syn.locations)
                    {
                        if (loc.destroyed || loc.approxDiscovered) continue;
                        if (!string.Equals(loc.body, bodyName, StringComparison.OrdinalIgnoreCase)) continue;

                        double offsetDist = rng.NextDouble() * 40000.0 + 20000.0; // 20-60km
                        double direction  = rng.NextDouble() * 2.0 * Math.PI;
                        double angularOff = offsetDist / bodyRadius;
                        double latRad     = loc.lat * Math.PI / 180.0;

                        loc.approxLat = loc.lat + angularOff * Math.Cos(direction) * (180.0 / Math.PI);
                        loc.approxLon = loc.lon + angularOff * Math.Sin(direction)
                                        / Math.Cos(latRad) * (180.0 / Math.PI);
                        loc.approxDiscovered = true;

                        Debug.Log($"[TimMod] Orbital scan: approx ring for {loc.name} on {loc.body} " +
                                  $"at ({loc.approxLat:F2}, {loc.approxLon:F2})");
                        ScreenMessages.PostScreenMessage(
                            "Syndicate signals detected from orbit. Zone marked on map.",
                            5f, ScreenMessageStyle.UPPER_CENTER);
                    }
                }
            }

            // Defender transition: notify when a body first becomes fully cleared + defended
            var allBodies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var syn in SyndicateManager.AllSyndicates)
                foreach (var loc in syn.locations)
                    allBodies.Add(loc.body);

            var currentDefended = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string body in allBodies)
                if (SyndicateManager.IsBodyDefended(body))
                    currentDefended.Add(body);

            foreach (string body in currentDefended)
                if (!_defendedBodies.Contains(body))
                    ScreenMessages.PostScreenMessage(
                        $"Syndicate cleared: {body} sector defended. Respawn suppressed.",
                        6f, ScreenMessageStyle.UPPER_CENTER);

            _defendedBodies = currentDefended;
        }

        private static Vector3 ParseVec3(string s)
        {
            var p = s.Trim().Split(new char[]{','});
            if (p.Length < 3) return Vector3.zero;
            return new Vector3(
                float.Parse(p[0].Trim()), float.Parse(p[1].Trim()), float.Parse(p[2].Trim()));
        }

        private static string FormatVec3(Vector3 v)
            => $"{v.x},{v.y},{v.z}";

        // Verbose-logging version for spawn debugging.
        // Every step logged so we can see exactly where failure occurs.
        private void SpawnSyndicateCraft(SyndicateLocation loc)
        {
            Debug.Log($"[TimMod][Spawn] === SpawnSyndicateCraft BEGIN === loc={loc.name} body={loc.body} lat={loc.lat:F4} lon={loc.lon:F4}");

            // ---- Craft file ----
            // syndicate_outpost_1.craft (1-part): spawn confirmed working.
            // Switched to full 35-part outpost for multi-part test.
            const string REL_PATH = "GameData/KoKo/Crafts/syndicate_craft_1.craft";
            string craftPath = KSPUtil.ApplicationRootPath + REL_PATH;
            Debug.Log($"[TimMod][Spawn] ApplicationRootPath={KSPUtil.ApplicationRootPath}");
            Debug.Log($"[TimMod][Spawn] Full craft path={craftPath}");
            Debug.Log($"[TimMod][Spawn] File.Exists={File.Exists(craftPath)}");

            if (!File.Exists(craftPath))
            {
                Debug.LogWarning("[TimMod][Spawn] ABORT: craft file not found.");
                return;
            }

            // ---- Resolve body ----
            Debug.Log($"[TimMod][Spawn] FlightGlobals.Bodies count={FlightGlobals.Bodies.Count}");
            foreach (var b2 in FlightGlobals.Bodies)
                Debug.Log($"[TimMod][Spawn]   body available: {b2.name}");

            CelestialBody body = FlightGlobals.Bodies.FirstOrDefault(b =>
                string.Equals(b.name, loc.body, StringComparison.OrdinalIgnoreCase));
            if (body == null)
            {
                Debug.LogWarning($"[TimMod][Spawn] ABORT: body '{loc.body}' not found in FlightGlobals.Bodies.");
                return;
            }
            int bodyIdx = FlightGlobals.Bodies.IndexOf(body);
            Debug.Log($"[TimMod][Spawn] Body resolved: name={body.name} idx={bodyIdx} Radius={body.Radius:F0}m");

            // ---- Load craft ConfigNode ----
            ConfigNode craftNode = ConfigNode.Load(craftPath);
            if (craftNode == null)
            {
                Debug.LogWarning("[TimMod][Spawn] ABORT: ConfigNode.Load returned null.");
                return;
            }
            Debug.Log($"[TimMod][Spawn] craftNode loaded. ship name='{craftNode.GetValue("ship")}' " +
                      $"top-level keys: {craftNode.CountValues} values, {craftNode.CountNodes} child nodes");

            ConfigNode[] craftParts = craftNode.GetNodes("PART");
            Debug.Log($"[TimMod][Spawn] PART nodes in craft file: {craftParts.Length}");
            for (int i = 0; i < craftParts.Length; i++)
            {
                string pf = craftParts[i].GetValue("part") ?? "(none)";
                string pp = craftParts[i].GetValue("pos")  ?? "(none)";
                Debug.Log($"[TimMod][Spawn]   craft PART[{i}]: part={pf} pos={pp}");
            }

            if (craftParts.Length == 0)
            {
                Debug.LogWarning("[TimMod][Spawn] ABORT: no PART nodes in craft file.");
                return;
            }

            // ---- Altitude / spawn position ----
            double terrainAlt = body.TerrainAltitude(loc.lat, loc.lon, false);
            // Increased offset from 10m to 50m -- gives vessel room to settle onto
            // terrain without spawning parts into ground on physics load.
            double spawnAlt   = terrainAlt + 50.0;
            Debug.Log($"[TimMod][Spawn] terrainAlt={terrainAlt:F2}m spawnAlt={spawnAlt:F2}m");
            Vector3d spawnWorld = body.GetWorldSurfacePosition(loc.lat, loc.lon, spawnAlt);
            Debug.Log($"[TimMod][Spawn] spawnWorld=({spawnWorld.x:F1},{spawnWorld.y:F1},{spawnWorld.z:F1})");

            Vector3 rootEditorPos = ParseVec3(craftParts[0].GetValue("pos") ?? "0,0,0");
            Debug.Log($"[TimMod][Spawn] rootEditorPos=({rootEditorPos.x:F3},{rootEditorPos.y:F3},{rootEditorPos.z:F3})");

            // ---- Surface normal + upright rotation ----
            // Compute surface normal at spawn point so vessel stands upright.
            // Identity quaternion (0,0,0,1) leaves vessel in world-axis alignment,
            // which is ~90 degrees off on any surface point away from lon=0.
            Vector3d surfacePos0 = body.GetWorldSurfacePosition(loc.lat, loc.lon, 0);
            Vector3 surfaceNormal = ((surfacePos0 - body.position) / body.Radius).normalized;
            Quaternion upright = Quaternion.FromToRotation(Vector3.up, surfaceNormal);
            string nrmStr = $"{surfaceNormal.x:R},{surfaceNormal.y:R},{surfaceNormal.z:R}";
            string rotStr = $"{upright.x:R},{upright.y:R},{upright.z:R},{upright.w:R}";
            Debug.Log($"[TimMod][Spawn] surfaceNormal=({surfaceNormal.x:F4},{surfaceNormal.y:F4},{surfaceNormal.z:F4}) upright=({upright.x:F4},{upright.y:F4},{upright.z:F4},{upright.w:F4})");

            // ---- Build VESSEL ConfigNode ----
            string vesselGuid = System.Guid.NewGuid().ToString();
            Debug.Log($"[TimMod][Spawn] Building VESSEL node. pid={vesselGuid}");
            ConfigNode vesselNode = new ConfigNode("VESSEL");
            vesselNode.AddValue("name",     "Syndicate Outpost");
            vesselNode.AddValue("type",     ((int)VesselType.Base).ToString());
            vesselNode.AddValue("sit",      Vessel.Situations.LANDED.ToString());
            vesselNode.AddValue("landed",   "True");
            vesselNode.AddValue("landedAt", body.name);
            vesselNode.AddValue("lat",      loc.lat.ToString("R"));
            vesselNode.AddValue("lon",      loc.lon.ToString("R"));
            vesselNode.AddValue("alt",      spawnAlt.ToString("R"));
            vesselNode.AddValue("hgt",      "10");
            vesselNode.AddValue("nrm",      nrmStr);
            vesselNode.AddValue("rot",      rotStr);
            vesselNode.AddValue("CoM",      "0,0,0");
            vesselNode.AddValue("stg",      "0");
            vesselNode.AddValue("prst",     "False");
            vesselNode.AddValue("ref",      bodyIdx.ToString());
            vesselNode.AddValue("ctrl",     "True");
            vesselNode.AddValue("flag",     "Squad/Flags/default");
            // Bug fix: save format uses "pid" (lowercase), not "PID".
            // "PID" was silently ignored -> vesselID came out as 00000000...
            vesselNode.AddValue("pid",      vesselGuid);

            ConfigNode orb = vesselNode.AddNode("ORBIT");
            orb.AddValue("SMA", (body.Radius + spawnAlt).ToString("R"));
            orb.AddValue("ECC", "0"); orb.AddValue("INC", "0");
            orb.AddValue("LPE", "0"); orb.AddValue("LAN", "0");
            orb.AddValue("MNA", "0"); orb.AddValue("EPH", "0");
            orb.AddValue("REF", bodyIdx.ToString());
            Debug.Log($"[TimMod][Spawn] ORBIT node added. SMA={body.Radius + spawnAlt:F0}m REF={bodyIdx}");

            // Bug fix: node name must be "PART" not "PROTOPART".
            // ProtoVessel constructor reads GetNodes("PART") -- "PROTOPART" returned
            // 0 results, so protoPartSnapshots was empty and vessel spawned invisible.
            // Also: key must be "name" not "partName" (ProtoPartSnapshot reads "name").
            // Also: "parent" field added -- required for valid part hierarchy.
            //   Root = -1, all others = rootUid (flat hierarchy; good enough for static outpost).
            // Removed: "uid64" (non-standard), "part" (craft-file format, not save format).

            // ---- Convert craft PART nodes -> save-format PART nodes ----
            uint nextUid  = 100000u;
            uint rootUid  = nextUid;   // uid assigned to root part
            bool isRoot   = true;
            int  partIdx  = 0;
            foreach (ConfigNode pn in craftParts)
            {
                string partField = pn.GetValue("part") ?? "";
                int ul = partField.LastIndexOf('_');
                string partName = ul > 0 ? partField.Substring(0, ul) : partField;

                Debug.Log($"[TimMod][Spawn] PART[{partIdx}]: partField='{partField}' -> partName='{partName}' isRoot={isRoot}");

                if (string.IsNullOrEmpty(partName))
                {
                    Debug.LogWarning($"[TimMod][Spawn]   partName empty, skipping PART[{partIdx}].");
                    isRoot = false; partIdx++; continue;
                }

                Vector3 editorPos = ParseVec3(pn.GetValue("pos") ?? "0,0,0");
                Vector3 relPos    = editorPos - rootEditorPos;
                uint uid = nextUid++;

                Debug.Log($"[TimMod][Spawn]   editorPos=({editorPos.x:F3},{editorPos.y:F3},{editorPos.z:F3}) " +
                          $"relPos=({relPos.x:F3},{relPos.y:F3},{relPos.z:F3}) uid={uid} parent={( isRoot ? -1 : (int)rootUid )}");

                // Count MODULE and RESOURCE nodes
                int modCount = pn.GetNodes("MODULE").Length;
                int resCount = pn.GetNodes("RESOURCE").Length;
                Debug.Log($"[TimMod][Spawn]   craft PART[{partIdx}] has {modCount} MODULE(s), {resCount} RESOURCE(s)");
                foreach (ConfigNode mod in pn.GetNodes("MODULE"))
                    Debug.Log($"[TimMod][Spawn]     MODULE: name={mod.GetValue("name") ?? "(none)"}");
                foreach (ConfigNode res in pn.GetNodes("RESOURCE"))
                    Debug.Log($"[TimMod][Spawn]     RESOURCE: name={res.GetValue("name") ?? "(none)"} " +
                              $"amount={res.GetValue("amount") ?? "?"} max={res.GetValue("maxAmount") ?? "?"}");

                // Root cause of ArgumentOutOfRangeException: ProtoPartSnapshot.Load()
                // iterates part.attachNodes and reads attN entries by index from the
                // ConfigNode. With 0 attN entries but N real attach nodes on the part,
                // it throws on index 0. Fix: look up each part in PartLoader, get its
                // real attach node list, write one "attN = nodeId, -1" per node.
                // "-1" means "not connected" -- correct for a standalone landed vessel.
                // Also add cid (craft ID = uid) and srfN (surface attach node).
                AvailablePart avPart = PartLoader.getPartInfoByName(partName);
                int attachNodeCount = 0;
                bool hasSrfAttach   = false;
                if (avPart != null && avPart.partPrefab != null)
                {
                    attachNodeCount = avPart.partPrefab.attachNodes != null
                                      ? avPart.partPrefab.attachNodes.Count : 0;
                    hasSrfAttach    = avPart.partPrefab.srfAttachNode != null;
                    Debug.Log($"[TimMod][Spawn]   PartLoader: '{partName}' found. " +
                              $"attachNodes={attachNodeCount} hasSrfAttach={hasSrfAttach}");
                }
                else
                {
                    Debug.LogWarning($"[TimMod][Spawn]   PartLoader: '{partName}' NOT FOUND in database. Skipping.");
                    isRoot = false; partIdx++; continue;
                }

                // Bug fixes from comparing spawn output to real persistent.sfs VESSEL node:
                //   parent: root = "0" not "-1" (save format uses 0 for root part)
                //   position/rotation: save uses "position"/"rotation", not "pos"/"rot"
                //   launchID: required field, was missing
                // "PART" is the save-format node name; "name" is the part config name field
                ConfigNode proto = vesselNode.AddNode("PART");
                proto.AddValue("name",         partName);
                proto.AddValue("cid",          uid.ToString());
                proto.AddValue("uid",          uid.ToString());
                proto.AddValue("mid",          "0");
                proto.AddValue("persistentId", pn.GetValue("persistentId") ?? uid.ToString());
                proto.AddValue("launchID",     "1");
                proto.AddValue("parent",       isRoot ? "0" : rootUid.ToString());
                proto.AddValue("position",     FormatVec3(relPos));
                proto.AddValue("rotation",     pn.GetValue("rot") ?? "0,0,0,1");
                proto.AddValue("mirror",       pn.GetValue("mir") ?? "1,1,1");
                proto.AddValue("symMethod",    pn.GetValue("symMethod") ?? "Radial");
                proto.AddValue("istg",         pn.GetValue("istg") ?? "-1");
                proto.AddValue("resPri",       pn.GetValue("resPri") ?? "0");
                proto.AddValue("dstg",         pn.GetValue("dstg") ?? "0");
                proto.AddValue("sidx",         pn.GetValue("sidx") ?? "-1");
                proto.AddValue("sqor",         pn.GetValue("sqor") ?? "-1");
                proto.AddValue("sepI",         pn.GetValue("sepI") ?? "-1");
                proto.AddValue("attm",         pn.GetValue("attm") ?? "0");
                proto.AddValue("modCost",      "0");
                proto.AddValue("modMass",      "0");
                proto.AddValue("modSize",      "0,0,0");

                // Attachment nodes: one attN per stack node + srfN (all unconnected = -1)
                if (avPart.partPrefab.attachNodes != null)
                    foreach (AttachNode an in avPart.partPrefab.attachNodes)
                        proto.AddValue("attN", an.id + ", -1");
                if (hasSrfAttach)
                    proto.AddValue("srfN", "srfAttach, -1");

                // Source MODULE nodes from craft file, not prefab.
                // Prefab count can differ from what KSP actually instantiates if MM patches
                // add modules to the part (e.g. ModuleSyndicateCraft on mk1-3pod).
                // Craft file was saved with the real running module list -> correct count.
                // ProtoPartSnapshot.Load() iterates part.Modules.Count and accesses
                // protoModuleSnapshots[i]. Too few entries -> ArgumentOutOfRangeException.
                // Too many entries -> safe (extras ignored). Craft file count >= prefab count.
                ConfigNode[] craftModules = pn.GetNodes("MODULE");
                int prefabModCount = avPart.partPrefab.Modules != null
                                     ? avPart.partPrefab.Modules.Count : 0;
                Debug.Log($"[TimMod][Spawn]   '{partName}': craft modules={craftModules.Length} " +
                          $"prefab modules={prefabModCount} attN={attachNodeCount} srfN={hasSrfAttach}");
                foreach (ConfigNode craftMod in craftModules)
                {
                    ConfigNode pmod = proto.AddNode("MODULE");
                    pmod.AddValue("name",           craftMod.GetValue("name") ?? "");
                    pmod.AddValue("isEnabled",      craftMod.GetValue("isEnabled") ?? "True");
                    pmod.AddValue("stagingEnabled", craftMod.GetValue("stagingEnabled") ?? "True");
                }

                isRoot = false; partIdx++;
            }

            Debug.Log($"[TimMod][Spawn] VESSEL node built. " +
                      $"PART count={vesselNode.GetNodes("PART").Length}");

            // ---- ProtoVessel construction ----
            Debug.Log($"[TimMod][Spawn] HighLogic.CurrentGame null={HighLogic.CurrentGame == null}");
            Debug.Log($"[TimMod][Spawn] flightState null={HighLogic.CurrentGame?.flightState == null}");
            Debug.Log($"[TimMod][Spawn] protoVessels count before Add={HighLogic.CurrentGame?.flightState?.protoVessels?.Count}");
            Debug.Log($"[TimMod][Spawn] FlightGlobals.Vessels count before Add={FlightGlobals.Vessels?.Count}");

            ProtoVessel pv;
            try
            {
                pv = new ProtoVessel(vesselNode, HighLogic.CurrentGame);
                Debug.Log($"[TimMod][Spawn] ProtoVessel constructed OK. " +
                          $"vesselID={pv.vesselID} vesselName='{pv.vesselName}' " +
                          $"protoPartSnapshots.Count={pv.protoPartSnapshots?.Count ?? -1} " +
                          $"situation={pv.situation} landed={pv.landed} " +
                          $"lat={pv.latitude:F4} lon={pv.longitude:F4} alt={pv.altitude:F2}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[TimMod][Spawn] ProtoVessel constructor threw: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
                return;
            }

            // ---- Add to flightState ----
            try
            {
                HighLogic.CurrentGame.flightState.protoVessels.Add(pv);
                Debug.Log($"[TimMod][Spawn] protoVessels.Add() succeeded. " +
                          $"protoVessels count after Add={HighLogic.CurrentGame.flightState.protoVessels.Count}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[TimMod][Spawn] protoVessels.Add() threw: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
                return;
            }

            // ---- Attempt pv.Load() to bring vessel into active physics scene ----
            Debug.Log($"[TimMod][Spawn] Attempting pv.Load() to load vessel into active scene...");
            try
            {
                pv.Load(HighLogic.CurrentGame.flightState);
                Debug.Log($"[TimMod][Spawn] pv.Load() returned without exception.");
                Debug.Log($"[TimMod][Spawn] FlightGlobals.Vessels count after pv.Load()={FlightGlobals.Vessels?.Count}");

                // Check if the vessel actually appeared
                Vessel spawned = FlightGlobals.Vessels?.FirstOrDefault(
                    vv => vv.id == pv.vesselID);
                if (spawned != null)
                    Debug.Log($"[TimMod][Spawn] Vessel FOUND in FlightGlobals after Load(): " +
                              $"name='{spawned.vesselName}' situation={spawned.situation} " +
                              $"loaded={spawned.loaded} packed={spawned.packed}");
                else
                    Debug.LogWarning($"[TimMod][Spawn] Vessel NOT found in FlightGlobals after Load(). " +
                                     $"vesselID={pv.vesselID}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[TimMod][Spawn] pv.Load() threw: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
                Debug.Log($"[TimMod][Spawn] pv.Load() failed -- vessel remains in flightState only (will appear on next scene load).");
            }

            string craftName = craftNode.GetValue("ship") ?? "syndicate craft";
            Debug.Log($"[TimMod][Spawn] === SpawnSyndicateCraft END === '{craftName}' ({craftParts.Length} parts) " +
                      $"at ({loc.lat:F2}, {loc.lon:F2}) on {loc.body}");
        }

        // Option B: template-based spawn using synthetically converted save-format
        // VESSEL node from syndicate_craft_1.craft. Includes correct:
        //   - parent hierarchy (batteries->crewCabin, lights->truss, etc.)
        //   - srfN references (surface-attached parts point to parent uid)
        //   - attN references (stack-connected parts point to connected uid)
        //   - MODULE blocks copied directly from craft file (correct count + content)
        //   - sameVesselCollision, attached, autostrutMode, rTrf fields
        // Part UIDs kept from craft file (unique within vessel; no uid remapping needed).
        // Only vessel-level fields (pid, lat, lon, alt, body, orbit) substituted at spawn.
        private void SpawnFromSaveTemplate(SyndicateLocation loc)
        {
            Debug.Log($"[TimMod][SpawnB] === SpawnFromSaveTemplate BEGIN === loc={loc.name}");

            const string TMPL_PATH = "GameData/KoKo/Crafts/syndicate_craft_1_template.sfs";
            string tmplFull = KSPUtil.ApplicationRootPath + TMPL_PATH;
            if (!File.Exists(tmplFull))
            {
                Debug.LogWarning($"[TimMod][SpawnB] ABORT: template not found at {tmplFull}");
                return;
            }

            ConfigNode fileRoot = ConfigNode.Load(tmplFull);
            if (fileRoot == null)
            {
                Debug.LogWarning("[TimMod][SpawnB] ABORT: ConfigNode.Load returned null.");
                return;
            }

            ConfigNode vesselNode = fileRoot.GetNode("VESSEL");
            if (vesselNode == null)
            {
                Debug.LogWarning("[TimMod][SpawnB] ABORT: no VESSEL node in template.");
                return;
            }

            // Resolve body
            CelestialBody body = FlightGlobals.Bodies.FirstOrDefault(b =>
                string.Equals(b.name, loc.body, StringComparison.OrdinalIgnoreCase));
            if (body == null)
            {
                Debug.LogWarning($"[TimMod][SpawnB] ABORT: body '{loc.body}' not found.");
                return;
            }
            int bodyIdx = FlightGlobals.Bodies.IndexOf(body);

            double terrainAlt = body.TerrainAltitude(loc.lat, loc.lon, false);
            double spawnAlt   = terrainAlt + 50.0;
            Debug.Log($"[TimMod][SpawnB] body={body.name} idx={bodyIdx} terrainAlt={terrainAlt:F1} spawnAlt={spawnAlt:F1}");

            // Surface normal + upright rotation (same fix as SpawnSyndicateCraft)
            Vector3d surfacePos0B = body.GetWorldSurfacePosition(loc.lat, loc.lon, 0);
            Vector3 surfaceNormalB = ((surfacePos0B - body.position) / body.Radius).normalized;
            Quaternion uprightB = Quaternion.FromToRotation(Vector3.up, surfaceNormalB);
            string nrmStrB = $"{surfaceNormalB.x:R},{surfaceNormalB.y:R},{surfaceNormalB.z:R}";
            string rotStrB = $"{uprightB.x:R},{uprightB.y:R},{uprightB.z:R},{uprightB.w:R}";

            // Substitute vessel-level fields
            string newPid = System.Guid.NewGuid().ToString("N");
            uint newPersId = (uint)UnityEngine.Random.Range(200000, int.MaxValue);

            vesselNode.SetValue("pid",          newPid,                    createIfNotFound: true);
            vesselNode.SetValue("persistentId", newPersId.ToString(),      createIfNotFound: true);
            vesselNode.SetValue("name",         "Syndicate Outpost",       createIfNotFound: true);
            vesselNode.SetValue("sit",          Vessel.Situations.LANDED.ToString(), createIfNotFound: true);
            vesselNode.SetValue("landed",       "True",                    createIfNotFound: true);
            vesselNode.SetValue("splashed",     "False",                   createIfNotFound: true);
            vesselNode.SetValue("launchedFrom", body.name,                 createIfNotFound: true);
            vesselNode.SetValue("landedAt",     body.name,                 createIfNotFound: true);
            vesselNode.SetValue("lat",          loc.lat.ToString("R"),     createIfNotFound: true);
            vesselNode.SetValue("lon",          loc.lon.ToString("R"),     createIfNotFound: true);
            vesselNode.SetValue("alt",          spawnAlt.ToString("R"),    createIfNotFound: true);
            vesselNode.SetValue("hgt",          "10",                      createIfNotFound: true);
            vesselNode.SetValue("nrm",          nrmStrB,                   createIfNotFound: true);
            vesselNode.SetValue("rot",          rotStrB,                   createIfNotFound: true);
            vesselNode.SetValue("ref",          bodyIdx.ToString(),        createIfNotFound: true);
            vesselNode.SetValue("prst",         "False",                   createIfNotFound: true);

            // Substitute ORBIT node
            ConfigNode orb = vesselNode.GetNode("ORBIT");
            if (orb != null)
            {
                orb.SetValue("SMA", (body.Radius + spawnAlt).ToString("R"), createIfNotFound: true);
                orb.SetValue("REF", bodyIdx.ToString(),                      createIfNotFound: true);
            }

            // Template UIDs are already self-consistent (from craft file).
            // No remapping needed. Just log counts for diagnostics.
            ConfigNode[] partNodes = vesselNode.GetNodes("PART");
            Debug.Log($"[TimMod][SpawnB] Template has {partNodes.Length} PART nodes.");
            foreach (ConfigNode pn in partNodes)
            {
                string pname    = pn.GetValue("name") ?? "?";
                string puid     = pn.GetValue("uid")  ?? "?";
                string pparent  = pn.GetValue("parent") ?? "?";
                int    modCount = pn.GetNodes("MODULE").Length;
                Debug.Log($"[TimMod][SpawnB]   PART name={pname} uid={puid} parent={pparent} modules={modCount}");
            }

            // Create and load ProtoVessel
            ProtoVessel pv;
            try
            {
                pv = new ProtoVessel(vesselNode, HighLogic.CurrentGame);
                Debug.Log($"[TimMod][SpawnB] ProtoVessel constructed. vesselID={pv.vesselID} " +
                          $"parts={pv.protoPartSnapshots?.Count ?? -1} " +
                          $"lat={pv.latitude:F4} lon={pv.longitude:F4}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[TimMod][SpawnB] ProtoVessel constructor threw: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
                return;
            }

            HighLogic.CurrentGame.flightState.protoVessels.Add(pv);
            Debug.Log($"[TimMod][SpawnB] Added to protoVessels. Count={HighLogic.CurrentGame.flightState.protoVessels.Count}");

            try
            {
                pv.Load(HighLogic.CurrentGame.flightState);
                Vessel spawned = FlightGlobals.Vessels?.FirstOrDefault(v => v.id == pv.vesselID);
                if (spawned != null)
                    Debug.Log($"[TimMod][SpawnB] SUCCESS: vessel in FlightGlobals. " +
                              $"loaded={spawned.loaded} packed={spawned.packed} situation={spawned.situation}");
                else
                    Debug.LogWarning($"[TimMod][SpawnB] pv.Load() returned but vessel not in FlightGlobals.");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[TimMod][SpawnB] pv.Load() threw: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            }

            Debug.Log($"[TimMod][SpawnB] === SpawnFromSaveTemplate END ===");
        }
    }
}
