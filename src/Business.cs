using System;
using UnityEngine;

namespace Koko
{
    public class Business
    {
        public string Id { get; set; }
        public string Type { get; set; }
        public float StartupCost { get; set; }
        public float MonthlyIncome { get; set; }
        public double LastPayoutTime { get; set; }

        public Business() { }

        public Business(string type, float startupCost, float monthlyIncome)
        {
            Id = Guid.NewGuid().ToString();
            Type = type;
            StartupCost = startupCost;
            MonthlyIncome = monthlyIncome;
            LastPayoutTime = Planetarium.GetUniversalTime();
        }

        public Business(ConfigNode node)
        {
            Id = node.GetValue("Id");
            Type = node.GetValue("Type");
            StartupCost = float.Parse(node.GetValue("StartupCost"));
            MonthlyIncome = float.Parse(node.GetValue("MonthlyIncome"));
            LastPayoutTime = double.Parse(node.GetValue("LastPayoutTime"));
        }

        public ConfigNode ToConfigNode()
        {
            ConfigNode node = new ConfigNode("BUSINESS");
            node.AddValue("Id", Id);
            node.AddValue("Type", Type);
            node.AddValue("StartupCost", StartupCost);
            node.AddValue("MonthlyIncome", MonthlyIncome);
            node.AddValue("LastPayoutTime", LastPayoutTime);
            return node;
        }
    }
}
