using System;
using System.Collections.Generic;
using UnityEngine;
using com.ootii.Collections;
using com.ootii.Geometry;

namespace com.ootii.Actors.BoneControllers
{
    /// <summary>
    /// Holds information about the solution we want as well as
    /// result information.
    /// </summary>
    public struct IKSolverState
    {
        public Vector3 TargetPosition;

        public bool UsePlaneNormal;

        public bool UseBindRotation;

        public bool IsDebugEnabled;

        public List<BoneControllerBone> Bones;

        public List<float> BoneLengths;

        public List<Vector3> BonePositions;

        public List<Vector3> BoneBendAxes;

        public Dictionary<BoneControllerBone, Quaternion> Rotations;

        public Dictionary<BoneControllerBone, Quaternion> Swings;

        public Dictionary<BoneControllerBone, Quaternion> Twists;

        /// <summary>
        /// Adds a rotation to the specified bone
        /// </summary>
        /// <param name="rBone"></param>
        /// <param name="rRotation"></param>
        public void AddRotation(BoneControllerBone rBone, Quaternion rRotation)
        {
            Rotations.Add(rBone, rRotation);
        }

        /// <summary>
        /// Adds a rotation to the specified bone
        /// </summary>
        /// <param name="rBone"></param>
        /// <param name="rSwing"></param>
        /// <param name="rTwist"></param>
        public void AddRotation(BoneControllerBone rBone, Quaternion rSwing, Quaternion rTwist)
        {
            Rotations.Add(rBone, rSwing * rTwist);

            Swings.Add(rBone, rSwing);
            Twists.Add(rBone, rTwist);
        }

        // ******************************** OBJECT POOL ********************************

        /// <summary>
        /// Allows us to reuse objects without having to reallocate them over and over
        /// </summary>
        private static ObjectPool<IKSolverState> sPool = new ObjectPool<IKSolverState>(10, 5);

        /// <summary>
        /// Returns the number of items allocated
        /// </summary>
        /// <value>The allocated.</value>
        public static int Length
        {
            get { return sPool.Length; }
        }

        /// <summary>
        /// Pulls an object from the pool.
        /// </summary>
        /// <returns></returns>
        public static IKSolverState Allocate()
        {
            // Grab the next available object
            IKSolverState lInstance = sPool.Allocate();

            // Initialize
            lInstance.TargetPosition = Vector3.zero;
            lInstance.UsePlaneNormal = false;
            lInstance.UseBindRotation = false;
            lInstance.IsDebugEnabled = false;

            if (lInstance.Bones == null) { lInstance.Bones = new List<BoneControllerBone>(); }
            if (lInstance.BoneLengths == null) { lInstance.BoneLengths = new List<float>(); }
            if (lInstance.BonePositions == null) { lInstance.BonePositions = new List<Vector3>(); }
            if (lInstance.BoneBendAxes == null) { lInstance.BoneBendAxes = new List<Vector3>(); }
            if (lInstance.Rotations == null) { lInstance.Rotations = new Dictionary<BoneControllerBone, Quaternion>(); }
            if (lInstance.Swings == null) { lInstance.Swings = new Dictionary<BoneControllerBone, Quaternion>(); }
            if (lInstance.Twists == null) { lInstance.Twists = new Dictionary<BoneControllerBone, Quaternion>(); }

            return lInstance;
        }

        /// <summary>
        /// Returns an element back to the pool.
        /// </summary>
        /// <param name="rEdge"></param>
        public static void Release(IKSolverState rInstance)
        {
            // Ensure we have a valid object
            if (rInstance.BoneLengths == null) { return; }

            // Clear the values
            rInstance.TargetPosition = Vector3.zero;
            rInstance.UsePlaneNormal = false;
            rInstance.UseBindRotation = false;
            rInstance.IsDebugEnabled = false;

            if (rInstance.Bones == null) { rInstance.Bones = new List<BoneControllerBone>(); }
            rInstance.Bones.Clear();

            if (rInstance.BoneLengths == null) { rInstance.BoneLengths = new List<float>(); }
            rInstance.BoneLengths.Clear();

            if (rInstance.BonePositions == null) { rInstance.BonePositions = new List<Vector3>(); }
            rInstance.BonePositions.Clear();

            if (rInstance.BoneBendAxes == null) { rInstance.BoneBendAxes = new List<Vector3>(); }
            rInstance.BoneBendAxes.Clear();

            if (rInstance.Rotations == null) { rInstance.Rotations = new Dictionary<BoneControllerBone, Quaternion>(); }
            rInstance.Rotations.Clear();

            if (rInstance.Swings == null) { rInstance.Swings = new Dictionary<BoneControllerBone, Quaternion>(); }
            rInstance.Swings.Clear();

            if (rInstance.Twists == null) { rInstance.Twists = new Dictionary<BoneControllerBone, Quaternion>(); }
            rInstance.Twists.Clear();

            // Send it back to the pool
            sPool.Release(rInstance);
        }
    }
}
