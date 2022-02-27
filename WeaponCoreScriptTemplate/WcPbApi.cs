using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using System;
using System.Collections.Generic;
using VRage;
using VRage.Game;
using VRageMath;

namespace IngameScript
{
    public partial class Program
    {
        /// <summary>Implementation of the WeaponCore PB API</summary>
        ///
        /// relevant docs: https://steamcommunity.com/sharedfiles/filedetails/?id=2178802013
        ///     https://github.com/sstixrud/WeaponCore/blob/master/Data/Scripts/CoreSystems/Api/CoreSystemsPbApi.cs
        ///     https://github.com/sstixrud/WeaponCore/blob/master/Data/Scripts/CoreSystems/Api/ApiBackend.cs
        ///
        public class WcPbApi
        {
            private Action<ICollection<MyDefinitionId>> _getCoreWeapons;
            private Action<ICollection<MyDefinitionId>> _getCoreStaticLaunchers;
            private Action<ICollection<MyDefinitionId>> _getCoreTurrets;
            private Func<IMyTerminalBlock, IDictionary<string, int>, bool> _getBlockWeaponMap;
            private Func<long, MyTuple<bool, int, int>> _getProjectilesLockedOn;
            private Action<IMyTerminalBlock, IDictionary<MyDetectedEntityInfo, float>> _getSortedThreats;
            private Action<IMyTerminalBlock, ICollection<MyDetectedEntityInfo>> _getObstructions;
            private Func<long, int, MyDetectedEntityInfo> _getAiFocus;
            private Func<IMyTerminalBlock, long, int, bool> _setAiFocus;
            private Func<IMyTerminalBlock, int, MyDetectedEntityInfo> _getWeaponTarget;
            private Action<IMyTerminalBlock, long, int> _setWeaponTarget;
            private Action<IMyTerminalBlock, bool, int> _fireWeaponOnce;
            private Action<IMyTerminalBlock, bool, bool, int> _toggleWeaponFire;
            private Func<IMyTerminalBlock, int, bool, bool, bool> _isWeaponReadyToFire;
            private Func<IMyTerminalBlock, int, float> _getMaxWeaponRange;
            private Func<IMyTerminalBlock, ICollection<string>, int, bool> _getTurretTargetTypes;
            private Action<IMyTerminalBlock, ICollection<string>, int> _setTurretTargetTypes;
            private Action<IMyTerminalBlock, float> _setBlockTrackingRange;
            private Func<IMyTerminalBlock, long, int, bool> _isTargetAligned;
            private Func<IMyTerminalBlock, long, int, bool> _canShootTarget;
            private Func<IMyTerminalBlock, long, int, Vector3D?> _getPredictedTargetPos;
            private Func<IMyTerminalBlock, float> _getHeatLevel;
            private Func<IMyTerminalBlock, float> _currentPowerConsumption;
            private Func<MyDefinitionId, float> _getMaxPower;
            private Func<long, bool> _hasGridAi;
            private Func<IMyTerminalBlock, bool> _hasCoreWeapon;
            private Func<long, float> _getOptimalDps;
            private Func<IMyTerminalBlock, int, string> _getActiveAmmo;
            private Action<IMyTerminalBlock, int, string> _setActiveAmmo;
            private Action<IMyTerminalBlock, int, Action<long, int, ulong, long, Vector3D, bool>> _monitorProjectile;
            private Action<IMyTerminalBlock, int, Action<long, int, ulong, long, Vector3D, bool>> _unMonitorProjectile;
            private Func<long, float> _getConstructEffectiveDps;
            private Func<IMyTerminalBlock, long> _getPlayerController;
            private Func<IMyTerminalBlock, int, Matrix> _getWeaponAzimuthMatrix;
            private Func<IMyTerminalBlock, int, Matrix> _getWeaponElevationMatrix;
            private Func<IMyTerminalBlock, long, bool, bool, bool> _isTargetValid;
            private Func<IMyTerminalBlock, int, MyTuple<Vector3D, Vector3D>> _getWeaponScope;
            private Func<IMyTerminalBlock, MyTuple<bool, bool>> _isInRange;

            /// <summary>Initializes the API.</summary>
            /// <exception cref="Exception">If the WcPbAPI property added by WeaponCore couldn't be found on the block.</exception>
            public bool Activate(IMyTerminalBlock pbBlock)
            {
                var dict = pbBlock.GetProperty("WcPbAPI")?.As<IReadOnlyDictionary<string, Delegate>>().GetValue(pbBlock);
                if (dict == null) throw new Exception($"WcPbAPI failed to activate");
                return ApiAssign(dict);
            }

