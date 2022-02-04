using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using System;
using System.Collections.Generic;
using VRage.Game;
using VRageMath;

namespace IngameScript
{
    public partial class Program
    {
        public class WcPbApi
        {
            private Action<IMyTerminalBlock, IDictionary<MyDetectedEntityInfo, float>> _getSortedThreats;
            private Func<long, int, MyDetectedEntityInfo> _getAiFocus;
            private Func<IMyTerminalBlock, int, MyDetectedEntityInfo> _getWeaponTarget;
            private Action<IMyTerminalBlock, bool, int> _fireWeaponOnce;
            private Action<IMyTerminalBlock, bool, bool, int> _toggleWeaponFire;
            private Func<IMyTerminalBlock, int, bool, bool, bool> _isWeaponReadyToFire;
            private Func<long, bool> _hasGridAi;
            private Func<IMyTerminalBlock, bool> _hasCoreWeapon;

            public bool Activate(IMyTerminalBlock pbBlock)
            {
                var dict = pbBlock.GetProperty("WcPbAPI")?.As<IReadOnlyDictionary<string, Delegate>>().GetValue(pbBlock);
                if (dict == null) throw new Exception($"WcPbAPI failed to activate");
                return ApiAssign(dict);
            }

            public bool ApiAssign(IReadOnlyDictionary<string, Delegate> delegates)
            {
                if (delegates == null)
                    return false;
                AssignMethod(delegates, "GetSortedThreats", ref _getSortedThreats);
                AssignMethod(delegates, "GetAiFocus", ref _getAiFocus);
                AssignMethod(delegates, "GetWeaponTarget", ref _getWeaponTarget);
                AssignMethod(delegates, "FireWeaponOnce", ref _fireWeaponOnce);
                AssignMethod(delegates, "ToggleWeaponFire", ref _toggleWeaponFire);
                AssignMethod(delegates, "IsWeaponReadyToFire", ref _isWeaponReadyToFire);
                AssignMethod(delegates, "HasGridAi", ref _hasGridAi);
                AssignMethod(delegates, "HasCoreWeapon", ref _hasCoreWeapon);
                return true;
            }

            private void AssignMethod<T>(IReadOnlyDictionary<string, Delegate> delegates, string name, ref T field) where T : class
            {
                if (delegates == null)
                {
                    field = null;
                    return;
                }
                Delegate del;
                if (!delegates.TryGetValue(name, out del))
                    throw new Exception($"{GetType().Name} :: Couldn't find {name} delegate of type {typeof(T)}");
                field = del as T;
                if (field == null)
                    throw new Exception(
                        $"{GetType().Name} :: Delegate {name} is not type {typeof(T)}, instead it's: {del.GetType()}");
            }
            public void GetSortedThreats(IMyTerminalBlock pbBlock, IDictionary<MyDetectedEntityInfo, float> collection) =>
                _getSortedThreats?.Invoke(pbBlock, collection);
            public MyDetectedEntityInfo? GetAiFocus(long shooter, int priority = 0) => _getAiFocus?.Invoke(shooter, priority);
            public MyDetectedEntityInfo? GetWeaponTarget(IMyTerminalBlock weapon, int weaponId = 0) =>
                _getWeaponTarget?.Invoke(weapon, weaponId) ?? null;
            public void FireWeaponOnce(IMyTerminalBlock weapon, bool allWeapons = true, int weaponId = 0) =>
                _fireWeaponOnce?.Invoke(weapon, allWeapons, weaponId);
            public void ToggleWeaponFire(IMyTerminalBlock weapon, bool on, bool allWeapons, int weaponId = 0) =>
                _toggleWeaponFire?.Invoke(weapon, on, allWeapons, weaponId);
            public bool IsWeaponReadyToFire(IMyTerminalBlock weapon, int weaponId = 0, bool anyWeaponReady = true,
                bool shootReady = false) =>
                _isWeaponReadyToFire?.Invoke(weapon, weaponId, anyWeaponReady, shootReady) ?? false;
            public bool HasGridAi(long entity) => _hasGridAi?.Invoke(entity) ?? false;
            public bool HasCoreWeapon(IMyTerminalBlock weapon) => _hasCoreWeapon?.Invoke(weapon) ?? false;
        }
    }
}