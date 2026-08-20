// Launch To base feature.
// LaunchDestination: static state, survives scene transitions within session.
// LaunchDestinationTeleporter: Flight-scene addon, fires teleport once then clears.

using System.Collections;
using UnityEngine;

namespace Koko
{
    // Shared data class. Built from either a live Vessel (SpaceCenter/Flight)
    // or a ProtoVessel (editor). Avoids coupling LogisticsMain to Vessel type.
    public class LaunchCandidate
    {
        public string             Name;
        public string             BodyName;
        public Vessel.Situations  Situation;
        public double             Lat;
        public double             Lon;
        public bool               IsOrbital;
        // Orbital params — populated only when IsOrbital and a live Vessel is available.
        public double SMA, Ecc, Inc, LAN, ArgPe, MeanAnom, Epoch;
    }

    // Static carrier. Set in VAB/SPH via LogisticsMain; read by Teleporter in Flight.
    public static class LaunchDestination
    {
        public static bool   IsPending        { get; private set; }
        public static string TargetVesselName { get; private set; }
        public static string BodyName         { get; private set; }
        public static double Lat              { get; private set; }
        public static double Lon              { get; private set; }
        // Orbital params stored in case we add orbital insertion later.
        public static bool   IsOrbital        { get; private set; }
        public static double SMA              { get; private set; }
        public static double Ecc              { get; private set; }
        public static double Inc              { get; private set; }
        public static double LAN              { get; private set; }
        public static double ArgPe            { get; private set; }
        public static double MeanAnom         { get; private set; }
        public static double Epoch            { get; private set; }

        // Switched from Set(Vessel) to Set(LaunchCandidate) so both live-vessel
        // and proto-vessel paths can share one setter.
        public static void Set(LaunchCandidate c)
        {
            IsPending        = true;
            TargetVesselName = c.Name;
            BodyName         = c.BodyName;
            Lat              = c.Lat;
            Lon              = c.Lon;
            IsOrbital        = c.IsOrbital;
            SMA              = c.SMA;
            Ecc              = c.Ecc;
            Inc              = c.Inc;
            LAN              = c.LAN;
            ArgPe            = c.ArgPe;
            MeanAnom         = c.MeanAnom;
            Epoch            = c.Epoch;
            Debug.Log($"[TimMod] LaunchDestination: set to '{TargetVesselName}' on {BodyName} (orbital={IsOrbital})");
        }

        public static void Clear()
        {
            IsPending        = false;
            TargetVesselName = null;
            BodyName         = null;
            Debug.Log("[TimMod] LaunchDestination: cleared");
        }
    }

    // Fires once per Flight scene load. Teleports active vessel if destination pending.
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class LaunchDestinationTeleporter : MonoBehaviour
    {
        void Start()
        {
            if (LaunchDestination.IsPending)
                StartCoroutine(TeleportRoutine());
        }

        IEnumerator TeleportRoutine()
        {
            // Wait for KSP to finish spawning and settling the vessel.
            yield return new WaitForSeconds(3f);

            Vessel vessel = FlightGlobals.ActiveVessel;
            if (vessel == null)
            {
                Debug.LogWarning("[TimMod] LaunchDestinationTeleporter: no active vessel");
                LaunchDestination.Clear();
                yield break;
            }

            CelestialBody body = FlightGlobals.Bodies
                .Find(b => b.bodyName == LaunchDestination.BodyName);
            if (body == null)
            {
                Debug.LogWarning($"[TimMod] LaunchDestinationTeleporter: body '{LaunchDestination.BodyName}' not found");
                LaunchDestination.Clear();
                yield break;
            }

            string destName = LaunchDestination.TargetVesselName;
            LaunchDestination.Clear();

            // Surface teleport for all target types.
            // Orbital insertion left for future work — drop to surface of target body instead.
            TeleportToSurface(vessel, body);

            ScreenMessages.PostScreenMessage(
                $"Arrived near {destName} ({body.bodyName})",
                5f,
                ScreenMessageStyle.UPPER_CENTER);
        }

        void TeleportToSurface(Vessel vessel, CelestialBody body)
        {
            // ~0.05 degree lon offset keeps us clear of the target vessel (~3km at equator).
            double lat     = LaunchDestination.Lat;
            double lon     = LaunchDestination.Lon + 0.05;

            // 500m buffer — gives the player time to react before terrain impact.
            double surfAlt = body.TerrainAltitude(lat, lon) + 500.0;
            Vector3d worldPos  = body.GetWorldSurfacePosition(lat, lon, surfAlt);
            Vector3d relPos    = worldPos - body.position;

            // Surface rotation velocity at this point.
            Vector3d surfVel = Vector3d.Cross(body.angularVelocity, relPos);

            // UpdateFromStateVectors first — changes orbit.referenceBody to the target
            // body before SetPosition fires. Without this the physics engine sees a vessel
            // in Kerbin orbit suddenly at Mun surface coords and explodes it.
            vessel.orbit.UpdateFromStateVectors(relPos, surfVel, body, Planetarium.GetUniversalTime());
            vessel.SetPosition(worldPos);
            vessel.SetWorldVelocity(surfVel);

            Debug.Log($"[TimMod] Teleported to {body.bodyName} lat={lat:F3} lon={lon:F3} alt={surfAlt:F0}");
        }
    }
}
