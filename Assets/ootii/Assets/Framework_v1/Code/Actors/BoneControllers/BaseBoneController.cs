using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using com.ootii.Base;

namespace com.ootii.Actors.BoneControllers
{
    /// <summary>
    /// Base class for all bone controllers. Provides functions for
    /// dealing with bones and motors.
    /// </summary>
    public abstract class BaseBoneController : BaseMonoObject
    {
        /// <summary>
        /// Provides access to a list of transforms that represents the
        /// bones in the skeleton. This field should never be updated except
        /// by the skeleton itself. However, it may be useful when ignoring
        /// raycast results and such.
        /// </summary>
        public List<Transform> BoneTransforms;

        /// <summary>
        /// Recursively searches for a bone given the name and returns it if found
        /// </summary>
        /// <param name="rParent">Parent to search through</param>
        /// <param name="rBoneName">Bone to find</param>
        /// <returns>Transform of the bone or null</returns>
        public abstract IKBone GetBone(string rBoneName);

        /// <summary>
        /// Recursively searches for a bone given the name and returns it if found
        /// </summary>
        /// <param name="rParent">Parent to search through</param>
        /// <param name="rBone">Bone transform to find</param>
        /// <returns>Transform of the bone or null</returns>
        public abstract IKBone GetBone(Transform rBone);

        /// <summary>
        /// Recursively searches for a bone given the name and returns it if found
        /// </summary>
        /// <param name="rParent">Parent to search through</param>
        /// <param name="rBoneName">Bone to find</param>
        /// <returns>Transform of the bone or null</returns>
        public abstract IKBone GetBone(HumanBodyBones rBone);

        /// <summary>
        /// Tests if the point is contained by any of the bones' collider
        /// </summary>
        /// <param name="rPoint"></param>
        /// <returns></returns>
        public abstract IKBone TestPointCollision(Vector3 rPoint);

        /// <summary>
        /// Tests if the ray collides with the any of the bones' collider and if so, it returns the bone and collision point
        /// </summary>
        /// <param name="rStart"></param>
        /// <param name="rDirection"></param>
        /// <param name="rRange"></param>
        /// <param name="rHitPoint"></param>
        /// <returns></returns>
        public abstract bool TestRayCollision(Vector3 rStart, Vector3 rDirection, float rRange, out IKBone rHitBone, out Vector3 rHitPoint);

        /// <summary>
        /// Clears the position and rotations on all the bones, basically putting it
        /// back to the bind pose.
        /// </summary>
        public abstract void ResetBindPose();

        /// <summary>
        /// Retrieves the motor based on the motor's name. It will return the first
        /// motor matching the specified name.
        /// </summary>
        /// <param name="rName"></param>
        /// <returns></returns>
        public abstract IKMotor GetMotor(string rName);

        /// <summary>
        /// Retrieves the motor based on the motor's type. It will return the first
        /// motor of the specified type.
        /// </summary>
        /// <param name="rType"></param>
        /// <returns></returns>
        public abstract IKMotor GetMotor(Type rType);

        /// <summary>
        /// Retrieves the motor based on the motor's type. It will return the first
        /// motor of the specified type.
        /// </summary>
        /// <returns></returns>
        public abstract T GetMotor<T>() where T : IKMotor;

        /// <summary>
        /// Retrieves the motor based on the motor's type. It will return the first
        /// motor of the specified type.
        /// </summary>
        /// <param name="rName"></param>
        /// <returns></returns>
        public abstract T GetMotor<T>(string rName) where T : IKMotor;

        /// <summary>
        /// Enables and disables motors of the specified type
        /// </summary>
        /// <param name="rEnable"></param>
        /// <returns></returns>
        public abstract void EnableMotors<T>(bool rEnable) where T : IKMotor;

        // ------------------------------------------ STATIC FUNCTIONS ------------------------------------------

        /// <summary>
        /// Support function to normalize the bone names
        /// </summary>
        /// <param name="rBoneName">Original bone name</param>
        /// <returns>Cleaned name</returns>
        public static string CleanBoneName(string rBoneName)
        {
            string lNewName = rBoneName;

            // Handle the case where the bone name is nested in a namespace
            int lIndex = rBoneName.IndexOf(':');
            if (lIndex >= 0)
            {
                lNewName = rBoneName.Substring(lIndex + 1);
            }

            // Add spaces between camel case
            lNewName = Regex.Replace(Regex.Replace(lNewName, @"(\P{Ll})(\P{Ll}\p{Ll})", "$1 $2"), @"(\p{Ll})(\P{Ll})", "$1 $2");

            // Return the new string
            return lNewName;
        }
    }
}
