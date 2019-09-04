using System;
using UnityEngine;

namespace com.ootii.Actors.BoneControllers
{
    /// <summary>
    /// Simple motor that forces the skeleton into it's
    /// binding pose.
    /// </summary>
    [Serializable]
    [IKName("Bind Pose Motor")]
    public class BindPoseMotor : BoneControllerMotor
    {
        /// <summary>
        /// Default constructor
        /// </summary>
        public BindPoseMotor() 
            : base()
        {
        }

        /// <summary>
        /// Skeleton constructor
        /// </summary>
        /// <param name="rSkeleton">Skeleton the motor is driving</param>
        public BindPoseMotor(BoneController rSkeleton)
            : base(rSkeleton)
        {
        }

        /// <summary>
        /// Process the motor each frame so that it can update the bone rotations.
        /// This is the function that should be overridden in each motor
        /// </summary>
        /// <param name="rDeltaTime">Delta time to use for the update</param>
        /// <param name="rUpdate">Determines if it is officially time to do the update</param>
        protected override void Update(float rDeltaTime, bool rUpdate)
        {
            for (int i = 0; i < mSkeleton.Bones.Count; i++)
            {
                mSkeleton.Bones[i].SetLocalRotation(Quaternion.identity, _BoneWeight);
            }
        }
    }
}
