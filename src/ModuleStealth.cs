using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;

namespace Koko
{
    public class ModuleStealth : PartModule
    {
        [KSPField(isPersistant = false)]
        public float stealthValue = 1.0f;

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            Debug.Log($"[TimMod] Stealth module loaded with stealthValue = {stealthValue}");
        }
    }
}
