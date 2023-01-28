using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
    public partial class Program : MyGridProgram
    {
        public class RotorController
        {
            /// <summary>The rotor controlled by this instance.</summary>
            public IMyMotorStator Rotor { get; }

            /// <summary>Creates a new controller for the rotor.</summary>
            public RotorController(IMyMotorStator rotor)
            {
                Rotor = rotor;
            }

            /// <summary>Moves the rotor to its rest position.</summary>
            public void MoveToRest(float maxSpeed)
            {
                var neg = Rotor.CustomData.Contains("-");
                var pos = 0;
                try
                {
                    pos = Convert.ToInt32(Rotor.CustomData);
                } catch
                {
                    var has_rest = ConfigIni.Get(WHIP_REST_ANGLE_TAG, WHIP_HAS_REST_KEY).ToBoolean();
                    if (has_rest)
                        pos = ConfigIni.Get(WHIP_REST_ANGLE_TAG, WHIP_REST_ANGLE_KEY).ToInt16();
                }

                if (Math.Abs(MathHelper.ToRadians(pos) - Rotor.Angle) < 0.01)
                {
                    Rotor.TargetVelocityRad = 0;
                } else {
                    Rotor.RotorLock = false;
                    SetRestSpeed(maxSpeed, pos);
                }
            }

            /// <summary>Sets the speed of the rotor to reach its rest position.</summary>
            private void SetRestSpeed(float maxSpeed, float targetAngleDeg)
            {
                float currentAngleDeg = MathHelper.ToDegrees(Rotor.Angle);

                #region WHERE_IT_BREAKS
                float angleDiff = 180 - Math.Abs(Math.Abs(targetAngleDeg - currentAngleDeg) - 180);
                if (currentAngleDeg < 180)
                {
                    angleDiff *= -1;
                }
                #endregion

                float targetSpeed = angleDiff / 360 * (float)maxSpeed;
                if (Rotor.BlockDefinition.SubtypeName.Contains("Hinge"))
                {
                    targetSpeed = (targetAngleDeg - currentAngleDeg) / 180 * (float)maxSpeed;
                }
                Rotor.TargetVelocityRad = MathHelper.Clamp(targetSpeed, -1 * (float)maxSpeed, (float)maxSpeed);
            }

            /// <summary>Sets the speed of the rotor to aim at the target.</summary>
            /// <param name="offset">The offset in radians from the target vector.</param>
            public void SetRotorSpeedFromOffset(float offset, float multiplier, float maxSpeed)
            {
                Rotor.RotorLock = false;
                Rotor.TargetVelocityRad = MathHelper.Clamp(offset * multiplier, -1 * maxSpeed, maxSpeed);
            }

            /// <summary>Rotates the rotor as Elevation rotor, inverting the direction for hinges and rotors not matching the designated main up vector.</summary>
            public void RotateElevation(float upperOffset, float maxSpeed, Vector3D Up)
            {
                bool inv = Rotor != null && Rotor.WorldMatrix.Up != Up || Rotor.BlockDefinition.SubtypeName.Contains("Hinge");
                int invFac = inv ? -1 : 1;
                SetRotorSpeedFromOffset(-upperOffset * invFac, 4, maxSpeed);
            }

            /// <summary>Shortcut for IMyEntity.GetPosition()</summary>
            public Vector3 GetPosition()
            {
                return Rotor.GetPosition();
            }

            #region MATH_HELL
            public bool IsPointInAzimuthRange(Vector3D point)
            {
                var upLimit = Rotor.UpperLimitDeg;
                var lowLimit = Rotor.LowerLimitDeg;
                var angle = GetAzimuthAngleTo(point);
                return angle <= upLimit && angle >= lowLimit;
            }

            public float GetAzimuthAngleTo(Vector3D point)
            {
                var forward = Rotor.WorldMatrix.Backward; // Don't question it
                var up = Rotor.WorldMatrix.Up;
                if (Rotor.BlockDefinition.SubtypeId.Contains("Hinge"))
                {
                    up = Rotor.WorldMatrix.Down;
                    forward = Rotor.WorldMatrix.Left;
                }
                var targetVec = point - GetPosition();
                var planeVec = Vector3D.ProjectOnPlane(ref targetVec, ref up);
               return MathHelper.ToDegrees(MyMath.AngleBetween(forward, planeVec));
            }

            public MatrixD GetAzimuthRotationMatrixTo(Vector3D point)
            {
                var angle = GetAzimuthAngleTo(point);
                return MatrixD.CreateFromAxisAngle(Rotor.WorldMatrix.Up, angle);
            }

            public bool IsPointInElevationRange(Vector3D point, MatrixD azimuthRotation)
            {
                var upLimit = Rotor.UpperLimitDeg;
                var lowLimit = Rotor.LowerLimitDeg;
                var angle = GetElevationAngleTo(point, azimuthRotation);
                return angle <= upLimit && angle >= lowLimit;
            }

            public float GetElevationAngleTo(Vector3D point, MatrixD azimuthRotation)
            {
                var matrix = Rotor.WorldMatrix;
                var newMatrix = MatrixD.Multiply(azimuthRotation, matrix);
                var forward = newMatrix.Backward;
                var up = newMatrix.Up;
                if (Rotor.BlockDefinition.SubtypeId.Contains("Hinge"))
                {
                    up = newMatrix.Down;
                    forward = newMatrix.Left;
                }
                var targetVec = point - GetPosition();
                var planeVec = Vector3D.ProjectOnPlane(ref targetVec, ref up);
                return MathHelper.ToDegrees(MyMath.AngleBetween(forward, planeVec));
            }
            #endregion
        }
    }
}
