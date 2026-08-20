# Syndicate System Expansion Plan
Created: 2026-05-29

## Goal

Make syndicates feel like a real strategic threat that the player actively hunts,
not just a random explosion generator. Full stock-only implementation. BD Armory
support preserved and gated behind a config flag.

---

## Current State (before this expansion)

- Zones: lat/lon + radius + explodeChance per location (from Syndicates.json)
- Trigger: player enters radius -> random part explodes (modulated by stealthValue sum)
- Stealth: ModuleStealth flat float per part -- already stock, no BD dependency
- No map overlay, no syndicate "craft", no player weapons
- FacilityDestroyer: periodically damages KSC buildings (already in place)

---

## Step 1 -- Stealth Score Overhaul  [IN PROGRESS]

Replace flat stealthValue sum with computed detection score.

**Formula:**
```
baseDetection  = (partCount * partCountWeight)
               + (totalMass * massWeight)
               + (maxPartTemp / tempDivisor)

stealthScore   = sum(ModuleStealth.stealthValue) * stealthMultiplier

netDetection   = baseDetection - stealthScore      (clamped >= 0)
sabotageChance = Clamp01(netDetection / maxDetectionScore)
```

**Config (SYNDICATE_SETTINGS in SyndicateSettings.cfg):**
```
partCountWeight   = 0.1     // per part
massWeight        = 0.2     // per tonne
tempDivisor       = 2000    // hottest part kelvin / this
stealthMultiplier = 1.0     // each ModuleStealth unit subtracts this much
maxDetectionScore = 10.0    // score at which chance = 100%
```

**Sample outcomes:**
- Tiny probe (1 part, 0.1t, 300K) + 1 stealth module -> 0% chance (stealthy)
- Medium lander (10 parts, 5t, 300K) + 2 stealth modules -> ~1.5% chance
- Heavy vessel (30 parts, 15t, engines burning 1500K) + no stealth -> ~68% chance

**Files touched:**
- src/RandomPartExplosion.cs  -- ExplodeOne() formula
- src/ProximityTrigger.cs     -- LoadSyndicateSettings() in Start()
- SuperParts_8-12-25/Configs/SyndicateSettings.cfg  (new)
- src/deprecated/RandomPartExplosion_2026-05-29-1332.cs  (backup)

---

## Step 2 -- Detection Rings

Add three concentric radii to SyndicateLocation:

| Field        | Default  | Effect                                              |
|---|---|---|
| dangerRadius  | e.g. 500km | Sabotage trigger zone. Uses new stealth score.    |
| detectionRadius  | e.g. 20km  | Location appears on map (red marker).             |
| revealRadius | e.g. 2.5km | Full info revealed: name, threat level.           |

Backward-compat: if JSON only has `radius`, use for all three.

SyndicateLocation data model: add dangerRadius, detectionRadius, revealRadius fields.
SyndicateZonesManager: update Update() loop to check each ring separately.
Persist revealed-set per save (player must re-explore after new save).

**Files touched:**
- src/SyndicateManager.cs     -- SyndicateLocation fields + SaveToSave/LoadFromSave
- src/ProximityTrigger.cs     -- Update() ring logic
- src/SyndicateScenario.cs    -- persist revealedLocations set
- src/Syndicates.json         -- add dangerRadius/detectionRadius/revealRadius to locations

---

## Step 3 -- Map Overlay

New file: src/SyndicateMapOverlay.cs

KSPAddon active in MapView. On each frame:
- For each known (revealed) syndicate location: draw red circle at lat/lon
- For locations in detectionRadius but not revealRadius: dashed circle (uncertainty ring)
- For fully revealed locations: solid circle + label

Implementation approach:
- Hook MapView.OnEnterMapView / OnExitMapView
- Use PlanetariumCamera to project lat/lon -> screen coords
- Draw circles via GL.LINES in OnPostRender (or GUI.DrawTexture with a ring texture)

Simpler alternative (MVP): draw a 2D GUI circle overlay by computing screen-space
position from CelestialBody.GetWorldSurfacePosition() -> Camera.main.WorldToScreenPoint().

**Files touched:**
- src/SyndicateMapOverlay.cs   (new)
- KSP_Logistics_Mod.csproj     (add compile entry)

---

## Step 4 -- Weapon System ("Launch and Forget")

### Explosive warhead part (CFG)
Clone of a nose cone with crashTolerance = 2. Triggers on impact.
```
name = TE_Warhead_Small
crashTolerance = 2.0
mass = 0.05
TechRequired = start
```

### ModuleWeapon (new PartModule)
Tags the vessel as a weapon. Stores:
- targetLat, targetLon, targetBody (set via right-click UI before launch)
- estimatedSpeed (m/s, config or right-click editable)

### WeaponManager (new ScenarioModule)
Tracks in-flight weapons:
1. In OnUpdate: if a vessel has ModuleWeapon AND exits physics range (distance > 2500m),
   capture it: record (targetCoords, launchTime, travelTime = distance/speed, body)
2. In OnUpdate: for each tracked weapon, check if Planetarium.GetUniversalTime() >= arrivalUT
3. On arrival: SyndicateManager.LaunchAttack(targetCoords, body)
4. Show screen message: "STRIKE CONFIRMED: [location name]"
5. If player is in flight scene near impact, spawn explosion on nearest vessel at coords

**Files touched:**
- src/WeaponManager.cs       (new ScenarioModule)
- src/ModuleWeapon.cs        (new PartModule -- target coords + speed)
- SuperParts_8-12-25/Parts/warheads.cfg  (new -- warhead part defs)
- KSP_Logistics_Mod.csproj  (add compile entries)

---

## Step 5 -- BD Armory Compat

Add to SYNDICATE_SETTINGS:
```
bdArmoryEnabled = false
```

When true: register a GameEvent hook for BD weapon hits. On BD hit event,
check if hit target has a SyndicateLocation nearby -> call LaunchAttack.

When false: stock weapon system (Step 4) runs as primary.

Existing BD playtesting preserved. Flag is a toggle, not a rewrite.

**Files touched:**
- SuperParts_8-12-25/Configs/SyndicateSettings.cfg  (add bdArmoryEnabled)
- src/SyndicateZonesManager or WeaponManager.cs     (BD event hook, conditional)

---

## Notes / Open Questions

- Should syndicates spawn actual vessels? Current plan: NO. Location coordinate
  is the abstraction. Simplifies persistence dramatically. Revisit after Step 4.
- Should syndicates attack player bases (not just passing ships)? FacilityDestroyer
  already does KSC. Could extend to player-owned off-world bases in a later step.
- Physics range (2.5 km stock): revealRadius should probably match this by default
  so the player "finds" the location exactly when it loads into physics range.
- Rover/EVA ground team approach (dismantling by hand): cool idea, defer to post-Step 4.
  Would need: stealth check on approach + "Sabotage" context menu within X meters.
