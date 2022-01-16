using Sandbox.ModAPI.Ingame;
using System;
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
                } catch {}

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

            public void RotateElevation(float upperOffset, float maxSpeed, Vector3D Up)
            {
                bool inv = Rotor != null && Rotor.WorldMatrix.Up != Up || Rotor.BlockDefinition.SubtypeName.Contains("Hinge");
                int invFac = inv ? -1 : 1;
                SetRotorSpeedFromOffset(upperOffset * invFac, 4, maxSpeed);
            }
        }
    }
}
