using System;
using System.Collections.Generic;
using UnityEngine;
using com.ootii.Base;
using com.ootii.Geometry;
using com.ootii.Utilities;
using com.ootii.Utilities.Debug;

namespace com.ootii.Actors.BoneControllers
{
    public class FABRIKSolver : IKSolver
    {
        private static List<Vector3> lBonePositions = new List<Vector3>();

        private static List<Quaternion> lBoneRotations = new List<Quaternion>();

        /// <summary>
        /// Determine the final position and rotation of the bones in a chain. The
        /// first bone will remain fixed, while the last bone will attempt to reach the target
        /// position.
        /// </summary>
        /// <param name="rBoneChainRoot">List of BoneControllerBones we'll be solving for.</param>
        /// <param name="rBoneChainEffector">Vector3 the last bone in the chain is attempting to reach.</param>
        /// <remarks>Note that the last bone (effector) will have a bone length of 0. This joint is the effector.</remarks>
        public static void SolveIK(ref IKSolverState rState)
        {
            lBonePositions.Clear();
            lBoneRotations.Clear();

            BoneControllerBone lBoneChainRoot = rState.Bones[0];
            BoneControllerBone lBoneChainEnd = rState.Bones[rState.Bones.Count - 1];

            // Positions of each bone. This allows us to iterate the positions
            //List<Vector3> lBonePositions = rState.BonePositions;

            // Rotations of each bone. This allows us to iterate the rotations
            //List<Quaternion> lBoneRotations = rState.BoneRotations;

            Vector3 lBoneForward = lBoneChainRoot.BoneForward;

            Quaternion lToBoneForward = lBoneChainRoot.ToBoneForward;

            // Build the chain that we'll be processing
            List<BoneControllerBone> lBones = new List<BoneControllerBone>();

            // Store the root start position
            Vector3 lRootPosition = lBoneChainRoot.Transform.position;

            // Add the end point of the last bone in our chain
            // as our last point. We do this so we rotate this last bone correctly
            lBones.Add(null);
            lBonePositions.Add(lBoneChainEnd.Transform.position + (lBoneChainEnd.Transform.rotation * (lBoneForward * lBoneChainEnd.Length)));

            // Insert each ancestor
            BoneControllerBone lParent = lBoneChainEnd;
            while (lParent != null)
            {
                lBones.Insert(0, lParent);
                lBonePositions.Insert(0, lParent.Transform.position);

                lParent = (lParent == lBoneChainRoot ? null : lParent = lParent.Parent);
            }

            // Since a bone can have multiple children, we want the length that
            // follows this chain. So, we'll work backwards.
            float lTotalLength = 0f;
            List<float> lBoneLengths = new List<float>();
            for (int i = 0; i < lBonePositions.Count - 2; i++)
            {
                float lLength = Vector3.Distance(lBonePositions[i], lBonePositions[i + 1]);

                lTotalLength += lLength;
                lBoneLengths.Add(lLength);
            }

            // Add the last lengths since we can't determine them. The second
            // to last one is the end point of our chain
            lTotalLength += lBoneChainEnd.Length;

            lBoneLengths.Add(lBoneChainEnd.Length);
            lBoneLengths.Add(0);

            //// We need to determine if we can even reach the target. If not, we'll define
            //// a target we can reach
            //if (lTotalLength > Vector3.Distance(rBoneChainRoot.Transform.position, rTargetPosition))
            //{
            //    Vector3 lDirection = (rTargetPosition - rBoneChainRoot.Transform.position).normalized;
            //    rTargetPosition = rBoneChainRoot.Transform.position + (lDirection * lTotalLength);
            //}

            // Perform the solution
            bool lIterate = true;
            int lIterationCount = 0;

            while (lIterate && lIterationCount < BoneController.MaxIterations)
            {
                lIterationCount++;

                // First, reposition from the end and iterate backwards. Grab the
                // line the new position should be on and based on the bone length,
                // position it. We don't need to change the position of the first
                // bone since it needs to be fixed as the root.
                lBonePositions[lBonePositions.Count - 1] = rState.TargetPosition;
                for (int i = lBonePositions.Count - 2; i >= 0; i--)
                {
                    Vector3 lDirection = lBonePositions[i + 1].DirectionTo(lBonePositions[i]);
                    lBonePositions[i] = lBonePositions[i + 1] + (lDirection * lBoneLengths[i]);
                }

                // Second, reposition the start and iterate forward. Grab the
                // line the new position should be on and based on the bone length,
                // position it.
                lBonePositions[0] = lRootPosition;
                for (int i = 1; i < lBonePositions.Count; i++)
                {
                    Vector3 lDirection = lBonePositions[i - 1].DirectionTo(lBonePositions[i]);
                    lBonePositions[i] = lBonePositions[i - 1] + (lDirection * lBoneLengths[i - 1]);
                }

                // Enforce limits
                lBoneRotations.Clear();
                for (int i = 0; i < lBonePositions.Count - 1; i++)
                {
                    Vector3 lNextPosition = lBonePositions[i + 1];

                    Vector3 lDirectionForward = (lNextPosition - lBones[i]._Transform.position).normalized;

                    // For the arm, the forward direction points down (as the rotation axis of the elbow). So, that's what we'll get.
                    // For the arm, we use this directly as it's the "up" vector for the "look" rotation
                    Vector3 lUpAxis = (lBones[i]._Joint == null ? Vector3.forward : lBones[i]._Joint._UpAxis);

                    // Using the bind pose "rotation axis", grab an "up" direction for our look rotation
                    Vector3 lDirectionUp = lBones[i].WorldBindRotation * lUpAxis;
                    //lDirectionUp = Quaternion.AngleAxis(90, lDirectionUp) * lDirectionForward;

                    // Create the rotation based on typical forward and up. Then, we need to point from
                    // the typical forward direction to the 'BoneForward'
                    Quaternion lWorldRotation = Quaternion.LookRotation(lDirectionForward, lDirectionUp);
                    lWorldRotation = lWorldRotation * lToBoneForward;

                    // Convert the world rotation we want to a rotation that is relative to the bone
                    Quaternion lLocalRotation = lBones[i].TransformWorldRotationToLocalRotation(lWorldRotation);

                    if (lBones[i]._Joint != null)
                    {
                        // Extract out the limitations so we can adjust the reach
                        Quaternion lSwing = Quaternion.identity;
                        Quaternion lTwist = Quaternion.identity;
                        lLocalRotation.DecomposeSwingTwist(lBoneForward, ref lSwing, ref lTwist);

                        lBones[i]._Joint.ApplyLimits(ref lSwing, ref lTwist);
                        lLocalRotation = lSwing * lTwist;
                    }

                    // Store the resulting local rotations
                    lBoneRotations.Add(lLocalRotation);
                }

                // Determine the new positions based on the final rotations. This is
                // important since the rotations may have been limited
                Vector3 lParentPosition = lBones[0]._Transform.position;
                Quaternion lParentRotation = (lBones[0]._Transform.parent != null ? lBones[0]._Transform.parent.rotation : Quaternion.identity);
                for (int i = 1; i < lBonePositions.Count; i++)
                {
                    int iMinus1 = i - 1;

                    lParentRotation = lParentRotation * lBones[iMinus1].BindRotation * lBoneRotations[iMinus1];
                    lBonePositions[i] = lParentPosition + (lParentRotation * (lBoneForward * lBoneLengths[iMinus1]));

                    lParentPosition = lBonePositions[i];
                }

                // If our last position is close to our target, we can stop
                float lDistance = Vector3.Distance(lBonePositions[lBonePositions.Count - 1], rState.TargetPosition);
                if (lDistance < 0.01f) { lIterate = false; }
            }

            // We'll report the new rotations that we calculated earlier
            rState.Swings.Clear();
            rState.Twists.Clear();
            rState.Rotations.Clear();
            for (int i = 0; i < lBoneRotations.Count; i++)
            {
                Quaternion lSwing = Quaternion.identity;
                Quaternion lTwist = Quaternion.identity;
                lBoneRotations[i].DecomposeSwingTwist(lBoneForward, ref lSwing, ref lTwist);

                rState.AddRotation(lBones[i], lSwing, lTwist);
            }

            // The final positions we'll be moving to
            //return lBonePositions;
        }

