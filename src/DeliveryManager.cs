using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;


namespace Koko
{
    public class DeliveryManager
    {
        private Dictionary<string, Route> routes = new Dictionary<string, Route>();
        private Dictionary<string, TransportVehicle> vehicles = new Dictionary<string, TransportVehicle>();
        private List<DeliveryRecord> deliveryHistory = new List<DeliveryRecord>();


        private float playerFunds = 100000f;

        public void LoadRoutesFromScenario()
        {
            if (LogisticsScenario.Instance != null)
            {
                foreach (var route in LogisticsScenario.Instance.SavedRoutes)
                {
                    RegisterRoute(route);
                }
            }
        }

        public void RegisterVehicle(TransportVehicle vehicle)
        {
            if (!vehicles.ContainsKey(vehicle.Id))
                vehicles[vehicle.Id] = vehicle;
        }

        public void RegisterRoute(Route route)
        {
            if (!routes.ContainsKey(route.Id))
                routes[route.Id] = route;
        }

        public void CreateRoute(string id, string origin, string destination, string resourceType, double quantity, float frequencyHours)
        {
            var route = new Route(id, origin, destination, resourceType, quantity, frequencyHours);
            RegisterRoute(route);
            UnityEngine.Debug.Log($"[CreateRoute] Route created: {origin} → {destination}, {quantity} {resourceType} every {frequencyHours} hrs");
        }

        public void AssignVehicleToRoute(string vehicleId, string routeId)
        {
            if (vehicles.ContainsKey(vehicleId) && routes.ContainsKey(routeId))
            {
                var vehicle = vehicles[vehicleId];
                vehicle.AssignRoute(routeId);
                UnityEngine.Debug.Log($"[Assign] Vehicle {vehicle.Name} assigned to route {routeId}");
            }
        }

        public void CompleteDelivery(string vehicleId)
        {
            if (!vehicles.ContainsKey(vehicleId)) return;

            var vehicle = vehicles[vehicleId];
            if (!vehicle.InTransit || string.IsNullOrEmpty(vehicle.AssignedRouteId)) return;

            var route = routes[vehicle.AssignedRouteId];
            float cost = CalculateDeliveryCost(route);

            bool isCareer = HighLogic.CurrentGame.Mode == Game.Modes.CAREER;
            bool canAfford = !isCareer || Funding.CanAfford(cost);

            if (canAfford)
            {
                deliveryHistory.Add(new DeliveryRecord(
                    route.Id,
                    vehicle.Id,
                    route.Origin,
                    route.Destination,
                    route.ResourceType,
                    route.Quantity,
                    vehicle.CurrentCargo,
                    route.FrequencyHours,
                    true,
                    "Delivery completed successfully",
                    cost,
                    "Completed"
                ));

                vehicle.CompleteDelivery();

                if (isCareer)
                {
                    Funding.Instance.AddFunds(-cost, TransactionReasons.None);
                    Debug.Log($"[Delivery] {vehicle.Name} completed delivery for ${cost}. Funds left: {Funding.Instance.Funds}");
                }
                else
                {
                    Debug.Log($"[Sandbox] Delivery completed without deducting funds. Cost would have been ${cost}.");
                }
            }
            else
            {
                Debug.LogWarning("[Delivery] Insufficient funds for delivery.");
            }
        }


        public void ProcessDeliveries()
        {
            foreach (var route in routes.Values)
            {
                foreach (var vehicle in vehicles.Values)
                {
                    if (vehicle.AssignedRouteId == route.Id && !vehicle.InTransit)
                    {
                        // Simulate delivery start and immediate completion (for now)
                        vehicle.InTransit = true;
                        CompleteDelivery(vehicle.Id);
                    }
                }
            }
        }

        private float CalculateDeliveryCost(Route route)
        {
            // Placeholder formula for cost
            return (float)(route.Quantity * 10); // e.g. $10 per unit
        }

        public void PrintStatus()
        {
            UnityEngine.Debug.Log("=== Logistics Status ===");
            UnityEngine.Debug.Log($"Player Funds: ${playerFunds}");

            foreach (var v in vehicles.Values)
            {
                UnityEngine.Debug.Log($"[{v.Id}] {v.Name} — Cargo: {v.CurrentCargo}/{v.CargoCapacity}, InTransit: {v.InTransit}, Route: {v.AssignedRouteId}");
            }

            UnityEngine.Debug.Log("--- Delivery History ---");
            foreach (var record in deliveryHistory)
            {
                UnityEngine.Debug.Log($"[UT {record.GameTime:F2}] {record.VehicleId} delivered {record.Quantity} {record.ResourceType} (${record.Cost})");
            }
        }

        public void PrintDeliveryHistory()
        {
            Console.WriteLine("=== Delivery History ===");
            foreach (var record in deliveryHistory)
            {
                Console.WriteLine($"[UT {record.GameTime:F2}] Vehicle {record.VehicleId} delivered {record.QuantityDelivered} units on Route {record.RouteId}. Notes: {record.Notes}");
            }
        }

        public List<Route> GetActiveRoutes()
        {
            return routes.Values.ToList();
        }

        public List<DeliveryRecord> GetDeliveryHistory()
        {
            return deliveryHistory;
        }

        public void RemoveRoute(string routeId)
        {
            routes.Remove(routeId);
        }


    }
}
