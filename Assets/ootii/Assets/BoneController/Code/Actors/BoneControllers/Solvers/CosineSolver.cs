using System;
using System.Collections.Generic;
using UnityEngine;
using com.ootii.Base;
using com.ootii.Geometry;
using com.ootii.Helpers;
using com.ootii.Utilities;
using com.ootii.Utilities.Debug;

namespace com.ootii.Actors.BoneControllers
{
    /// <summary>
    /// This solver leverages the "law of cosines" to solve for an angle between
    /// two points. This works because we know the start point, end point, and bone lengths.
    /// </summary>
    public class CosineSolver : IKSolver
    {
        /// <summary>
        /// Core function that relies on the law of cosines in order to
        /// determine the angles between two bones.
        /// </summary>
        /// <param name="rState">State object containing information about what is to be solved and the results</param>
        public static void SolveIK(ref IKSolverState rState, float rBone2Extension = 0f)
        {
            if (rState.Bones == null || rState.Bones.Count != 2) { return; }

            // Extract out the data
            BoneControllerBone lBone1 = rState.Bones[0];
            BoneControllerBone lBone2 = rState.Bones[1];

            // Grab basic bone info. We need the end's bind rotation so that it will keep the cosine equations
            // on a single plane after limits are processed.
            float lBone1Length = Vector3.Distance(lBone1.Transform.position, lBone2.Transform.position);
            Vector3 lBone1Position = lBone1.Transform.position;
            Quaternion lBone1Rotation = lBone1.Transform.rotation;
            Vector3 lBone1BendAxis = rState.BoneBendAxes[0];

            float lBone2Length = lBone2.Length + rBone2Extension;
            Vector3 lBone2Position = lBone2.Transform.position;
            Quaternion lBone2Rotation = lBone2.Transform.rotation;
            Vector3 lBone2BendAxis = rState.BoneBendAxes[1];

            Vector3 lBone3Position = lBone2Position + (lBone2Rotation * lBone2.BindRotation * lBone2.ToBoneForward * (Vector3.forward * lBone2Length));

            // Check if our final position is too far. If so, we need to bring it in
            Vector3 lTargetPosition = rState.TargetPosition;
            float lBone1ToTargetLength = Vector3.Distance(lBone1Position, lTargetPosition);
            if (lBone1ToTargetLength > lBone1Length + lBone2Length)
            {
                // We remove a tiny bit of length so we never end up with a 
                // bone angle of 0. This allows us to account for the bend axis.
                lBone1ToTargetLength = lBone1Length + lBone2Length;// -0.0000f;

                Vector3 lDirection = (lTargetPosition - lBone1Position).normalized;
                lTargetPosition = lBone1Position + (lDirection * lBone1ToTargetLength);
            }

            // Grab the angle between the target vector and the mid bone. Then, create the final rotation vector for the first bone
            float lAngle = (-(lBone2Length * lBone2Length) + (lBone1Length * lBone1Length) + (lBone1ToTargetLength * lBone1ToTargetLength)) / (2f * lBone1Length * lBone1ToTargetLength);
            float lBone1Angle = Mathf.Acos(Mathf.Clamp(lAngle, -1f, 1f)) * Mathf.Rad2Deg;

            // The bind rotation in world coordinates
            Quaternion lBaseRootRotation = (rState.UseBindRotation ? lBone1.WorldBindRotation : lBone1.Transform.rotation * lBone1.ToBoneForward);

            // Grab the rotation that gets us from the base vector to the target vector. This is the hypotenuse.
            Quaternion lToTargetRotation = Quaternion.FromToRotation(lBaseRootRotation * Vector3.forward, lTargetPosition - lBone1Position);
            
            // Determine the axis we'll rotate the root bone around
            Vector3 lRootBendAxis = Vector3.zero;
            if (rState.UsePlaneNormal)
            {
                lRootBendAxis = Vector3Ext.PlaneNormal(lBone1Position, lBone2Position, lBone3Position);
            }
            else
            {
                lRootBendAxis = lToTargetRotation * lBaseRootRotation * lBone1BendAxis;
            }
            
            // Rotate from the base rotation to the target rotation and finally to the correct rotation (based on the angle)
            lBone1Rotation = Quaternion.AngleAxis(lBone1Angle, lRootBendAxis) * lToTargetRotation * lBaseRootRotation;

            // Now we can determine the position of the second bone
            lBone2Position = lBone1Position + (lBone1Rotation * (Vector3.forward * lBone1Length));

            // Want to ensure we don't end up with a '0' look direction. Otherwise, we'll get infinite errors.
            if (Vector3.SqrMagnitude(lTargetPosition - lBone2Position) > 0.001f)
            {
                // Grabbing the rotation of the second bone is easier since we just look at the target
                Vector3 lForward = lTargetPosition - lBone2Position;
                Vector3 lRight = lBone1Rotation * lBone2BendAxis;
                Vector3 lUp = Vector3.Cross(lForward, lRight).normalized;

                lBone2Rotation = Quaternion.LookRotation(lForward, lUp);
            }

            // Return the results
            rState.Rotations.Clear();
            rState.AddRotation(lBone1, lBone1Rotation);
            rState.AddRotation(lBone2, lBone2Rotation);

            // Set the position valudes (for debugging)
            rState.BonePositions.Clear();
            rState.BonePositions.Add(lBone1Position);
            rState.BonePositions.Add(lBone2Position);
            rState.BonePositions.Add(lBone2Position + (lBone2Rotation * (Vector3.forward * lBone2Length)));

            // Debug
            if (rState.IsDebugEnabled)
            {
                DebugDraw.DrawOctahedronOverlay(lBone1Position, Quaternion.identity, 0.03f, Color.red, 1f);
                DebugDraw.DrawOctahedronOverlay(lBone2Position, Quaternion.identity, 0.03f, Color.green, 1f);
                DebugDraw.DrawOctahedronOverlay(lBone3Position, Quaternion.identity, 0.03f, Color.blue, 1f);
                DebugDraw.DrawOctahedronOverlay(lTargetPosition, Quaternion.identity, 0.03f, Color.magenta, 1f);

                DebugDraw.DrawLineOverlay(lBone1Position, lBone1Position + (lBone1Rotation * (Vector3.forward * 0.5f)), 0.01f, Color.blue, 0.75f);
                DebugDraw.DrawLineOverlay(lBone1Position, lBone1Position + (lBone1Rotation * (Vector3.up * 0.5f)), 0.01f, Color.green, 0.75f);
                DebugDraw.DrawLineOverlay(lBone1Position, lBone1Position + (lBone1Rotation * (Vector3.right * 0.5f)), 0.01f, Color.red, 0.75f);

                DebugDraw.DrawLineOverlay(lBone2Position, lBone2Position + (lBone2Rotation * Vector3.forward), 0.02f, Color.blue, 0.5f);
                DebugDraw.DrawLineOverlay(lBone2Position, lBone2Position + (lBone2Rotation * Vector3.up), 0.02f, Color.green, 0.5f);
                DebugDraw.DrawLineOverlay(lBone2Position, lBone2Position + (lBone2Rotation * Vector3.right), 0.02f, Color.red, 0.5f);
            }
        }
    }
}
