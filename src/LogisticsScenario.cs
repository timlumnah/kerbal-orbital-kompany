using System;
using System.Collections.Generic;
using UnityEngine;
using KSP;

namespace Koko
{
    [KSPScenario(ScenarioCreationOptions.AddToAllGames, GameScenes.SPACECENTER, GameScenes.FLIGHT, GameScenes.TRACKSTATION)]
    public class LogisticsScenario : ScenarioModule
    {
        public static LogisticsScenario Instance;

        public List<Route> SavedRoutes = new List<Route>();
        public List<Business> ActiveBusinesses = new List<Business>();


        private double lastCheckTime = 0;

        public void Update()
        {
            double currentTime = Planetarium.GetUniversalTime();

            if (currentTime - lastCheckTime > 60) // Every 60s in game time
            {
                lastCheckTime = currentTime;

                // Trigger deliveries
                LogisticsMain.Instance?.ProcessRecurringDeliveries();
            }

            foreach (var biz in ActiveBusinesses)
            {
                double timeSinceLast = currentTime - biz.LastPayoutTime;
                double secondsPerMonth = 6 * 3600 * 30; // KSP: 6-hour days, 30-day month

                if (timeSinceLast >= secondsPerMonth)
                {
                    int payoutsDue = (int)(timeSinceLast / secondsPerMonth);
                    float totalIncome = payoutsDue * biz.MonthlyIncome;

                    Funding.Instance.AddFunds(totalIncome, TransactionReasons.None);
                    biz.LastPayoutTime += payoutsDue * secondsPerMonth;

                    Debug.Log($"[TimMod] {payoutsDue} payouts processed for {biz.Type} (${totalIncome:NO}).");
                }
            }

        }


        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            Instance = this;

            SavedRoutes.Clear();
            if (node.HasNode("ROUTE"))
            {
                foreach (ConfigNode routeNode in node.GetNodes("ROUTE"))
                {
                    var route = new Route(routeNode); // Parses the node into a Route object
                    SavedRoutes.Add(route);           // Stores it in memory
                }
            }

            if (node.HasNode("BUSINESS"))
            {
                foreach (ConfigNode businessNode in node.GetNodes("BUSINESS"))
                {
                    ActiveBusinesses.Add(new Business(businessNode));
                }
            }


            Debug.Log($"[TimMod] Loaded {SavedRoutes.Count} routes from save.");
        }

        public override void OnSave(ConfigNode node)
        {
            base.OnSave(node);

            foreach (var route in SavedRoutes)
            {
                ConfigNode routeNode = route.ToConfigNode(); // Creates a KSP-style save node
                node.AddNode(routeNode);                     // Appends it to the save
            }

            foreach (var business in ActiveBusinesses)
            {
                node.AddNode(business.ToConfigNode());
            }


            Debug.Log($"[TimMod] Saved {SavedRoutes.Count} routes.");
        }
    }
}
