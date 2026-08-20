# KoKo — Kerbal Orbital Kompany

> **⚠ Beta / work in progress.** KoKo is playable and the core campaign loop
> works end-to-end, but this is not a finished, polished release. Expect
> rough edges, at least one known-broken feature (see **Known Limitations**),
> active renaming/reorganization, and breaking changes between versions
> without much warning. Back up your saves before installing.

## Overview

A KSP1 career-mode overhaul that turns the stock sandbox into a persistent
geopolitical, logistical, and military simulation.

The player operates inside a living world: establish off-world businesses and
logistics networks, scout and destroy Syndicate installations scattered across
the Kerbol system, and race to eliminate the enemy before they complete a
self-sustaining fortress on Eeloo.

**Current state (2026-08-20):** Compiles cleanly, playtested. Full campaign
arc working end-to-end including Eeloo Fortress victory. All-body syndicate
expansion (every planet/moon), orbital discovery rings, telescope reveal part,
body defender mechanic, missile HUD, weapon system (three on-rails warhead
tiers + kamikaze), force field defense module, and business type selection
are all in-game and working. Syndicate craft spawning is implemented but not
yet loading correctly in-scene — see **Known Limitations**.

This is also an experiment in what a story-driven, systems-first career mode
can look like in KSP. If any of it — the Syndicate arc, the business/logistics
economy, the on-rails weapon abstraction — is useful reference material for
other career-mode projects (including [Kitten Space Agency](https://kittenspaceagency.wiki.gg/)),
it's shared under a permissive license specifically so it can be borrowed from.

---

# How KoKo Compares to Other Mods

KoKo isn't trying to replace the mods below — most of them solve a different
problem, and several are worth running alongside it.

- **[BDArmory](https://github.com/PapaJoesSoup/BDArmory)** — BDArmory is a
  full weapons/combat simulation with real projectile physics and detailed
  models. KoKo's weapon system is deliberately the opposite: a lightweight
  on-rails abstraction (travel time = distance / speed, no physics sim) that
  despawns the target on a hit. It exists so a strike has *narrative and
  economic* weight — did you find and destroy an installation — without the
  overhead of a full combat sim. No BDArmory integration exists yet; it's on
  the roadmap (see below), and until then the two mods just coexist fine
  side by side.

- **Contract packs** — KoKo replaces contracts almost entirely
  (`ContractSuppressor` disables stock contracts on save start) in favor of a
  single ongoing story arc: find and destroy the Syndicate before the Eeloo
  Fortress completes. If you find contract packs tedious busywork, this is
  the alternative.

- **Parts mods (Future Tech, OPT, etc.)** — KoKo is not a parts mod and isn't
  meant to compete with one. Its "custom" parts are almost all `+PART[]`
  ModuleManager clones of stock parts with tuned stats — lightweight and
  arcade-feeling (e.g. near-infinite ISP on RCS thrusters) rather than a new
  art/model pipeline. Full part mods are complementary, not redundant.

- **[Realism Overhaul](https://github.com/KSP-RO/RealismOverhaul)** — the
  opposite design goal. KoKo intentionally loosens physics/economy realism
  (free tech unlocks, boosted science transmit, instant proximity refueling)
  in favor of pace and story momentum. Running both together will fight each
  other.

- **[TAC Life Support](https://github.com/KSP-RO/TacLifeSupport)** —
  intentionally not implemented in KoKo; the mod is about logistics, business,
  and covert ops, not survival management. The two are complementary — KoKo
  doesn't touch life support at all, so TACLS can be layered on top.

- **Kerbal Konstructs** — KoKo's business-outpost system does some of the
  same job (a qualifying vessel becomes a persistent, useful base you can
  launch to directly via the Operations UI's Launch-To tab) without needing
  custom static models — but it's also less immersive; there's no persistent
  visual structure, just game-state tracking on a vessel. If you want visual
  bases, KK is still the better tool and the two aren't mutually exclusive.

- **[USI / MKS](https://github.com/UmbraSpaceIndustries/MKS)** — USI's
  colonization system is much deeper and more detail-oriented than KoKo's:
  dedicated Kerbal specialties, life-support-driven habitation limits,
  inflatable structures, and real production chains. KoKo's business system
  is a lighter, higher-level abstraction (a qualification checklist and a
  flat income formula) aimed at strategic pacing rather than colony
  simulation. *(Caveat: I haven't played USI myself — this comparison is from
  reading its docs, not hands-on use, so take it as a starting point rather
  than gospel.)*

- **MechJeb** — recommended, not competing. All MechJeb modules are unlocked
  from the start (`Configs/mechjeb.cfg`) specifically because KoKo assumes
  you're flying a lot of routine logistics/scouting runs and autopilot cuts
  the tedium.

**What's actually distinctive here** is the story arc — KoKo is a career mode
built around one antagonist and one deadline, not a checklist of contracts.
Most of the individual systems above (logistics, combat, economy) exist in
other mods in more detailed forms; what ties them together in KoKo is that
they all serve the same narrative throughline, and the syndicate-generation
system is modular enough in the C# source that the endgame could branch into
multiple outcomes — a non-linear story engine — without a rewrite.

---

# How to Build

**Requirements (devbox):**
- Ubuntu (tested on 24.04)
- `dotnet-sdk-8.0`
- KSP reference DLLs in `Refs/` (copy from `KSP_Data/Managed/` on your own KSP
  install — **not included in this repo**, since they're Squad/Take-Two's
  compiled game assemblies, not code this project owns):
  - `Assembly-CSharp.dll`
  - `UnityEngine.dll`
  - `UnityEngine.CoreModule.dll`
  - `UnityEngine.IMGUIModule.dll`
  - `UnityEngine.JSONSerializeModule.dll`
  - `UnityEngine.InputLegacyModule.dll`
  - `UnityEngine.AudioModule.dll`

**Build command:**
```bash
cd kerbal-orbital-kompany
dotnet msbuild KoKo.csproj /p:Configuration=Release
```

Output: `bin/Release/KoKo.dll`

---

# How to Deploy

Copy the built DLL and these files to your KSP install:

```
GameData/KoKo/Plugins/KoKo.dll              (build it yourself, or grab it from a Release)
GameData/KoKo/Configs/BusinessSettings.cfg
GameData/KoKo/Configs/mechjeb.cfg
GameData/KoKo/Configs/ModuleUpgrades.cfg
GameData/KoKo/Configs/ProximityRefueler.cfg
GameData/KoKo/Configs/ScienceLab.cfg
GameData/KoKo/Configs/ScienceOverrides.cfg
GameData/KoKo/Configs/Syndicates.cfg
GameData/KoKo/Configs/SyndicateSettings.cfg
GameData/KoKo/Configs/TE_parts_tags.cfg
GameData/KoKo/Configs/TechTreeOverrides.cfg
GameData/KoKo/Crafts/*
GameData/KoKo/Parts/**/*.cfg
```

Requires **ModuleManager** (installed via CKAN).

> **Note:** All "custom" parts under `Parts/` are ModuleManager clones of
> stock parts (`+PART[stock_name]`) — they reuse the stock model/texture at
> runtime, so there's no separate art asset pipeline to deploy.

Simplest option once a tagged release exists: install via CKAN (see below)
rather than copying files by hand.

---

# Installing via CKAN

A `.netkan` submission is in progress for the CKAN index. Once merged, KoKo
will show up as a normal CKAN entry (identifier `kerbal-orbital-kompany`).
Until then, use the manual deploy steps above, or grab a release zip directly
from the [GitHub Releases page](https://github.com/timlumnah/kerbal-orbital-kompany/releases).

---

# How to Use

## Campaign Story

Start a **new career save**. On the first visit to the Space Center, the
opening briefing plays automatically:

> *"Whatever is producing these signals isn't exploring. It's building."*

Gene Kerman briefs the player on The Syndicate — a machine economy that has
been quietly constructing installations across the Kerbol system for years.
Their Eeloo operation is nearing completion. The player must locate and destroy
all installations before the Eeloo Fortress becomes self-sustaining.

**Win condition:** Eeloo Fortress destroyed. Destroying the Fortress immediately collapses the entire Syndicate — all other locations across all bodies become inactive. The player does not need to clear every body first.
**Lose condition:** Eeloo Fortress construction reaches 100%.

### Campaign Beats

| Beat | Trigger |
|---|---|
| Opening briefing | First SC/TS visit on new save |
| Mid-game twist | ~40% of locations destroyed |
| Penultimate reveal | Player reveals the Eeloo Fortress location |
| Victory | All locations destroyed |
| Defeat | Fortress progress reaches 100% |

Dialogue stops time warp automatically when triggered. All beats fire in flight
and at the Space Center.

### Eeloo Fortress Progress

A **fortress progress bar** is always visible in the top-right corner of the
flight HUD (below the stealth bar). The **map view** shows a ring on Eeloo that
shifts from gray to orange to red as construction advances.

Fortress timer is driven by in-game time (`Planetarium.GetUniversalTime()`) and
advances with time warp. Default duration: 2 Kerbin years.

Config (`SyndicateSettings.cfg`): `fortressDurationSeconds`

## Career Override (fires once per save)

On first load, the mod:
- Maxes all facility levels (VAB, launchpad, R&D, Mission Control, etc.)
- Cancels all stock contracts

Fires once per save (flag stored in save file).

> **Tech tree:** Key nodes for MechJeb and advanced probes (`unmannedTech`,
> `flightControl`, `advFlightControl`) are patched to cost 0 and are
> immediately researchable.

### A note on the config overrides

Free tech unlocks, 100%-value science transmit, near-infinite RCS ISP, and
similar arcade-tuned overrides are **an intentional, permanent design
choice** — not a placeholder or a bug. KoKo is built around momentum: the
player should be spending time on the Syndicate story and the logistics
economy, not on early-game tech-tree grinding. If you want the harder stock
experience underneath, the relevant patches are isolated in
`TechTreeOverrides.cfg`, `ScienceOverrides.cfg`, and `ScienceLab.cfg` and can
be deleted or disabled individually.

## Starting Parts

The following stock parts are unlocked at the start node with no entry cost:

**Science:** TE-ScienceHex probe, Mobile Processing Lab, all stock instruments

**Fuel Tanks:** Rockomax X200-16/32, TE-Rockomax8 (custom clone with MonoProp + EC)

**Resources / ISRU:** Drill-O-Matic Junior, Convert-O-Tron 125, Radial Holding Tank

**Other:** Standard struts, solar panels, RTG, batteries, fairings, RCS, antennas,
parachutes, airbrakes, cargo containers, EVA kits, custom drone cores, custom comms.
See `Parts/starting_parts.cfg` for full list.

> **Finding custom parts:** All custom parts have `manufacturer = KoKo`.
> Filter by manufacturer or search `koko` in the VAB/SPH.

## HECS-S Science Probe (TE-ScienceHex)

Modified HECS probe core pre-fitted with a full instrument suite.
Experiments: Temperature, Barometer, Seismic, Gravity, Atmosphere Analysis,
Mystery Goo, Materials Study — all rerunnable, no scientist required.
EC capacity boosted to 500. Found under **Pods**, start node.

## Mobile Processing Lab

Patched to include all 7 science experiments above. No bolt-on instruments needed.
Lab processing (requiring scientists) is unchanged.

## Science Transmit Values

All stock experiments transmit at full value (1.0). Penalties patched out.
Patch: `Configs/ScienceOverrides.cfg`

## Proximity Refueler

Instant fuel transfer to any vessel within range. No docking required.

**Setup:**
1. Build a refueling station with a drone core or pod.
2. Right-click in flight → **"Enable Refueling Mode"**.
3. Any craft within 100m (non-source) is refueled instantly.

**Config** (`Configs/ProximityRefueler.cfg`): `transferRange`, `infiniteFuel`, `resourceNames`

## Business Income System

Qualifying outposts generate passive income and science every 2 game-minutes
(including during time warp). Any body is eligible — Kerbin included, with a
penalty multiplier making it less profitable than off-world operations.

**Qualifying conditions:**

| Condition | Detail |
|---|---|
| Situation | LANDED, ORBITING, or SPLASHED |
| Crew | >= 2 Kerbals present |
| Mobile Processing Lab | `ModuleScienceLab` required |
| Drill | `ModuleResourceHarvester` required |
| Convert-O-Tron | `ModuleResourceConverter` required |
| Ore Tank | Ore resource capacity > 0 |
| CommNet | Connected to relay network |
| ElectricCharge | >= `minEC` units |

**Payout formula:**

```
effective_rate = fundsPerHour × typeMultiplier × (1 + bodyMultiplier)
```

Both funds and science use the same body multiplier. Income is floored at 0.

**Per-body multipliers** (`Configs/BusinessSettings.cfg`):

Each body has separate `orbital` and `landed` values. `landed` falls back to
`orbital` if omitted. Bodies not listed use `unknownBodyMultiplier` (default 0.0).

| Body | Orbital | Landed |
|---|---|---|
| Kerbin | -0.25 (75% of base) | -0.40 (60% of base) |
| Mun | +0% | +2% |
| Minmus | +1% | +3% |
| Duna | +4% | +7% |
| Ike | +5% | +8% |
| Laythe | +8% | +16% |
| Tylo | +8% | +18% |
| Eeloo | +20% | +30% |
| *(others in cfg)* | | |

**Business types** (`ModuleBusinessType`, right-click a Mobile Processing Lab
to set): `Standard` (flat income), `Security` (reduces sabotage chance for
same-body outposts), `Relay` (requires CommNet coverage, higher income
multiplier), `Refueling` (requires Ore + ISRU, bonus per docking vessel).

**Config** (`Configs/BusinessSettings.cfg`):
```
BUSINESS_SETTINGS
{
    fundsPerHour    = 5000
    sciencePerHour  = 1.0
    minCrew         = 2
    checkIntervalUT = 120
    minEC           = 10
}
```

## Kerbal Salary System

Kerbals cost a recurring hourly salary instead of a flat hire fee.
- Hire fee refunded (effective hire cost = 0)
- All hired Kerbals instantly set to level 5
- Salary deducted hourly for Kerbals on vessels; idle Kerbals at KSC not charged

**Config** (`Configs/BusinessSettings.cfg`): `fundsPerKerbalPerHour`, `maxXPOnHire`, `zeroCostOnHire`

## Syndicate System

The Syndicate is a single hostile machine economy with installations on
multiple planetary bodies. All locations belong to one entity.
Data file: `Configs/Syndicates.cfg` — edit to add/modify locations, no recompile needed.

### Locations

**Fixed** (same every career):

| Name | Body | Notes |
|---|---|---|
| Kerbin Outpost Alpha | Kerbin | Island Runway area, good first target |
| Mun Outpost Alpha | Mun | Equatorial highlands |
| Eeloo Fortress | Eeloo | Final boss. Destroying it wins the game. |

**LOCATION_GROUP** (count randomized per career, generated at career start):

| Body | Count | Type |
|---|---|---|
| Moho, Eve, Duna, Dres | 1–3 each | Planet |
| Kerbin, Eeloo | 1–2 each | Planet (in addition to fixed location) |
| Mun, Minmus, Gilly, Ike | 0–2 each | Moon |
| Laythe, Vall, Tylo, Bop, Pol | 0–2 each | Moon |

Jool excluded (no surface). Expected total: ~15–37 locations per career.

Check `KSP.log` for `Generated ... on ...` entries to find randomized positions.

### Fortress Timer Setback

Each destroyed non-Eeloo location adds **10% of the base fortress duration** to the effective deadline. Respawning a location removes that 10% again. No cap — aggressive early clearing can buy significant time.

### Orbital Discovery

Put any controllable vessel with comms in orbit around a body → a **yellow 100km zone** appears on the map. The actual location is anywhere inside the zone (center is offset 20–60km from the real coordinates).

**Syndicate Detector telescope** (`TE_SyndicateDetector`, requires `scienceTech`): when orbiting a body, immediately **fully reveals** all syndicate locations on that body (red ring, targetable). Does not scan moons from planetary orbit.

### Body Defender

A body is **defended** when: all its syndicate locations are destroyed AND a controllable vessel is in orbit. While defended, respawn timers are suppressed — locations stay destroyed indefinitely. The Operations UI shows `PROTECTED: Mun, Duna` when bodies are defended.

### Detection Rings

Each location has three concentric zones:

| Field | Default | Effect |
|---|---|---|
| `detectionRadius` | 20,000m | Orange ring on map. Sets `discovered = true`. |
| `revealRadius` | 7,500m | Red ring + name label on map. Sets `revealed = true`. Required before warhead targeting. |
| `dangerRadius` | 4,000m | Active sabotage zone. Vessel parts at risk based on stealth score. |

Both states persist in the save file.

### Syndicate Respawn

Destroyed locations respawn after 1 Kerbin year — unless prevented by one of:

- **Full collapse**: Destroying the Eeloo Fortress immediately destroys all other locations permanently. No respawn.
- **Body defender**: All locations on a body cleared + controllable vessel in orbit → that body's locations never respawn while the defender remains.
- **Simultaneous wipe**: Destroying the last active location before any respawn timer fires → no respawn (existing behavior).

The respawn timer only starts if there are still active locations remaining elsewhere when a location is destroyed.

**Config** (`Configs/SyndicateSettings.cfg`): `respawnDurationSeconds` (default 9,203,545 = 1 Kerbin year)

### In-Flight HUD

When entering syndicate zones:

- **Detection zone (orange):** Gradient vignette, filled triangle icon top-center,
  "SYNDICATE DETECTED". Entry/exit arpeggios (C5→F5→C6 / reverse).
- **Danger zone (red):** Red vignette, hexagon icon, "DANGER ZONE".
  D5+F5+G#5 chord loops with pulsing volume.

**Stealth bar:** Always visible top-right in flight. `STEALTH: XX%`, colored bar.
Updates live — firing engines raises temperature and lowers stealth rating.

**Fortress bar:** Below stealth bar. `EELOO FORTRESS: XX%`. Green/yellow/red.
Only visible after opening briefing is dismissed.

### Stealth System

```
baseDetection = (partCount × partCountWeight)
              + (totalMass_t × massWeight)
              + (maxPartTemp_K / tempDivisor)
stealthScore  = sum(ModuleStealth.stealthValue) × stealthMultiplier
sabotageChance = Clamp01((baseDetection - stealthScore) / maxDetectionScore)
```

Custom drone cores include `ModuleStealth`:
- `TE-Small_Drone_Core`: `stealthValue = 3.0` (suppresses small probes to ~0%)
- `TE-Large_Drone_Core`: `stealthValue = 1.5` (partial; stack more for full)

Add `ModuleStealth { stealthValue = X }` to any part CFG for stealth capability.

**Config** (`Configs/SyndicateSettings.cfg`):
```
partCountWeight   = 0.1
massWeight        = 0.2
tempDivisor       = 2000
stealthMultiplier = 1.0
maxDetectionScore = 10.0
```

### Facility Attacks

KSC facilities are periodically damaged while The Syndicate is active.

**Config** (`Configs/SyndicateSettings.cfg`):
```
facilityDestroyIntervalSeconds = 600    // real seconds between checks
facilityDestroyChance          = 0.15  // probability per check
```

Default: ~one attack per 67 minutes of active playtime.

### Weapon System

Three tiers plus an early-game kamikaze option. All in VAB → **Payload**. Search `warhead`.

| Part | Mode | Speed | Mass | Cost | Use for |
|---|---|---|---|---|---|
| `TE Warhead (Small)` | On-rails | 500 m/s | 0.05t | 500 | Standard strikes |
| `TE Warhead (Heavy)` | On-rails | 800 m/s | 0.20t | 2000 | Eeloo Fortress, hardened targets |
| `TE Warhead (Micro)` | On-rails | 200 m/s | 0.02t | 200 | Pack multiple per warship |
| `TE Warhead (Kamikaze)` | Impact | — | 0.05t | 300 | Early game, no on-rails system needed |

**On-rails workflow (Small / Heavy / Micro):**
1. Scout within `revealRadius` — red ring + name must appear on map.
2. Build strike rocket with warhead as nose.
3. Launch → right-click → **"Set Target (Nearest Revealed)"** → **"Arm Weapon"**.
4. Switch away. ETA shown on screen. Vessel auto-despawns on strike confirmation.
5. Time-warp past ETA. `"STRIKE CONFIRMED"`.

**Kamikaze workflow:**
1. Scout to reveal (red ring visible).
2. Right-click → **"Arm (Kamikaze)"** before departure.
3. Fly or autopilot directly into the target zone (within 15km on impact).
4. Strike fires on crash. No switching required.

ETA = straight-line distance / `estimatedSpeedMS` (tunable per part CFG).

**BDArmory compatibility:** not implemented. Planned as a config-gated hook
so players running both mods can use BDArmory's combat sim for engagements
instead of (or alongside) the on-rails strikes above. See
`syndicate_expansion_plan.md` for the design sketch.

## Force Field Module

Late-game defensive upgrade (`ModuleForceField`). While active on a vessel,
sabotage rolls against it always fail regardless of stealth score — at the
cost of a high EC drain. Attach via `ModuleUpgrades.cfg` (currently patched
onto the TE Large Drone Core).

## Operations UI

Press **Ctrl + Alt + L** in any scene to toggle the Operations window.

**Operations tab:** Shows all vessels (any body, landed/orbiting/splashed, not debris).
Each vessel row is **green** if qualifying for business income, **red** if not.
Click any row to expand a full qualification checklist and income breakdown:

```
✓  Crew          3 present, 2 needed
✗  Science Lab   ModuleScienceLab required
✓  Drill
✓  ISRU
✓  Ore Storage
✓  CommNet
✓  Power         45 EC (min 10)

  Base:  5,000 ₦/hr   1.00 sci/hr
  Relay ×1.50  ·  Duna (landed) +7%
  → 8,025 ₦/hr   1.07 sci/hr
```

**Switch To Vessel** button jumps directly to that craft from any scene.

**Launch To tab** (VAB/SPH only): lists all qualifying business bases as launch
destinations. Select one before launching — craft will spawn near that base.
Only bases that meet all business income requirements appear in this list.

Routes / Create Route / History tabs are commented out (code preserved).

## MechJeb

All MechJeb modules are unlocked from the start.
If modules appear locked, check that `Configs/mechjeb.cfg` is deployed.

---

# Configuration Reference

## SyndicateSettings.cfg

```
SYNDICATE_SETTINGS
{
    // Stealth / detection weights
    partCountWeight   = 0.1
    massWeight        = 0.2
    tempDivisor       = 2000
    stealthMultiplier = 1.0
    maxDetectionScore = 10.0

    // Facility attacks (real wall-clock time, unaffected by warp)
    facilityDestroyIntervalSeconds = 600
    facilityDestroyChance          = 0.15

    // Campaign
    fortressDurationSeconds = 55221270  // 6 Kerbin years (playtested default)
    midTwistThreshold       = 0.40      // fraction of locations destroyed to fire mid-twist

    // Respawn
    respawnDurationSeconds  = 9203545   // 1 Kerbin year; set to 600 for testing

    // Business types (income multipliers)
    securityMultiplier          = 0.8
    relayMultiplier             = 1.5
    refuelingMultiplier         = 1.3
    securitySabotageMultiplier  = 0.5   // fraction of sabotage chance remaining with Security active
}
```

## Syndicates.cfg

Edit to add/remove syndicate locations. No recompile needed.
Supports fixed and randomized locations.

```
SYNDICATES_DATABASE
{
    SYNDICATE
    {
        name = The Syndicate

        // Fixed location
        LOCATION
        {
            name             = My Outpost
            body             = Kerbin
            lat              = -1.515
            lon              = -71.967
            radius           = 10000        // legacy fallback
            dangerRadius     = 4000         // sabotage zone
            detectionRadius  = 20000        // map discovery zone
            revealRadius     = 7500         // full reveal + targetable
            explodeChance    = 0.60
            cooldownSeconds  = 15
        }

        // Randomized location -- lat/lon generated fresh each career
        LOCATION
        {
            name             = Random Outpost
            body             = Mun
            lat              = 0            // ignored when randomized = true
            lon              = 0
            randomized       = true
            latMin           = -70
            latMax           = 70
            lonMin           = -180
            lonMax           = 180
            radius           = 10000
            dangerRadius     = 4000
            detectionRadius  = 20000
            revealRadius     = 7500
            explodeChance    = 0.55
            cooldownSeconds  = 15
        }
    }
}
```

## BusinessSettings.cfg

```
BUSINESS_SETTINGS
{
    fundsPerHour          = 5000
    sciencePerHour        = 1.0
    minCrew               = 2
    checkIntervalUT       = 120
    minEC                 = 10
    fundsPerKerbalPerHour = 100
    maxXPOnHire           = true
    zeroCostOnHire        = true

    // Business type multipliers
    securityMultiplier   = 0.8
    relayMultiplier      = 1.5
    refuelingMultiplier  = 1.3
    securitySabotageMultiplier = 0.5

    // Per-body multipliers. Formula: fundsPerHour * (1 + multiplier)
    // -0.25 = 25% reduction (75% of base). +0.04 = 4% bonus.
    unknownBodyMultiplier = 0.0
    BODY_MULTIPLIER { body = Kerbin; orbital = -0.25; landed = -0.40 }
    // ... one entry per body, multi-line format required (see cfg file)
}
```

---

# Source Layout

```
src/
  CampaignScenario.cs         ScenarioModule: campaign phase, fortress timer, dialogue triggers
  CampaignDialogue.cs         KSPAddon: full-screen slide dialogue renderer + all story text
  ModuleKamikazeWeapon.cs     PartModule: impact-triggered strike (early-game, no WeaponManager)
  ModuleForceField.cs         PartModule: blocks sabotage while active, high EC drain
  ModuleBusinessType.cs       PartModule: business type selector + right-click status display
  BusinessManager.cs          Passive income + science from off-world outposts
  SalaryManager.cs            Kerbal hire overrides + recurring payroll
  CareerBootstrap.cs          One-time-per-save career unlock
  FacilityBootstrap.cs        Max all KSC facilities on every KSC scene load
  ModuleProximityRefueler.cs  Proximity-based instant fuel transfer
  ModuleStealth.cs            PartModule: stealthValue for detection calc
  ModuleWeapon.cs             PartModule: target coords + arm state for warhead
  WeaponManager.cs            ScenarioModule: tracks in-flight weapons, fires strikes
  ModuleSyndicateCraft.cs     PartModule: syndicate craft sabotage button + chain explosion
  ModuleSyndicateTelescope.cs PartModule: full-reveal on orbit (TE_SyndicateDetector)
  LaunchDestinationManager.cs Launch-To tab: qualifying-base launch site selection
  SyndicateManager.cs         Data model, load/save, attack logic, respawn, randomization
  SyndicateScenario.cs        ScenarioModule: bridges save system to SyndicateManager
  SyndicateBootstrap.cs       KSPAddon: loads syndicates on scene start, resets on exit
  ProximityTrigger.cs         KSPAddon (Flight): proximity checks, zone triggers, respawn ticks
  SyndicateMapOverlay.cs      KSPAddon (AllScenes): GL circles on map view + fortress ring
  SyndicateHUD.cs             KSPAddon (Flight): vignette, icons, audio, stealth + fortress bars
  FacilityDestroyer.cs        Periodic KSC facility damage
  RandomPartExplosion.cs      Detection score formula + sabotage execution
  LogisticsMain.cs            Logistics + Operations UI (Ctrl+Alt+L)
  LogisticsScenario.cs        ScenarioModule: route + business persistence
  ... (other logistics classes)

KoKo/                          GameData deploy folder
  Configs/
    BusinessSettings.cfg      Business income + salary tuning
    mechjeb.cfg                MM patch: MechJeb modules unlocked at start
    ProximityRefueler.cfg      MM patch: refueler on all command parts
    ScienceLab.cfg              MM patch: all experiments on MPL
    ScienceOverrides.cfg        MM patch: transmit value = 1.0 for all experiments
    Syndicates.cfg               Syndicate location data
    SyndicateSettings.cfg        Stealth weights, facility rate, campaign + respawn timers
    TE_parts_tags.cfg            MM patch: manufacturer + koko tag on all TE parts
    TechTreeOverrides.cfg        Free/unlockable nodes for early-game access
    ModuleUpgrades.cfg           MM patch: ModuleForceField added to TE Large Drone Core
  Crafts/                      Syndicate craft/outpost templates
  Parts/
    warheads.cfg                TE_Warhead_Small/Heavy/Micro (on-rails) + Kamikaze (impact)
    starting_parts.cfg           Stock parts unlocked at start node
    drone_core/                  Custom drone cores with ModuleStealth
    science/, resources/, comms/, engines/, fuel_tank/, rcs/
  Plugins/                     Deploy target for the built DLL (not committed)

Refs/                          KSP/Unity reference DLLs (not committed)
devnotes/                      Session logs, archived variants, personal notes (gitignored)
bin/Release/                   Build output (not committed)
story_outline_v2.md            Narrative arc and all dialogue text (source of truth)
syndicate_expansion_plan.md    Syndicate system design doc + future-system sketches
```

---

# Major Systems

## Campaign (CampaignScenario + CampaignDialogue)

Drives the story arc from opening briefing through victory or defeat.
Tracks campaign phase, one-time dialogue flags, and the Eeloo fortress timer.
Fortress timer advances with in-game time (warp = burning the deadline).
All dialogue triggers stop time warp. Opening fires at SC/TS only; subsequent
beats fire in flight as well for proper warp-interrupt behavior.

Key classes: `CampaignScenario` (ScenarioModule), `CampaignDialogue` (KSPAddon)
Story text: `story_outline_v2.md` / `CampaignDialogue.cs:CampaignSlides`

## Business Income (BusinessManager)

Passive fund + science generation from qualifying off-world outposts.
Scans every 2 game-minutes. Payout scales with actual elapsed game time.

Key class: `BusinessManager` (ScenarioModule)
Config: `Configs/BusinessSettings.cfg`

## Kerbal Salary (SalaryManager)

Replaces flat hire fees with recurring hourly salary for deployed crew.
Hooks `GameEvents.onKerbalAdded` for hire-time overrides.

Key class: `SalaryManager` (ScenarioModule)
Config: `Configs/BusinessSettings.cfg`

## Proximity Refueler (ModuleProximityRefueler)

Instant range-based fuel transfer. No docking required.

Key class: `ModuleProximityRefueler` (PartModule)

## Logistics Framework

Recurring automated resource deliveries between vessels/bases.
UI toggle: Ctrl+Alt+L.

Key classes: `LogisticsMain`, `DeliveryManager`, `Route`, `TransportVehicle`,
`DeliveryRecord`, `LogisticsScenario`

## Syndicate System

Single hostile machine economy. Locations spread across multiple bodies.
Three concentric detection rings per location. Discovered/revealed states
persisted in save. Locations respawn after 1 Kerbin year unless the full
syndicate is wiped simultaneously. New careers get randomized locations.

Map overlay (GL circles), in-flight HUD (vignette + icons + synthesized audio),
vessel sabotage modulated by stealth score, periodic KSC facility attacks.

Key classes: `SyndicateManager`, `SyndicateMapOverlay`,
`SyndicateHUD`, `FacilityDestroyer`, `RandomPartExplosionUtil`, `ModuleStealth`

Data: `Configs/Syndicates.cfg`
Config: `Configs/SyndicateSettings.cfg`

## Weapon System

"Launch and forget" strike rocket. Vessel captured when going on-rails.
Travel time = distance / estimatedSpeedMS (abstract, no physics sim).
Vessel automatically despawned on strike confirmation.

Key classes: `WeaponManager` (ScenarioModule), `ModuleWeapon` (PartModule)
Parts: `warheads.cfg`

## Career Override / Facility Bootstrap

One-time-per-save: disables stock contracts, maxes facilities.
FacilityBootstrap runs every KSC load as a safety net.

Key classes: `CareerBootstrap`, `CareerOverride`, `ContractSuppressor`, `FacilityBootstrap`

---

# Technical Notes

- **Game:** Kerbal Space Program 1.12.5
- **Engine:** Unity 2019.4
- **Language:** C# / .NET 4.5
- **Build:** `dotnet msbuild` with `FrameworkPathOverride=/usr/lib/mono/4.5/`
- **Mod hooks:** `KSPAddon`, `ScenarioModule`, `PartModule`
- **ModuleManager** required for CFG patches

**Audio:** All synthesized via `AudioClip.Create()` — no external sound files.
`UnityEngine.AudioModule.dll` required in `Refs/` to build.

**FontStyle:** Not used in any GUIStyle. Requires `TextRenderingModule` (not referenced).

**`string.Split`:** All calls use `Split(new char[]{ ',' })` to avoid .NET 8 Roslyn
resolving to an overload unavailable on KSP's Mono runtime.

**`Contracts.dll`:** Not referenced. `ContractSuppressor` resolves types from
`Assembly-CSharp.dll` at runtime.

---

# Reference Links

- Tech tree node IDs: https://wiki.kerbalspaceprogram.com/wiki/Technology_tree
- Tracking Station levels: https://wiki.kerbalspaceprogram.com/wiki/Tracking_Station

---

# Known Limitations

- **Syndicate craft spawning** — `ModuleSyndicateCraft` (sabotage button,
  chain explosion, `LaunchAttack`) is implemented and its craft template
  exists (`Crafts/`), and the spawn trigger fires correctly at 2.5km
  proximity — but the craft doesn't reliably load into the active physics
  scene without a full scene transition (`flightState.protoVessels.Add()`
  writes to the save but doesn't force a load). The likely next steps are
  `pv.Load()`, `AssembleForLaunchUnlocked`, or pre-placing the vessel at
  career start.

- **BD Armory compatibility** — not implemented, planned as a config-gated
  hook (see the weapon system section above).

- **Diagnostic logging** — some `SaveToSave`/`LoadFromSave` and SyndicateHUD
  diagnostic logging is still active; harmless but noisy in `KSP.log`.

- **CareerBootstrap tech unlock** — the one-time full tech-tree unlock is
  unreliable on its own; `TechTreeOverrides.cfg` CFG patches cover the
  practically important nodes as a more reliable workaround.

- **Autostrut** — the `generalConstruction` tech node stays locked at start
  despite a CFG patch targeting it; appears to sit below ModuleManager's
  reach. Not actively being pursued.

- **FacilityDestroyer** — KSC facility damage may not persist across scene
  reloads in Career mode.

- **HUD icon overlap** — the detection/danger triangle-or-hexagon icon can
  render over the stock altitude display (top-center of the flight HUD).
  Cosmetic only.

- **Defeat sequence** — the Fortress-completion defeat dialogue exists but
  hasn't been confirmed in a full playtest yet.

- **Penultimate story beat** — fires when the Eeloo Fortress is first
  revealed; not yet confirmed to fire correctly if that happens mid-warp.

- **LOCATION_GROUP on existing saves** — new planet/moon location groups only
  generate on a fresh career; an existing save won't retroactively pick up
  newly-added bodies from a config change without starting a new game.

- **Orbital scan runs in the flight scene only** — body-defender status and
  orbital discovery don't update while parked at the Space Center; switch to
  a flight scene to refresh them.

- **Orbital discovery ring clips under terrain** — the yellow 100km discovery
  zone is drawn with `GL` in 3D world space, so on bodies with significant
  terrain relief the ring can partially or fully disappear under the
  surface. A map-screen-space projection would fix this properly.

If a business outpost isn't generating income and the checklist in the
Operations UI looks satisfied, check CommNet signal strength and antenna
coverage before assuming it's a code bug — that's the most common cause.

---

# License

MIT — see [LICENSE](LICENSE). Use it, fork it, borrow whatever's useful for
your own career-mode project.

---

# Philosophy

> "What happens if KSP becomes a persistent geopolitical simulation
> instead of just a rocket sandbox?"

The player is infrastructure operator, logistics manager, intelligence
coordinator, and covert operations strategist. One machine enemy. One
deadline. Everything else is emergent.
