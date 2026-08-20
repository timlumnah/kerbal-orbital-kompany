using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using KSP;

// Fix: namespace typo. Was SyndicateMangaer. Unified into main namespace.
namespace Koko
{
    [Serializable]
    public class SyndicateLocation
    {
        public string name;
        public string body;
        public double lat;
        public double lon;
        public double radius;           // legacy -- fallback when ring fields are 0

        // Detection rings. Three concentric zones per location.
        // JSON: set explicitly or leave 0 to use legacy radius fallback.
        public double dangerRadius;     // sabotage trigger zone    (0 = use radius)
        public double detectionRadius;  // location shows on map   (0 = use radius)
        public double revealRadius;     // full info revealed     (0 = physics range 2500m)

        public float explodeChance;
        public float cooldownSeconds;
        public bool destroyed;
        public bool discovered;         // player entered detectionRadius at least once
        public bool revealed;           // player entered revealRadius at least once

        // Respawn timer. 0 = no timer running. >0 = Planetarium UT when location respawns.
        // Timer only starts if the syndicate still has active locations when this
        // one is destroyed. If it was the last active location, no timer is set
        // and the syndicate is dead permanently.
        public double respawnTimerUT;

        // Orbital discovery. Player puts a controllable vessel in orbit around
        // the body; a fuzzy 250km yellow ring appears at an offset center.
        // approxLat/approxLon: randomized center, 50-150km from real location.
        // Reset on respawn so re-orbiting shows a new (possibly different) ring.
        public bool   approxDiscovered;
        public double approxLat;
        public double approxLon;

        // Craft spawn. True once the player enters physics range (2500m) and
        // the spawn stub fires. Reset on respawn so the stub can fire again if
        // the player approaches after the location rebuilds.
        public bool craftSpawned;

        // Effective radii: explicit value or legacy fallback
        // Detection/reveal fallbacks use design-intent defaults rather than radius.
        // Diagnostic showed detectionRadius loading as 0 from save; using radius
        // (500m) as fallback collapsed both rings to the same tiny zone.
        public double EffectiveDangerRadius     => dangerRadius     > 0 ? dangerRadius     : radius;
        public double EffectiveDetectionRadius  => detectionRadius  > 0 ? detectionRadius  : 20000.0;
        public double EffectiveRevealRadius     => revealRadius     > 0 ? revealRadius     : 2500.0;
    }

    [Serializable]
    public class Syndicate
    {
        public string name;
        public List<SyndicateLocation> locations = new List<SyndicateLocation>();
    }

    [Serializable]
    public class SyndicateDatabase
    {
        public List<Syndicate> syndicates = new List<Syndicate>();
    }

    public static class SyndicateManager
    {
        public static List<Syndicate> AllSyndicates = new List<Syndicate>();

        private static bool loaded = false;

        // Reset: called by SyndicateBootstrap.OnDestroy when leaving game scenes.
        // Ensures next game load starts clean.
        public static void Reset()
        {
            AllSyndicates.Clear();
            loaded = false;
        }