        ///// <summary>
        ///// Determine the final position and rotation of the bones in a chain. The
        ///// first bone will remain fixed, while the last bone will attempt to reach the target
        ///// position.
        ///// </summary>
        ///// <param name="rBoneChainRoot">List of BoneControllerBones we'll be solving for.</param>
        ///// <param name="rBoneChainEffector">Vector3 the last bone in the chain is attempting to reach.</param>
        ///// <remarks>Note that the last bone (effector) will have a bone length of 0. This joint is the effector.</remarks>
        //public override List<Vector3> SolveIK(BoneControllerBone rBoneChainRoot, BoneControllerBone rBoneChainEnd, Vector3 rTargetPosition, ref Dictionary<BoneControllerBone, Quaternion> rResult)
        //{
        //    // Positions of each bone. This allows us to iterate the positions
        //    lBonePositions.Clear();

        //    // Rotations of each bone. This allows us to iterate the rotations
        //    lBoneRotations.Clear();

        //    Vector3 lBoneForward = rBoneChainRoot.BoneForward;

        //    Quaternion lToBoneForward = rBoneChainRoot.Skeleton.ToBoneForward;

        //    // Build the chain that we'll be processing
        //    List<BoneControllerBone> lBones = new List<BoneControllerBone>();

        //    // Store the root start position
        //    Vector3 lRootPosition = rBoneChainRoot.Transform.position;

