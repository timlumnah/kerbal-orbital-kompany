using System.Linq;
using UnityEngine;
using KSP;
using Contracts;

namespace Koko
{
    // Runs in every scene; quietly cancels anything that the stock generator offers.
    [KSPAddon(KSPAddon.Startup.AllGameScenes, false)]
    public class ContractSuppressor : MonoBehaviour
    {
        private float _nextSweep;

        void Update()
        {
            if (HighLogic.CurrentGame?.Mode != Game.Modes.CAREER) return;

            // Sweep every 5 seconds of real time (cheap).
            if (Time.realtimeSinceStartup < _nextSweep) return;
            _nextSweep = Time.realtimeSinceStartup + 5f;

            var cs = ContractSystem.Instance;
            if (cs == null || cs.Contracts == null) return;

            int killed = 0;
            foreach (var c in cs.Contracts.ToList())
            {
                if (c == null) continue;

                var st = c.ContractState;
                if (st == Contract.State.Offered || st == Contract.State.Active || st == Contract.State.DeadlineExpired)
                {
                    try { c.Cancel(); killed++; } catch { /* best effort */ }
                }
            }

            if (killed > 0)
                Debug.Log($"[TimMod] ContractSuppressor: cancelled {killed} contracts.");
        }
    }
}
