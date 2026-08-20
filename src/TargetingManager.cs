using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
// Removed: using FinePrint / FinePrint.Contracts. All FinePrint code is
// commented out. These usings are compile blockers without FinePrint.dll.
using System;

// Added namespace. Removed redundant using Koko (self-reference).
namespace Koko
{

[KSPAddon(KSPAddon.Startup.Flight, false)]
public class TargetingUI : MonoBehaviour
{
    private bool showUI = false;
    private string latText = "0";
    private string lonText = "0";
    private Rect windowRect = new Rect(10, 10, 250, 260);

    public static Vector2d CurrentTarget = default;
    public static CelestialBody TargetBody = null;


    void Update()
    {
        if (Input.GetKey(KeyCode.LeftAlt) && Input.GetKeyDown(KeyCode.Alpha1))
        {
            showUI = !showUI;
        }
    }

    public static class TargetingUtils
    {
        //public static void CreateSyndicateWaypoint(Vector2d coords, CelestialBody body, string label = "Syndicate Target")
        //{
        //    if (body == null)
        //    {
        //        Debug.LogWarning("[TimMod] Cannot create waypoint: celestial body is null.");
        //        return;
        //    }

        //    Waypoint wp = new Waypoint
        //    {
        //        name = "Syndicate Target",
        //        celestialName = body.bodyName,
        //        latitude = coords.x,
        //        longitude = coords.y,
        //        isOnSurface = true,
        //        seed = UnityEngine.Random.Range(0, int.MaxValue),
        //        id = Guid.NewGuid(),
        //        //caption = "Syndicate Target"
        //    };

        //    WaypointManager.Instance().AddWaypoint(wp);
        //    Debug.Log($"[TimMod] Waypoint created at lat: {coords.x}, lon: {coords.y} on {body.name}");
        //}
    }


    void OnGUI()
    {
        if (!showUI) return;

        windowRect = GUI.Window(24601, windowRect, DrawTargetingWindow, "Syndicate Targeting");
    }

    void DrawTargetingWindow(int windowID)
    {
        GUILayout.Label("Set Target Coordinates:");
        GUILayout.Label("Latitude:");
        latText = GUILayout.TextField(latText);
        GUILayout.Label("Longitude:");
        lonText = GUILayout.TextField(lonText);

        if (GUILayout.Button("Set Target"))
        {
            if (double.TryParse(latText, out double lat) &&
                double.TryParse(lonText, out double lon))
            {
                CurrentTarget = new Vector2d(lat, lon);
                TargetBody = FlightGlobals.currentMainBody; // ✅ Store current body
                Debug.Log($"[TimMod] Target set to: lat={lat:F6}, lon={lon:F6}");
                ScreenMessages.PostScreenMessage($"Target set to: {lat:F6}, {lon:F6}", 4f);
                //TargetingUtils.CreateSyndicateWaypoint(CurrentTarget, FlightGlobals.currentMainBody);

            }
            else
            {
                ScreenMessages.PostScreenMessage("Invalid coordinates.", 3f, ScreenMessageStyle.UPPER_CENTER);
            }
        }

        //if (GUILayout.Button("Snap to Nearest Syndicate"))
        //{
        //    var body = FlightGlobals.currentMainBody?.bodyName ?? "Kerbin";
        //    var closest = SyndicateManager.AllSyndicates
        //        .Where(s => s.body == body && !s.destroyed)
        //        .OrderBy(s => Vector2d.Distance(new Vector2d(s.lat, s.lon), CurrentTarget))
        //        .FirstOrDefault();

        //    if (closest != null)
        //    {
        //        latText = closest.lat.ToString("F6");
        //        lonText = closest.lon.ToString("F6");
        //    }

        //    Debug.Log($"[TimMod] Snapped to: {closest.name} at {closest.lat:F6}, {closest.lon:F6}");

        //}


        if (CurrentTarget != default && GUILayout.Button("Launch Attack"))
        {
            var body = FlightGlobals.currentMainBody?.bodyName ?? "Kerbin";
            SyndicateManager.LaunchAttack(CurrentTarget, body);
            Debug.Log("[TimMod] Attempted syndicate attack.");
            
            //var waypoints = WaypointManager.Instance().Waypoints;
            //foreach (var wp in waypoints)
            //{
            //    if (wp.name == "Syndicate Target" && wp.celestialName == body.name)
            //    {
            //        WaypointManager.Instance().RemoveWaypoint(wp);
            //        Debug.Log($"[TimMod] Removed waypoint at {wp.latitude}, {wp.longitude}");
            //        break;
            //    }
            //}

            showUI = false;
        }

        // Allow dragging the window around
        GUI.DragWindow();
    }





    //void OnGUI()
    //{
    //    if (!showUI) return;

    //    GUILayout.BeginArea(new Rect(10, 10, 250, 200), GUI.skin.box);
    //    GUILayout.Label("Set Target Coordinates:");
    //    GUILayout.Label("Latitude:");
    //    latText = GUILayout.TextField(latText);
    //    GUILayout.Label("Longitude:");
    //    lonText = GUILayout.TextField(lonText);

    //    if (GUILayout.Button("Set Target"))
    //    {
    //        if (double.TryParse(latText, out double lat) &&
    //            double.TryParse(lonText, out double lon))
    //        {
    //            CurrentTarget = new Vector2d(lat, lon);
    //            Debug.Log($"[TimMod] Target set to: {CurrentTarget}");
    //            ScreenMessages.PostScreenMessage($"Target set to: {CurrentTarget}", 4f);
    //        }
    //        else
    //        {
    //            ScreenMessages.PostScreenMessage("Invalid coordinates.", 3f, ScreenMessageStyle.UPPER_CENTER);
    //        }
    //    }

    //    if (CurrentTarget != default && GUILayout.Button("Launch Attack"))
    //    {
    //        var body = FlightGlobals.currentMainBody?.bodyName ?? "Kerbin";
    //        SyndicateManager.LaunchAttack(CurrentTarget, body);
    //        Debug.Log("[TimMod] Attempted syndicate attack.");
    //        showUI = false;
    //    }

    //    GUILayout.EndArea();
    //}

} // class TargetingUI
} // namespace Koko