        //    // Add the end point of the last bone in our chain
        //    // as our last point. We do this so we rotate this last bone correctly
        //    lBones.Add(null);
        //    lBonePositions.Add(rBoneChainEnd.Transform.position + (rBoneChainEnd.Transform.rotation * (lBoneForward * rBoneChainEnd.Length)));

        //    // Insert each ancestor
        //    BoneControllerBone lParent = rBoneChainEnd;
        //    while (lParent != null)
        //    {
        //        lBones.Insert(0, lParent);
        //        lBonePositions.Insert(0, lParent.Transform.position);

        //        lParent = (lParent == rBoneChainRoot ? null : lParent = lParent.Parent);
        //    }

        //    // Since a bone can have multiple children, we want the length that
        //    // follows this chain. So, we'll work backwards.
        //    float lTotalLength = 0f;
        //    List<float> lBoneLengths = new List<float>();
        //    for (int i = 0; i < lBonePositions.Count - 2; i++)
        //    {
        //        float lLength = Vector3.Distance(lBonePositions[i], lBonePositions[i + 1]);

        //        lTotalLength += lLength;
        //        lBoneLengths.Add(lLength);
        //    }

        //    // Add the last lengths since we can't determine them. The second
        //    // to last one is the end point of our chain
        //    lTotalLength += rBoneChainEnd.Length;

        //    lBoneLengths.Add(rBoneChainEnd.Length);
        //    lBoneLengths.Add(0);

        //    //// We need to determine if we can even reach the target. If not, we'll define
        //    //// a target we can reach
        //    //if (lTotalLength > Vector3.Distance(rBoneChainRoot.Transform.position, rTargetPosition))
        //    //{
        //    //    Vector3 lDirection = (rTargetPosition - rBoneChainRoot.Transform.position).normalized;
        //    //    rTargetPosition = rBoneChainRoot.Transform.position + (lDirection * lTotalLength);
        //    //}

        //    // Perform the solution
        //    bool lIterate = true;
        //    int lIterationCount = 0;

        //    while (lIterate && lIterationCount < BoneControllerSkeleton .MaxIterations)
        //    {
        //        lIterationCount++;

        //        // First, reposition from the end and iterate backwards. Grab the
        //        // line the new position should be on and based on the bone length,
        //        // position it. We don't need to change the position of the first
        //        // bone since it needs to be fixed as the root.
        //        lBonePositions[lBonePositions.Count - 1] = rTargetPosition;
        //        for (int i = lBonePositions.Count - 2; i >= 0; i--)
        //        {
        //            Vector3 lDirection = lBonePositions[i + 1].DirectionTo(lBonePositions[i]);
        //            lBonePositions[i] = lBonePositions[i + 1] + (lDirection * lBoneLengths[i]);
        //        }

        //        // Second, reposition the start and iterate forward. Grab the
        //        // line the new position should be on and based on the bone length,
        //        // position it.
        //        lBonePositions[0] = lRootPosition;
        //        for (int i = 1; i < lBonePositions.Count; i++)
        //        {
        //            Vector3 lDirection = lBonePositions[i - 1].DirectionTo(lBonePositions[i]);
        //            lBonePositions[i] = lBonePositions[i - 1] + (lDirection * lBoneLengths[i - 1]);
        //        }

