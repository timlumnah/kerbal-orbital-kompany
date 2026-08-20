using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Koko
{
    public class TransportVehicle
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public double CargoCapacity { get; set; } // In units or tons
        public double CurrentCargo { get; set; }
        public double Speed { get; set; } // Units per hour
        public string AssignedRouteId { get; set; }
        public bool InTransit { get; set; }

        public TransportVehicle(string id, string name, double cargoCapacity, double speed)
        {
            Id = id;
            Name = name;
            CargoCapacity = cargoCapacity;
            Speed = speed;
            CurrentCargo = 0;
            AssignedRouteId = null;
            InTransit = false;
        }

        public void AssignRoute(string routeId)
        {
            AssignedRouteId = routeId;
            InTransit = true;
        }

        public void CompleteDelivery()
        {
            InTransit = false;
            CurrentCargo = 0;
        }
    }
}

