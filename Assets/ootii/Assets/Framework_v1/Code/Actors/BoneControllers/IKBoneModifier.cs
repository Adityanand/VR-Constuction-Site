using System;
using UnityEngine;
using com.ootii.Collections;

namespace com.ootii.Actors.BoneControllers
{
    /// <summary>
    /// Modifications to the BoneControllerBones don't typically happen immediately instead, they
    /// happen after all the modifications have been registered. Then, they are weighted
    /// and finally applied to the bones at once. They are also applied in order from
    /// parent to child.
    /// </summary>
    [Serializable]
    public class IKBoneModifier
    {
        /// <summary>
        /// Determines if the values of the modifier have changed
        /// </summary>
        public bool IsDirty;

        /// <summary>
        /// Defines the type of modifier and how we're to process it.
        /// </summary>
        public int Type;

        /// <summary>
        /// Stores the swing rotation to be applied
        /// </summary>
        public Quaternion Swing;

        /// <summary>
        /// Stores the twist rotation to be applied
        /// </summary>
        public Quaternion Twist;

        /// <summary>
        /// Store the position used to calculate rotation
        /// </summary>
        public Vector3 Position;

        /// <summary>
        /// Store the up vector to use when calculating rotation
        /// </summary>
        public Vector3 Up;

        /// <summary>
        /// Relative strength (0 to 1) of the modifier compared to other modifiers
        /// </summary>
        public float Weight;

        /// <summary>
        /// Default constructor
        /// </summary>
        public IKBoneModifier()
        {
            Type = EnumIKBoneModifierType.LOCAL_ROTATION;
            Swing = Quaternion.identity;
            Twist = Quaternion.identity;
            Position = Vector3.zero;
            Up = Vector3.zero;
            Weight = 1;
            IsDirty = false;
        }

        /// <summary>
        /// Default constructor
        /// </summary>
        public IKBoneModifier(int rType)
        {
            Type = rType;
            Swing = Quaternion.identity;
            Twist = Quaternion.identity;
            Position = Vector3.zero;
            Up = Vector3.zero;
            Weight = 1;
            IsDirty = false;
        }

        // ******************************** OBJECT POOL ********************************

        /// <summary>
        /// Allows us to reuse objects without having to reallocate them over and over
        /// </summary>
        private static ObjectPool<IKBoneModifier> sPool = new ObjectPool<IKBoneModifier>(20, 5);

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
        public static IKBoneModifier Allocate()
        {
            // Grab the next available object
            IKBoneModifier lInstance = sPool.Allocate();

            // Initialize
            lInstance.Type = EnumIKBoneModifierType.LOCAL_ROTATION;
            lInstance.Swing = Quaternion.identity;
            lInstance.Twist = Quaternion.identity;
            lInstance.Position = Vector3.zero;
            lInstance.Up = Vector3.zero;
            lInstance.Weight = 1f;
            lInstance.IsDirty = false;

            return lInstance;
        }

        /// <summary>
        /// Pulls an object from the pool.
        /// </summary>
        /// <returns></returns>
        public static IKBoneModifier Allocate(int rType, Quaternion rSwing, Quaternion rTwist, float rWeight)
        {
            // Grab the next available object
            IKBoneModifier lInstance = sPool.Allocate();

            // Initialize
            lInstance.Type = rType;
            lInstance.Swing = rSwing;
            lInstance.Twist = rTwist;
            lInstance.Position = Vector3.zero;
            lInstance.Up = Vector3.zero;
            lInstance.Weight = rWeight;
            lInstance.IsDirty = false;

            return lInstance;
        }

        /// <summary>
        /// Pulls an object from the pool.
        /// </summary>
        /// <returns></returns>
        public static IKBoneModifier Allocate(int rType, Vector3 rPosition, Vector3 rUp, float rWeight)
        {
            // Grab the next available object
            IKBoneModifier lInstance = sPool.Allocate();

            // Initialize
            lInstance.Type = rType;
            lInstance.Swing = Quaternion.identity;
            lInstance.Twist = Quaternion.identity;
            lInstance.Position = rPosition;
            lInstance.Up = rUp;
            lInstance.Weight = rWeight;
            lInstance.IsDirty = false;

            return lInstance;
        }

        /// <summary>
        /// Returns an element back to the pool.
        /// </summary>
        /// <param name="rEdge"></param>
        public static void Release(IKBoneModifier rInstance)
        {
            sPool.Release(rInstance);
        }
    }
}