        //        // Enforce limits
        //        lBoneRotations.Clear();
        //        for (int i = 0; i < lBonePositions.Count - 1; i++)
        //        {
        //            Vector3 lNextPosition = lBonePositions[i + 1];

        //            Vector3 lDirectionForward = (lNextPosition - lBones[i]._Transform.position).normalized;

        //            // For the arm, the forward direction points down (as the rotation axis of the elbow). So, that's what we'll get.
        //            // For the arm, we use this directly as it's the "up" vector for the "look" rotation
        //            Vector3 lUpAxis = (lBones[i]._Joint == null ? Vector3.forward : lBones[i]._Joint._UpAxis);

        //            // Using the bind pose "rotation axis", grab an "up" direction for our look rotation
        //            Vector3 lDirectionUp = lBones[i].WorldBindRotation * lUpAxis;
        //            //lDirectionUp = Quaternion.AngleAxis(90, lDirectionUp) * lDirectionForward;

        //            // Create the rotation based on typical forward and up. Then, we need to point from
        //            // the typical forward direction to the 'BoneForward'
        //            Quaternion lWorldRotation = Quaternion.LookRotation(lDirectionForward, lDirectionUp);
        //            lWorldRotation = lWorldRotation * lToBoneForward;

        //            // Convert the world rotation we want to a rotation that is relative to the bone
        //            Quaternion lLocalRotation = lBones[i].TransformWorldRotationToLocalRotation(lWorldRotation);

        //            if (lBones[i]._Joint != null)
        //            {
        //                // Extract out the limitations so we can adjust the reach
        //                Quaternion lSwing = Quaternion.identity;
        //                Quaternion lTwist = Quaternion.identity;
        //                lLocalRotation.DecomposeSwingTwist(lBoneForward, ref lSwing, ref lTwist);

        //                lBones[i]._Joint.ApplyLimits(ref lSwing, ref lTwist);
        //                lLocalRotation = lSwing * lTwist;
        //            }

        //            // Store the resulting local rotations
        //            lBoneRotations.Add(lLocalRotation);
        //        }

        //        // Determine the new positions based on the final rotations. This is
        //        // important since the rotations may have been limited
        //        Vector3 lParentPosition = lBones[0]._Transform.position;
        //        Quaternion lParentRotation = (lBones[0]._Transform.parent != null ? lBones[0]._Transform.parent.rotation : Quaternion.identity);
        //        for (int i = 1; i < lBonePositions.Count; i++)
        //        {
        //            int iMinus1 = i - 1;

        //            lParentRotation = lParentRotation * lBones[iMinus1].BindRotation * lBoneRotations[iMinus1];
        //            lBonePositions[i] = lParentPosition + (lParentRotation * (lBoneForward * lBoneLengths[iMinus1]));

        //            lParentPosition = lBonePositions[i];
        //        }

        //        // If our last position is close to our target, we can stop
        //        float lDistance = Vector3.Distance(lBonePositions[lBonePositions.Count - 1], rTargetPosition);
        //        if (lDistance < 0.01f) { lIterate = false; }
        //    }

        //    // We'll report the new rotations that we calculated earlier
        //    rResult.Clear();
        //    for (int i = 0; i < lBoneRotations.Count; i++)
        //    {
        //        rResult.Add(lBones[i], lBoneRotations[i]);
        //    }

        //    // The final positions we'll be moving to
        //    return lBonePositions;
        //}

        ///// <summary>
        ///// Renders out the bones that are being moved
        ///// </summary>
        //public override void Debug()
        //{
        //    Color lColor = new Color(0.94118f, 0.39608f, 0.13333f, 1f);

        //    for (int i = 0; i < lBonePositions.Count; i++)
        //    {
        //        DebugDraw.DrawOctahedronOverlay(lBonePositions[i], Quaternion.identity, 0.02f, lColor, 1f);

        //        if (i > 0)
        //        {
        //            DebugDraw.DrawLineOverlay(lBonePositions[i - 1], lBonePositions[i], 0.005f, lColor, 0.5f);
        //        }
        //    }
        //}
    }
}
