using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Koko
{
    class Program
    {
        static void Main(string[] args)
        {
            var manager = new DeliveryManager();

            var vehicle1 = new TransportVehicle("v1", "Rover Alpha", 100, 20);  // 20 units/hour
            var vehicle2 = new TransportVehicle("v2", "Lander Bravo", 200, 30);


            var route1 = new Route("r1", "Base A", "Base B", "Oxygen", 50, 3f);
            var route2 = new Route("r2", "Base C", "Base D", "Fuel", 100, 2f);

            manager.RegisterVehicle(vehicle1);
            manager.RegisterVehicle(vehicle2);

            manager.RegisterRoute(route1);
            manager.RegisterRoute(route2);

            manager.AssignVehicleToRoute("v1", "r1");
            manager.AssignVehicleToRoute("v2", "r2");

            manager.CompleteDelivery("v1");
            manager.CompleteDelivery("v2");

            manager.PrintStatus();

            Console.ReadLine(); // Keeps console window open
        }
    }
}