            /// <summary>Assigns WeaponCore's API methods to callable properties.</summary>
            public bool ApiAssign(IReadOnlyDictionary<string, Delegate> delegates)
            {
                if (delegates == null)
                    return false;
                AssignMethod(delegates, "GetCoreWeapons", ref _getCoreWeapons);
                AssignMethod(delegates, "GetCoreStaticLaunchers", ref _getCoreStaticLaunchers);
                AssignMethod(delegates, "GetCoreTurrets", ref _getCoreTurrets);
                AssignMethod(delegates, "GetBlockWeaponMap", ref _getBlockWeaponMap);
                AssignMethod(delegates, "GetProjectilesLockedOn", ref _getProjectilesLockedOn);
                AssignMethod(delegates, "GetSortedThreats", ref _getSortedThreats);
                AssignMethod(delegates, "GetObstructions", ref _getObstructions);
                AssignMethod(delegates, "GetAiFocus", ref _getAiFocus);
                AssignMethod(delegates, "SetAiFocus", ref _setAiFocus);
                AssignMethod(delegates, "GetWeaponTarget", ref _getWeaponTarget);
                AssignMethod(delegates, "SetWeaponTarget", ref _setWeaponTarget);
                AssignMethod(delegates, "FireWeaponOnce", ref _fireWeaponOnce);
                AssignMethod(delegates, "ToggleWeaponFire", ref _toggleWeaponFire);
                AssignMethod(delegates, "IsWeaponReadyToFire", ref _isWeaponReadyToFire);
                AssignMethod(delegates, "GetMaxWeaponRange", ref _getMaxWeaponRange);
                AssignMethod(delegates, "GetTurretTargetTypes", ref _getTurretTargetTypes);
                AssignMethod(delegates, "SetTurretTargetTypes", ref _setTurretTargetTypes);
                AssignMethod(delegates, "SetBlockTrackingRange", ref _setBlockTrackingRange);
                AssignMethod(delegates, "IsTargetAligned", ref _isTargetAligned);
                AssignMethod(delegates, "CanShootTarget", ref _canShootTarget);
                AssignMethod(delegates, "GetPredictedTargetPosition", ref _getPredictedTargetPos);
                AssignMethod(delegates, "GetHeatLevel", ref _getHeatLevel);
                AssignMethod(delegates, "GetCurrentPower", ref _currentPowerConsumption);
                AssignMethod(delegates, "GetMaxPower", ref _getMaxPower);
                AssignMethod(delegates, "HasGridAi", ref _hasGridAi);
                AssignMethod(delegates, "HasCoreWeapon", ref _hasCoreWeapon);
                AssignMethod(delegates, "GetOptimalDps", ref _getOptimalDps);
                AssignMethod(delegates, "GetActiveAmmo", ref _getActiveAmmo);
                AssignMethod(delegates, "SetActiveAmmo", ref _setActiveAmmo);
                AssignMethod(delegates, "MonitorProjectile", ref _monitorProjectile);
                AssignMethod(delegates, "UnMonitorProjectile", ref _unMonitorProjectile);
                AssignMethod(delegates, "GetConstructEffectiveDps", ref _getConstructEffectiveDps);
                AssignMethod(delegates, "GetPlayerController", ref _getPlayerController);
                AssignMethod(delegates, "GetWeaponAzimuthMatrix", ref _getWeaponAzimuthMatrix);
                AssignMethod(delegates, "GetWeaponElevationMatrix", ref _getWeaponElevationMatrix);
                AssignMethod(delegates, "IsTargetValid", ref _isTargetValid);
                AssignMethod(delegates, "GetWeaponScope", ref _getWeaponScope);
                AssignMethod(delegates, "IsInRange", ref _isInRange);
                return true;
            }

