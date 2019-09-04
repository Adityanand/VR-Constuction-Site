using System;
using System.Collections.Generic;
using UnityEngine;
using com.ootii.Geometry;
using com.ootii.Helpers;
using com.ootii.Utilities;
using com.ootii.Utilities.Debug;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace com.ootii.Actors.BoneControllers
{
    /// <summary>
    /// Controls the spine rotation of the character so it swings at the target
    /// </summary>
    [Serializable]
    [IKName("Swing At Motor")]
    [IKDescription("This motor will adjust the forward direction and face of a character to swing and attack a target")]
    public class SwingAtMotor : LookAtMotor
    {
        /// <summary>
        /// Default constructor
        /// </summary>
        public SwingAtMotor() 
            : base()
        {
        }

        /// <summary>
        /// Skeleton constructor
        /// </summary>
        /// <param name="rSkeleton">Skeleton the motor is driving</param>
        public SwingAtMotor(BoneController rSkeleton)
            : base(rSkeleton)
        {
        }

        /// <summary>
        /// Automatically loads bones for the developer. This is typically done by using things like
        /// the HumanBodyBones.
        /// </summary>
        /// <param name="rStyle">String that can be used to define how to load bones</param>
        public override void AutoLoadBones(string rStyle)
        {
            rStyle = rStyle.ToLower();

            mBones.Clear();
            _BoneInfo.Clear();

            AddBone(Skeleton.GetBone(HumanBodyBones.Head) as BoneControllerBone, false);
            
            AddBone(Skeleton.GetBone(HumanBodyBones.Neck) as BoneControllerBone, false);
            if (_BoneInfo.Count > 1) { _BoneInfo[1].Weight = 0.85f; }

            AddBone(Skeleton.GetBone(HumanBodyBones.Chest) as BoneControllerBone, false);
            if (_BoneInfo.Count > 2) { _BoneInfo[2].Weight = 0.65f; }

            AddBone(Skeleton.GetBone(HumanBodyBones.Spine) as BoneControllerBone, false);
            if (_BoneInfo.Count > 3) { _BoneInfo[3].Weight = 0.4f; }

            // Reset the invalidation flag
            mIsValid = true;
        }
    }
}
