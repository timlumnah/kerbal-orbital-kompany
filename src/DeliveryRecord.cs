using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using KSP;

namespace Koko
{
    public class TimeTest : MonoBehaviour
    {
        void Start()
        {
            double time = Planetarium.GetUniversalTime();
            Debug.Log("KSP Time: " + time);
        }
    }
    // Removed MonoBehaviour. Pure data class, no Unity lifecycle needed.
    public class DeliveryRecord
    {
        public string RouteId { get; set; }
        public string VehicleId { get; set; }
        public string ResourceType { get; set; }
        public double Quantity { get; set; }
        //public DateTime Timestamp { get; set; }
        public double GameTime { get; set; } // In hours, to match KSP's time system
        public float Cost { get; set; }
        public string Status { get; set; }  // Completed, Delayed, etc.
        public double QuantityDelivered { get; set; }
        public float DeliveryTimeHours { get; set; }
        public bool Success { get; set; }
        public string Notes { get; set; }
        public string Destination { get; set; }
        public string Origin { get; set; }

        public DeliveryRecord()
        {
            // Default constructor for serialization
        }

        public DeliveryRecord(string routeId, string vehicleId, string origin, string destination, string resourceType, double quantity, double quantityDelivered, float deliveryTimeHours, bool success, string notes, float cost, string status)
        {
            RouteId = routeId;
            VehicleId = vehicleId;
            Origin = origin;
            Destination = destination;
            ResourceType = resourceType;
            Quantity = quantity;
            QuantityDelivered = quantityDelivered;
            DeliveryTimeHours = deliveryTimeHours;
            Success = success;
            Notes = notes;
            GameTime = Planetarium.GetUniversalTime();
            Cost = cost;
            Status = status;
        }

    }
}