        public static void Load()
        {
            if (loaded) return;

            // Replaced JsonUtility / Syndicates.json with GameDatabase ConfigNode.
            // JsonUtility was silently returning 0 syndicates despite correct JSON
            // (known fragility with nested List<T> in some Unity 2019 builds).
            // GameDatabase scans all .cfg files in GameData -- no path required,
            // no file I/O, consistent with the rest of the mod.
            // Data file: GameData/KoKo/Syndicates.cfg
            try
            {
                if (GameDatabase.Instance == null)
                {
                    Debug.LogWarning("[TimMod] SyndicateManager: GameDatabase not ready.");
                    return;
                }

                var roots = GameDatabase.Instance.GetConfigNodes("SYNDICATES_DATABASE");
                if (roots == null || roots.Length == 0)
                {
                    Debug.LogWarning("[TimMod] SyndicateManager: SYNDICATES_DATABASE node not found in GameDatabase.");
                    return;
                }

                var root = roots[0];

                foreach (ConfigNode sNode in root.GetNodes("SYNDICATE"))
                {
                    var syn = new Syndicate();
                    syn.name = sNode.GetValue("name");

                    foreach (ConfigNode lNode in sNode.GetNodes("LOCATION"))
                    {
                        var loc = new SyndicateLocation();
                        loc.name            = lNode.GetValue("name");
                        loc.body            = lNode.GetValue("body");
                        loc.lat             = double.Parse(lNode.GetValue("lat"));
                        loc.lon             = double.Parse(lNode.GetValue("lon"));
                        loc.radius          = double.Parse(lNode.GetValue("radius"));
                        loc.explodeChance   = float.Parse(lNode.GetValue("explodeChance"));
                        loc.cooldownSeconds = float.Parse(lNode.GetValue("cooldownSeconds"));

                        var s = lNode.GetValue("dangerRadius");
                        loc.dangerRadius  = s != null ? double.Parse(s) : 0.0;
                        s = lNode.GetValue("detectionRadius");
                        loc.detectionRadius  = s != null ? double.Parse(s) : 0.0;
                        s = lNode.GetValue("revealRadius");
                        loc.revealRadius = s != null ? double.Parse(s) : 0.0;

                        // Randomized location: generate lat/lon within config-specified range.
                        // Uses a seed derived from current time so every career is unique.
                        // Saved lat/lon on first autosave; future loads use LoadFromSave() instead.
                        s = lNode.GetValue("randomized");
                        if (s != null && bool.Parse(s))
                        {
                            double latMin = -45.0, latMax = 45.0;
                            double lonMin = -180.0, lonMax = 180.0;
                            var ls = lNode.GetValue("latMin"); if (ls != null) latMin = double.Parse(ls);
                            ls = lNode.GetValue("latMax"); if (ls != null) latMax = double.Parse(ls);
                            ls = lNode.GetValue("lonMin"); if (ls != null) lonMin = double.Parse(ls);
                            ls = lNode.GetValue("lonMax"); if (ls != null) lonMax = double.Parse(ls);

                            // Seed: time ticks + location name hash = unique per career per location
                            int seed = (int)(System.DateTime.UtcNow.Ticks & 0xFFFFFFFF)
                                     ^ (loc.name + loc.body).GetHashCode();
                            var rng = new System.Random(seed);
                            loc.lat = latMin + rng.NextDouble() * (latMax - latMin);
                            loc.lon = lonMin + rng.NextDouble() * (lonMax - lonMin);
                            Debug.Log($"[TimMod] Randomized {loc.name} on {loc.body}: " +
                                      $"lat={loc.lat:F2} lon={loc.lon:F2}");
                        }

                        syn.locations.Add(loc);
                    }

                    // LOCATION_GROUP: generates a random count of randomized locations
                    // per body. Count baked per career via seeded RNG.
                    foreach (ConfigNode lgNode in sNode.GetNodes("LOCATION_GROUP"))
                    {
                        string lgBody    = lgNode.GetValue("body") ?? "";
                        string lgPrefix  = lgNode.GetValue("namePrefix") ?? (lgBody + " Outpost");
                        int    lgMin = 0, lgMax = 1;
                        double lgLatMin = -70, lgLatMax = 70, lgLonMin = -180, lgLonMax = 180;
                        double lgRadius = 10000, lgDangerR = 4000, lgDetectR = 40000, lgRevealR = 15000;
                        float  lgExplode = 0.55f, lgCooldown = 15f;

                        var ls2 = lgNode.GetValue("minCount");        if (ls2 != null) lgMin     = int.Parse(ls2);
                        ls2 = lgNode.GetValue("maxCount");             if (ls2 != null) lgMax     = int.Parse(ls2);
                        ls2 = lgNode.GetValue("latMin");               if (ls2 != null) lgLatMin  = double.Parse(ls2);
                        ls2 = lgNode.GetValue("latMax");               if (ls2 != null) lgLatMax  = double.Parse(ls2);
                        ls2 = lgNode.GetValue("lonMin");               if (ls2 != null) lgLonMin  = double.Parse(ls2);
                        ls2 = lgNode.GetValue("lonMax");               if (ls2 != null) lgLonMax  = double.Parse(ls2);
                        ls2 = lgNode.GetValue("radius");               if (ls2 != null) lgRadius  = double.Parse(ls2);
                        ls2 = lgNode.GetValue("dangerRadius");         if (ls2 != null) lgDangerR = double.Parse(ls2);
                        ls2 = lgNode.GetValue("detectionRadius");      if (ls2 != null) lgDetectR = double.Parse(ls2);
                        ls2 = lgNode.GetValue("revealRadius");         if (ls2 != null) lgRevealR = double.Parse(ls2);
                        ls2 = lgNode.GetValue("explodeChance");        if (ls2 != null) lgExplode = float.Parse(ls2);
                        ls2 = lgNode.GetValue("cooldownSeconds");      if (ls2 != null) lgCooldown = float.Parse(ls2);

                        int seed2 = (int)(System.DateTime.UtcNow.Ticks & 0xFFFFFFFF)
                                  ^ (lgBody + lgPrefix).GetHashCode();
                        var rng2 = new System.Random(seed2);
                        int count = lgMin + rng2.Next(lgMax - lgMin + 1);

                        for (int i = 0; i < count; i++)
                        {
                            var loc = new SyndicateLocation();
                            loc.name            = lgPrefix + " " + (char)('A' + i);
                            loc.body            = lgBody;
                            loc.lat             = lgLatMin + rng2.NextDouble() * (lgLatMax - lgLatMin);
                            loc.lon             = lgLonMin + rng2.NextDouble() * (lgLonMax - lgLonMin);
                            loc.radius          = lgRadius;
                            loc.dangerRadius    = lgDangerR;
                            loc.detectionRadius = lgDetectR;
                            loc.revealRadius    = lgRevealR;
                            loc.explodeChance   = lgExplode;
                            loc.cooldownSeconds = lgCooldown;
                            syn.locations.Add(loc);
                            Debug.Log($"[TimMod] Generated {loc.name} on {loc.body}: lat={loc.lat:F2} lon={loc.lon:F2}");
                        }
                        if (count > 0)
                            Debug.Log($"[TimMod] LOCATION_GROUP: {count} location(s) generated for {lgBody}");
                    }

                    AllSyndicates.Add(syn);
                }

                loaded = true;
                Debug.Log($"[TimMod] SyndicateManager: loaded {AllSyndicates.Count} syndicate(s) from GameDatabase.");
            }
            catch (Exception ex)
            {
                Debug.LogError("[TimMod] SyndicateManager: failed to load from GameDatabase: " + ex);
            }
        }

