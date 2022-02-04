using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ObjectBuilders.Definitions;
using VRageMath;

namespace IngameScript
{
    public partial class Program
    {
        public class WcPbApi
        {
            private Action<ICollection<MyDefinitionId>> _getCoreWeapons;
            private Action<ICollection<MyDefinitionId>> _getCoreStaticLaunchers;
            private Action<ICollection<MyDefinitionId>> _getCoreTurrets;
            private Func<IMyTerminalBlock, IDictionary<string, int>, bool> _getBlockWeaponMap;
            private Action<IMyTerminalBlock, IDictionary<MyDetectedEntityInfo, float>> _getSortedThreats;
            private Func<long, int, MyDetectedEntityInfo> _getAiFocus;
            private Func<IMyTerminalBlock, int, MyDetectedEntityInfo> _getWeaponTarget;
            private Func<IMyTerminalBlock, int, bool, bool, bool> _isWeaponReadyToFire;
            private Func<IMyTerminalBlock, int, float> _getMaxWeaponRange;
            private Func<IMyTerminalBlock, ICollection<string>, int, bool> _getTurretTargetTypes;
            private Func<IMyTerminalBlock, long, int, bool> _isTargetAligned;
            private Func<IMyTerminalBlock, long, int, bool> _canShootTarget;
            private Func<IMyTerminalBlock, long, int, Vector3D?> _getPredictedTargetPos;
            private Func<long, bool> _hasGridAi;
            private Func<IMyTerminalBlock, bool> _hasCoreWeapon;
            private Func<long, float> _getOptimalDps;
            private Func<IMyTerminalBlock, int, string> _getActiveAmmo;
            private Func<long, float> _getConstructEffectiveDps;
            private Func<IMyTerminalBlock, long> _getPlayerController;
            private Func<Sandbox.ModAPI.Ingame.IMyTerminalBlock, int, Matrix> _getWeaponAzimuthMatrix;
            private Func<Sandbox.ModAPI.Ingame.IMyTerminalBlock, int, Matrix> _getWeaponElevationMatrix;

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
                AssignMethod(delegates, "GetCoreWeapons", ref _getCoreWeapons);
                AssignMethod(delegates, "GetCoreStaticLaunchers", ref _getCoreStaticLaunchers);
                AssignMethod(delegates, "GetCoreTurrets", ref _getCoreTurrets);
                AssignMethod(delegates, "GetBlockWeaponMap", ref _getBlockWeaponMap);
                AssignMethod(delegates, "GetSortedThreats", ref _getSortedThreats);
                AssignMethod(delegates, "GetAiFocus", ref _getAiFocus);
                AssignMethod(delegates, "GetWeaponTarget", ref _getWeaponTarget);
                AssignMethod(delegates, "IsWeaponReadyToFire", ref _isWeaponReadyToFire);
                AssignMethod(delegates, "GetMaxWeaponRange", ref _getMaxWeaponRange);
                AssignMethod(delegates, "GetTurretTargetTypes", ref _getTurretTargetTypes);
                AssignMethod(delegates, "IsTargetAligned", ref _isTargetAligned);
                AssignMethod(delegates, "CanShootTarget", ref _canShootTarget);
                AssignMethod(delegates, "GetPredictedTargetPosition", ref _getPredictedTargetPos);
                AssignMethod(delegates, "HasGridAi", ref _hasGridAi);
                AssignMethod(delegates, "HasCoreWeapon", ref _hasCoreWeapon);
                AssignMethod(delegates, "GetOptimalDps", ref _getOptimalDps);
                AssignMethod(delegates, "GetActiveAmmo", ref _getActiveAmmo);
                AssignMethod(delegates, "GetConstructEffectiveDps", ref _getConstructEffectiveDps);
                AssignMethod(delegates, "GetPlayerController", ref _getPlayerController);
                AssignMethod(delegates, "GetWeaponAzimuthMatrix", ref _getWeaponAzimuthMatrix);
                AssignMethod(delegates, "GetWeaponElevationMatrix", ref _getWeaponElevationMatrix);
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
            public void GetAllCoreWeapons(ICollection<MyDefinitionId> collection) => _getCoreWeapons?.Invoke(collection);
            public void GetAllCoreStaticLaunchers(ICollection<MyDefinitionId> collection) =>
                _getCoreStaticLaunchers?.Invoke(collection);
            public void GetAllCoreTurrets(ICollection<MyDefinitionId> collection) => _getCoreTurrets?.Invoke(collection);
            public bool GetBlockWeaponMap(IMyTerminalBlock weaponBlock, IDictionary<string, int> collection) =>
                _getBlockWeaponMap?.Invoke(weaponBlock, collection) ?? false;
            public void GetSortedThreats(IMyTerminalBlock pbBlock, IDictionary<MyDetectedEntityInfo, float> collection) =>
                _getSortedThreats?.Invoke(pbBlock, collection);
            public MyDetectedEntityInfo? GetAiFocus(long shooter, int priority = 0) => _getAiFocus?.Invoke(shooter, priority);
            public MyDetectedEntityInfo? GetWeaponTarget(IMyTerminalBlock weapon, int weaponId = 0) =>
                _getWeaponTarget?.Invoke(weapon, weaponId) ?? null;
            public bool IsWeaponReadyToFire(IMyTerminalBlock weapon, int weaponId = 0, bool anyWeaponReady = true,
                bool shootReady = false) => _isWeaponReadyToFire?.Invoke(weapon, weaponId, anyWeaponReady, shootReady) ?? false;
            public float GetMaxWeaponRange(IMyTerminalBlock weapon, int weaponId) =>
                _getMaxWeaponRange?.Invoke(weapon, weaponId) ?? 0f;
            public bool GetTurretTargetTypes(IMyTerminalBlock weapon, IList<string> collection, int weaponId = 0) =>
                _getTurretTargetTypes?.Invoke(weapon, collection, weaponId) ?? false;
            public bool IsTargetAligned(IMyTerminalBlock weapon, long targetEnt, int weaponId) =>
                _isTargetAligned?.Invoke(weapon, targetEnt, weaponId) ?? false;
            public bool CanShootTarget(IMyTerminalBlock weapon, long targetEnt, int weaponId) =>
                _canShootTarget?.Invoke(weapon, targetEnt, weaponId) ?? false;
            public Vector3D? GetPredictedTargetPosition(IMyTerminalBlock weapon, long targetEnt, int weaponId) =>
                _getPredictedTargetPos?.Invoke(weapon, targetEnt, weaponId) ?? null;
            public bool HasGridAi(long entity) => _hasGridAi?.Invoke(entity) ?? false;
            public bool HasCoreWeapon(IMyTerminalBlock weapon) => _hasCoreWeapon?.Invoke(weapon) ?? false;
            public float GetOptimalDps(long entity) => _getOptimalDps?.Invoke(entity) ?? 0f;
            public string GetActiveAmmo(IMyTerminalBlock weapon, int weaponId) =>
                _getActiveAmmo?.Invoke(weapon, weaponId) ?? null;
            public float GetConstructEffectiveDps(long entity) => _getConstructEffectiveDps?.Invoke(entity) ?? 0f;
            public long GetPlayerController(IMyTerminalBlock weapon) => _getPlayerController?.Invoke(weapon) ?? -1;
            public Matrix GetWeaponAzimuthMatrix(Sandbox.ModAPI.Ingame.IMyTerminalBlock weapon, int weaponId) =>
                _getWeaponAzimuthMatrix?.Invoke(weapon, weaponId) ?? Matrix.Zero;
            public Matrix GetWeaponElevationMatrix(Sandbox.ModAPI.Ingame.IMyTerminalBlock weapon, int weaponId) =>
                _getWeaponElevationMatrix?.Invoke(weapon, weaponId) ?? Matrix.Zero;
        }
    }
}