            /// <summary>Assigns a delegate method to a property.</summary>
            private void AssignMethod<T>(IReadOnlyDictionary<string, Delegate> delegates, string name, ref T field) where T : class
            {
                if (delegates == null) {
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

            /// <summary>Returns a list of all WeaponCore weapon blocks.</summary>
            public void GetAllCoreWeapons(ICollection<MyDefinitionId> collection) => _getCoreWeapons?.Invoke(collection);

            /// <summary>Returns a list of all fixed weapon blocks (i.e. vanilla Rocket Launcher) registered in WeaponCore.</summary>
            public void GetAllCoreStaticLaunchers(ICollection<MyDefinitionId> collection) =>
                _getCoreStaticLaunchers?.Invoke(collection);

            /// <summary>Returns a list of all turret blocks registered in WeaponCore.</summary>
            public void GetAllCoreTurrets(ICollection<MyDefinitionId> collection) => _getCoreTurrets?.Invoke(collection);

            /// <summary>Returns a dictionary representing all weapons on the selected block (I think).</summary>
            public bool GetBlockWeaponMap(IMyTerminalBlock weaponBlock, IDictionary<string, int> collection) =>
                _getBlockWeaponMap?.Invoke(weaponBlock, collection) ?? false;

            /// <summary>Returns if and how many projectiles are locked on to the target entity.</summary>
            public MyTuple<bool, int, int> GetProjectilesLockedOn(long victim) =>
                _getProjectilesLockedOn?.Invoke(victim) ?? new MyTuple<bool, int, int>();

            /// <summary>Returns a dictionary of detected threats sorted by their offense rating (ranging from 0 to 5).</summary>
            public void GetSortedThreats(IMyTerminalBlock pbBlock, IDictionary<MyDetectedEntityInfo, float> collection) =>
                _getSortedThreats?.Invoke(pbBlock, collection);

            /// <summary>Returns a collection of 'obstructions' of the grid.</summary>
            public void GetObstructions(IMyTerminalBlock pbBlock, ICollection<MyDetectedEntityInfo> collection) =>
                _getObstructions?.Invoke(pbBlock, collection);

            /// <summary>Returns info about the targeted Entity of the shooter grid. This is the target selected via the WeaponCore HUD.</summary>
            public MyDetectedEntityInfo? GetAiFocus(long shooter, int priority = 0) => _getAiFocus?.Invoke(shooter, priority);

            /// <summary>Sets the targeted Entity of the shooter grid. This is the target selected via the WeaponCore HUD.</summary>
            public bool SetAiFocus(IMyTerminalBlock pbBlock, long target, int priority = 0) =>
                _setAiFocus?.Invoke(pbBlock, target, priority) ?? false;

            /// <summary>Returns info about the Entity targeted by the weapon block.</summary>
            public MyDetectedEntityInfo? GetWeaponTarget(IMyTerminalBlock weapon, int weaponId = 0) =>
                _getWeaponTarget?.Invoke(weapon, weaponId) ?? null;

            /// <summary>Sets the Entity targeted by the weapon block.</summary>
            public void SetWeaponTarget(IMyTerminalBlock weapon, long target, int weaponId = 0) =>
                _setWeaponTarget?.Invoke(weapon, target, weaponId);

            /// <summary>Fires the given weapon once. Optionally shoots a specific barrel of the weapon. Might be bugged atm.</summary>
            public void FireWeaponOnce(IMyTerminalBlock weapon, bool allWeapons = true, int weaponId = 0) =>
                _fireWeaponOnce?.Invoke(weapon, allWeapons, weaponId);

            /// <summary>Sets the firing state of the weapon. Might be bugged atm.</summary>
            public void ToggleWeaponFire(IMyTerminalBlock weapon, bool on, bool allWeapons, int weaponId = 0) =>
                _toggleWeaponFire?.Invoke(weapon, on, allWeapons, weaponId);

            /// <summary>Returns whether the weapon is ready to fire again. Optionally returns true if a specific barrel is ready.</summary>
            public bool IsWeaponReadyToFire(IMyTerminalBlock weapon, int weaponId = 0, bool anyWeaponReady = true,
                bool shootReady = false) =>
                _isWeaponReadyToFire?.Invoke(weapon, weaponId, anyWeaponReady, shootReady) ?? false;

            /// <summary>Returns the maximum range of the selected weapon. Targeting range might be higher than this.</summary>
            public float GetMaxWeaponRange(IMyTerminalBlock weapon, int weaponId) =>
                _getMaxWeaponRange?.Invoke(weapon, weaponId) ?? 0f;

            /// <summary>Returns the current target types of the selected weapon.</summary>
            public bool GetTurretTargetTypes(IMyTerminalBlock weapon, IList<string> collection, int weaponId = 0) =>
                _getTurretTargetTypes?.Invoke(weapon, collection, weaponId) ?? false;

            /// <summary>Sets the current target types of the selected weapon.</summary>
            public void SetTurretTargetTypes(IMyTerminalBlock weapon, IList<string> collection, int weaponId = 0) =>
                _setTurretTargetTypes?.Invoke(weapon, collection, weaponId);

            /// <summary>Sets the tracking range of the selected weapon.</summary>
            public void SetBlockTrackingRange(IMyTerminalBlock weapon, float range) =>
                _setBlockTrackingRange?.Invoke(weapon, range);

            /// <summary>Returns if the weapon is aligned to shoot the target entity.</summary>
            public bool IsTargetAligned(IMyTerminalBlock weapon, long targetEnt, int weaponId) =>
                _isTargetAligned?.Invoke(weapon, targetEnt, weaponId) ?? false;

            /// <summary>Returns if the weapon can shoot the target.</summary>
            public bool CanShootTarget(IMyTerminalBlock weapon, long targetEnt, int weaponId) =>
                _canShootTarget?.Invoke(weapon, targetEnt, weaponId) ?? false;

            /// <summary>Returns the position of the target once the bullet reaches its distance, if the bullet would be fired now.</summary>
            public Vector3D? GetPredictedTargetPosition(IMyTerminalBlock weapon, long targetEnt, int weaponId) =>
                _getPredictedTargetPos?.Invoke(weapon, targetEnt, weaponId) ?? null;

            /// <summary>Returns the current heat level of the selected weapon.</summary>
            public float GetHeatLevel(IMyTerminalBlock weapon) => _getHeatLevel?.Invoke(weapon) ?? 0f;

            /// <summary>Returns the current power drawn by the selected weapon.</summary>
            public float GetCurrentPower(IMyTerminalBlock weapon) => _currentPowerConsumption?.Invoke(weapon) ?? 0f;

            /// <summary>Returns the maximum power the selected weapon can draw from the grid.</summary>
            public float GetMaxPower(MyDefinitionId weaponDef) => _getMaxPower?.Invoke(weaponDef) ?? 0f;

            /// <summary>Returns if the entity has a WeaponCore grid ai, meaning it has WeaponCore weapons and a.</summary>
            public bool HasGridAi(long entity) => _hasGridAi?.Invoke(entity) ?? false;

            /// <summary>Returns if the block is a WeaponCore weapon.</summary>
            public bool HasCoreWeapon(IMyTerminalBlock weapon) => _hasCoreWeapon?.Invoke(weapon) ?? false;

            /// <summary>Returns the optimal dps for the selected entity.</summary>
            public float GetOptimalDps(long entity) => _getOptimalDps?.Invoke(entity) ?? 0f;

            /// <summary>Returns the name of the current ammo loaded in the selected weapon.</summary>
            public string GetActiveAmmo(IMyTerminalBlock weapon, int weaponId) =>
                _getActiveAmmo?.Invoke(weapon, weaponId) ?? null;

            /// <summary>Sets the current ammo loaded in the selected weapon based on name.</summary>
            public void SetActiveAmmo(IMyTerminalBlock weapon, int weaponId, string ammoType) =>
                _setActiveAmmo?.Invoke(weapon, weaponId, ammoType);

            /// <summary>.</summary>
            /// <param name="action">long Block EntityId, int PartId, ulong ProjectileId, long LastHitId, Vector3D LastPos, bool Start</param>
            public void MonitorProjectileCallback(IMyTerminalBlock weapon, int weaponId, Action<long, int, ulong, long, Vector3D, bool> action) =>
                _monitorProjectile?.Invoke(weapon, weaponId, action);

            /// <summary>.</summary>
            /// <param name="action">long Block EntityId, int PartId, ulong ProjectileId, long LastHitId, Vector3D LastPos, bool Start</param>
            public void UnMonitorProjectileCallback(IMyTerminalBlock weapon, int weaponId, Action<long, int, ulong, long, Vector3D, bool> action) =>
                _unMonitorProjectile?.Invoke(weapon, weaponId, action);

            /// <summary>Returns the effective dps of the selected entity.</summary>
            public float GetConstructEffectiveDps(long entity) => _getConstructEffectiveDps?.Invoke(entity) ?? 0f;

            /// <summary>Returns the id of the player controlling the turret.</summary>
            public long GetPlayerController(IMyTerminalBlock weapon) => _getPlayerController?.Invoke(weapon) ?? -1;

            /// <summary>Returns the orientation matrix of the azimuth part of the selected weapon.</summary>
            public Matrix GetWeaponAzimuthMatrix(IMyTerminalBlock weapon, int weaponId) =>
                _getWeaponAzimuthMatrix?.Invoke(weapon, weaponId) ?? Matrix.Zero;

            /// <summary>Returns the orientation matrix of the elevation part of the selected weapon.</summary>
            public Matrix GetWeaponElevationMatrix(IMyTerminalBlock weapon, int weaponId) =>
                _getWeaponElevationMatrix?.Invoke(weapon, weaponId) ?? Matrix.Zero;

            /// <summary>Returns whether the target is a valid target for the selected weapon optionally checking for threats and relations.</summary>
            public bool IsTargetValid(IMyTerminalBlock weapon, long targetId, bool onlyThreats, bool checkRelations) =>
                _isTargetValid?.Invoke(weapon, targetId, onlyThreats, checkRelations) ?? false;

            /// <summary>Returns the scope (?) of the selected weapon.</summary>
            public MyTuple<Vector3D, Vector3D> GetWeaponScope(IMyTerminalBlock weapon, int weaponId) =>
                _getWeaponScope?.Invoke(weapon, weaponId) ?? new MyTuple<Vector3D, Vector3D>();

            /// <summary>Returns whether a threat or 'other' is in range of the weapon.</summary>
            public MyTuple<bool, bool> IsInRange(IMyTerminalBlock block) =>
                _isInRange?.Invoke(block) ?? new MyTuple<bool, bool>();
        }
    }
}