        public static IEnumerable<SyndicateLocation> GetLocationsForBody(string body)
        {
            return AllSyndicates
                .SelectMany(s => s.locations)
                .Where(l =>
                    string.Equals(
                        l.body,
                        body,
                        StringComparison.OrdinalIgnoreCase));
        }

        // Fix: only consider undestroyed locations.
        public static SyndicateLocation FindNearestLocation(
            Vector2d coords,
            string body)
        {
            return GetLocationsForBody(body)
                .Where(l => !l.destroyed)
                .OrderBy(l =>
                    Vector2d.Distance(
                        coords,
                        new Vector2d(l.lat, l.lon)))
                .FirstOrDefault();
        }

        public static void LaunchAttack(Vector2d coords, string body)
        {
            Debug.Log(
                $"[TimMod] Syndicate attack launched at " +
                $"{coords.x}, {coords.y} on {body}");

            var nearest = FindNearestLocation(coords, body);

            if (nearest != null)
            {
                nearest.destroyed = true;

                ScreenMessages.PostScreenMessage(
                    $"Syndicate location destroyed: {nearest.name}",
                    5f,
                    ScreenMessageStyle.UPPER_CENTER);

                // Eeloo Fortress cascade: destroying the Fortress collapses the entire
                // Syndicate. All other locations immediately become inactive so the
                // win condition fires without requiring the player to clear every body.
                if (string.Equals(nearest.name, "Eeloo Fortress",
                        StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var syn in AllSyndicates)
                        foreach (var loc in syn.locations)
                            loc.destroyed = true;
                    Debug.Log("[TimMod] Eeloo Fortress destroyed -- Syndicate collapsed. All locations inactive.");
                    ScreenMessages.PostScreenMessage(
                        "EELOO FORTRESS DESTROYED. The Syndicate has collapsed.",
                        8f, ScreenMessageStyle.UPPER_CENTER);
                }
            }
        }

        // Respawn duration. Set by SyndicateZonesManager from SYNDICATE_SETTINGS.
        public static double respawnDurationSeconds = 9_203_545.0; // 1 Kerbin year default

        // Called periodically from SyndicateZonesManager.Update().
        // Drives the respawn cycle: starts timers on newly destroyed locations,
        // fires ready timers, and clears all timers when a syndicate is wiped.
        public static void UpdateRespawn()
        {
            double now = Planetarium.GetUniversalTime();

            foreach (var syn in AllSyndicates)
            {
                int activeCount = syn.locations.Count(l => !l.destroyed);

                if (activeCount == 0)
                {
                    // Last location just destroyed -- cancel any pending timers.
                    // Syndicate is permanently eliminated; nothing will respawn.
                    bool hadTimer = false;
                    foreach (var loc in syn.locations)
                    {
                        if (loc.respawnTimerUT > 0) hadTimer = true;
                        loc.respawnTimerUT = 0;
                    }
                    if (hadTimer)
                        Debug.Log($"[TimMod] Syndicate '{syn.name}' eliminated -- respawn timers cleared.");
                    continue;
                }

                foreach (var loc in syn.locations)
                {
                    if (!loc.destroyed) continue;

                    // Start timer on newly destroyed location
                    if (loc.respawnTimerUT <= 0)
                    {
                        loc.respawnTimerUT = now + respawnDurationSeconds;
                        Debug.Log($"[TimMod] Respawn timer started: {loc.name} " +
                                  $"fires at UT={loc.respawnTimerUT:F0} " +
                                  $"({respawnDurationSeconds:F0}s from now)");
                    }

                    // Fire ready timer
                    if (now >= loc.respawnTimerUT)
                    {
                        // Defender suppression: if the body is cleared + has a defender in
                        // orbit, skip the respawn. Timer stays expired; fires when defender leaves.
                        if (IsBodyDefended(loc.body))
                        {
                            Debug.Log($"[TimMod] Respawn suppressed: {loc.name} ({loc.body} is defended).");
                            continue;
                        }
                        loc.destroyed      = false;
                        loc.revealed       = false;
                        loc.discovered     = false;
                        loc.respawnTimerUT = 0;
                        // Reset approx/craft state so orbital re-scan and craft spawn
                        // can trigger fresh when player revisits after respawn.
                        loc.approxDiscovered = false;
                        loc.approxLat        = 0.0;
                        loc.approxLon        = 0.0;
                        loc.craftSpawned     = false;
                        Debug.Log($"[TimMod] Syndicate location respawned: {loc.name} on {loc.body}");
                        ScreenMessages.PostScreenMessage(
                            $"Intel: Syndicate activity resurging -- {loc.body} sector.",
                            5f, ScreenMessageStyle.UPPER_CENTER);
                    }
                }
            }
        }

        // Body defended: all locations on the body are destroyed AND a controllable
        // vessel is in orbit. While defended, UpdateRespawn suppresses respawns.
        public static bool IsBodyDefended(string bodyName)
        {
            var locs = GetLocationsForBody(bodyName).ToList();
            if (locs.Count == 0 || locs.Any(l => !l.destroyed)) return false;
            if (FlightGlobals.Vessels == null) return false;
            foreach (Vessel v in FlightGlobals.Vessels)
            {
                if (v.situation != Vessel.Situations.ORBITING) continue;
                if (!string.Equals(v.mainBody.name, bodyName,
                        StringComparison.OrdinalIgnoreCase)) continue;
                if (v.IsControllable) return true;
            }
            return false;
        }

        public static void SaveToSave(ConfigNode node)
        {
            ConfigNode syndicateNode = new ConfigNode("SYNDICATES");

            foreach (var syndicate in AllSyndicates)
            {
                ConfigNode sNode = new ConfigNode("SYNDICATE");
                sNode.AddValue("name", syndicate.name);

                foreach (var loc in syndicate.locations)
                {
                    ConfigNode lNode = new ConfigNode("LOCATION");

                    lNode.AddValue("name", loc.name);
                    lNode.AddValue("body", loc.body);
                    lNode.AddValue("lat", loc.lat);
                    lNode.AddValue("lon", loc.lon);
                    lNode.AddValue("radius", loc.radius);
                    lNode.AddValue("dangerRadius",    loc.dangerRadius);
                    lNode.AddValue("detectionRadius", loc.detectionRadius);
                    lNode.AddValue("revealRadius",    loc.revealRadius);
                    Debug.Log($"[TimMod] SaveToSave {loc.name}: danger={loc.dangerRadius} detect={loc.detectionRadius} reveal={loc.revealRadius}");
                    lNode.AddValue("explodeChance", loc.explodeChance);
                    lNode.AddValue("cooldownSeconds", loc.cooldownSeconds);
                    lNode.AddValue("destroyed",   loc.destroyed);
                    lNode.AddValue("discovered", loc.discovered);
                    lNode.AddValue("revealed",   loc.revealed);
                    lNode.AddValue("respawnTimerUT", loc.respawnTimerUT.ToString("R"));
                    lNode.AddValue("approxDiscovered", loc.approxDiscovered);
                    lNode.AddValue("approxLat",        loc.approxLat.ToString("R"));
                    lNode.AddValue("approxLon",        loc.approxLon.ToString("R"));
                    lNode.AddValue("craftSpawned",     loc.craftSpawned);

                    sNode.AddNode(lNode);
                }

                syndicateNode.AddNode(sNode);
            }

            node.AddNode(syndicateNode);
        }

        // Fix: clear and reset loaded before early return. Without this, a fresh
        // save (no SYNDICATES node) leaves stale data in memory from prior session
        // and blocks Load() from re-reading JSON.
        // Fix: if no SYNDICATES node (fresh save), fall back to Load() after
        // clearing. Previously this returned with an empty list and loaded=false,
        // but nothing would trigger Load() again in the same scene, so all
        // syndicate data was lost until the next scene transition.
        public static void LoadFromSave(ConfigNode node)
        {
            AllSyndicates.Clear();
            loaded = false;

            if (!node.HasNode("SYNDICATES"))
            {
                Debug.Log("[TimMod] SyndicateManager: no save data -- falling back to JSON.");
                Load();
                return;
            }

            AllSyndicates.Clear();

            ConfigNode root = node.GetNode("SYNDICATES");

            foreach (ConfigNode sNode in root.GetNodes("SYNDICATE"))
            {
                Syndicate syndicate = new Syndicate();
                syndicate.name = sNode.GetValue("name");

                foreach (ConfigNode lNode in sNode.GetNodes("LOCATION"))
                {
                    SyndicateLocation loc = new SyndicateLocation();

                    loc.name = lNode.GetValue("name");
                    loc.body = lNode.GetValue("body");
                    loc.lat = double.Parse(lNode.GetValue("lat"));
                    loc.lon = double.Parse(lNode.GetValue("lon"));
                    loc.radius = double.Parse(lNode.GetValue("radius"));
                    // New fields: optional in old saves. Null GetValue = absent key.
                    // Backward-compat: fall back to pre-rename keys (outerRadius /
                    // innerRadius) so saves written before the rename still load correctly.
                    var s = lNode.GetValue("dangerRadius") ?? lNode.GetValue("outerRadius");
                    loc.dangerRadius    = s != null ? double.Parse(s) : 0.0;
                    s = lNode.GetValue("detectionRadius") ?? lNode.GetValue("innerRadius");
                    loc.detectionRadius = s != null ? double.Parse(s) : 0.0;
                    s = lNode.GetValue("revealRadius");
                    loc.revealRadius    = s != null ? double.Parse(s) : 0.0;
                    Debug.Log($"[TimMod] LoadFromSave {loc.name}: raw danger='{lNode.GetValue("dangerRadius")}' detect='{lNode.GetValue("detectionRadius")}' reveal='{lNode.GetValue("revealRadius")}' -> danger={loc.dangerRadius} detect={loc.detectionRadius} reveal={loc.revealRadius}");
                    loc.explodeChance = float.Parse(lNode.GetValue("explodeChance"));
                    loc.cooldownSeconds = float.Parse(lNode.GetValue("cooldownSeconds"));
                    loc.destroyed = bool.Parse(lNode.GetValue("destroyed"));
                    s = lNode.GetValue("discovered");
                    loc.discovered = s != null && bool.Parse(s);
                    s = lNode.GetValue("revealed");
                    loc.revealed   = s != null && bool.Parse(s);
                    s = lNode.GetValue("respawnTimerUT");
                    loc.respawnTimerUT = (s != null && double.TryParse(s, out double rut)) ? rut : 0.0;
                    s = lNode.GetValue("approxDiscovered");
                    loc.approxDiscovered = s != null && bool.TryParse(s, out bool ad) && ad;
                    s = lNode.GetValue("approxLat");
                    loc.approxLat = (s != null && double.TryParse(s, out double alat)) ? alat : 0.0;
                    s = lNode.GetValue("approxLon");
                    loc.approxLon = (s != null && double.TryParse(s, out double alon)) ? alon : 0.0;
                    s = lNode.GetValue("craftSpawned");
                    loc.craftSpawned = s != null && bool.TryParse(s, out bool cs2) && cs2;

                    syndicate.locations.Add(loc);
                }

                AllSyndicates.Add(syndicate);
            }

            loaded = true;

            Debug.Log($"[TimMod] Loaded {AllSyndicates.Count} syndicate(s) from save.");

            // If save had 0 syndicates, it was probably written during a broken
            // earlier load (e.g. file not found, wrong path). Fall back to JSON
            // so the poisoned save doesn't permanently suppress the data.
            if (AllSyndicates.Count == 0)
            {
                Debug.Log("[TimMod] SyndicateManager: save has 0 syndicates -- falling back to JSON.");
                loaded = false;
                Load();
            }
        }
    }
}