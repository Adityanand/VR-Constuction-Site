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
    /// Rotates a bone over time
    /// </summary>
    [Serializable]
    [IKName("Rotation Motor")]
    [IKDescription("Rotates a bones over time.")]
    public class RotationMotor : BoneControllerMotor
    {
        /// <summary>
        /// Extra information in order to help the management of the bones
        /// </summary>
        public List<RotationBone> _BoneInfo = new List<RotationBone>();

        /// <summary>
        /// Track if the motor has initialized or not
        /// </summary>
        protected bool mIsInitialized = false;

        /// <summary>
        /// Default constructor
        /// </summary>
        public RotationMotor()
            : base()
        {
        }

        /// <summary>
        /// Skeleton constructor
        /// </summary>
        /// <param name="rSkeleton">Skeleton the motor is driving</param>
        public RotationMotor(BoneController rSkeleton)
            : base(rSkeleton)
        {
        }

        /// <summary>
        /// Clears all the bones from the list
        /// </summary>
        public override void ClearBones()
        {
            mBones.Clear();
            _BoneInfo.Clear();
        }

        /// <summary>
        /// Process the motor each frame so that it can update the bone rotations.
        /// This is the function that should be overridden in each motor
        /// </summary>
        /// <param name="rDeltaTime">Delta time to use for the update</param>
        /// <param name="rUpdate">Determines if it is officially time to do the update</param>
        protected override void Update(float rDeltaTime, bool rUpdate)
        {
            if (mBones.Count == 0) { return; }

            // Process the motor info at start up
            if (!mIsInitialized)
            {
                // Ensure we have the correct amount of bone infos... we should
                while (_BoneInfo.Count < mBones.Count)
                {
                    RotationBone lBoneInfo = new RotationBone();
                    _BoneInfo.Add(lBoneInfo);
                }

                // Initialize the euler angles that are our base
                for (int i = 0; i < mBones.Count; i++)
                {
                    _BoneInfo[i].Euler = Vector3.zero;
                    _BoneInfo[i].BaseRotation = mBones[i]._Transform.rotation;

                    //_BoneInfo[i].Euler = mBones[i]._Transform.rotation.eulerAngles;
                }

                // Flag that we've initialized
                mIsInitialized = true;
            }

            // If it's time to update, determine the positions we need to be
            // at and lerp towards them.
            if (rUpdate)
            {
                // Process each bone
                for (int i = 0; i < mBones.Count; i++)
                {
                    BoneControllerBone lBone = mBones[i];
                    RotationBone lBoneInfo = _BoneInfo[i];

                    // The current rotation we will lerp from. We remove the trailing rotation offset because we'll add it later
                    Quaternion lCurrentRotation = lBone.Transform.rotation * lBone.ToBoneForward;

                    // Rotation we're moving towards
                    Quaternion lTargetRotation = lBone._Transform.rotation;

                    // Speed this frame
                    Vector3 lRotationSpeed = lBoneInfo.RotationSpeed * rDeltaTime;

                    // Update the angles we're rotating to
                    lBoneInfo.Euler += lRotationSpeed;

                    // Rotate based on the axis
                    if (lBoneInfo.RotationAxis == EnumIKBoneRotationAxis.BONE)
                    {
                        lTargetRotation = _BoneInfo[i].BaseRotation * Quaternion.Euler(lBoneInfo.Euler);
                    }
                    else if (lBoneInfo.RotationAxis == EnumIKBoneRotationAxis.MODEL)
                    {
                        lTargetRotation = _BoneInfo[i].BaseRotation * Quaternion.Euler(lBoneInfo.Euler) * lBone._ToBoneForward;
                    }
                    else
                    {
                        lTargetRotation = lTargetRotation * lBone._ToBoneForward;
                    }

                    // Rotation as determined by the target
                    lBoneInfo.RotationTarget = Quaternion.Lerp(lCurrentRotation, lTargetRotation, _Weight * lBoneInfo.Weight);

                    // Slowly move towards the rotation we determined
                    lBoneInfo.Rotation = Quaternion.Lerp(lBoneInfo.Rotation, lBoneInfo.RotationTarget, (_IsFixedUpdateEnabled && !mIsFirstUpdate ? lBoneInfo.RotationLerp : 1f));

                    // Set the world rotation
                    lBone.SetWorldRotation(lBoneInfo.Rotation, Quaternion.identity, _BoneWeight);
                }
            }
            // If it's not on a consistant update, we just want to reset the
            // last rotations that we found.
            else
            {
                for (int i = 0; i < mBones.Count; i++)
                {
                    BoneControllerBone lBone = mBones[i];
                    if (lBone == null) { continue; }

                    lBone.SetWorldRotation(_BoneInfo[i].Rotation, Quaternion.identity, _BoneWeight);
                }
            }
        }

        // **************************************************************************************************
        // Following properties and function only valid while editing
        // **************************************************************************************************

        /// <summary>
        /// Render a unique inspector
        /// </summary>
        /// <returns></returns>
        public override bool OnInspectorGUI(List<BoneControllerBone> rSelectedBones)
        {
            bool lIsDirty = false;

#if UNITY_EDITOR

            // Load bones if they are invalid
            if (mBones == null || mBones.Count == 0) { LoadBones(); }

            // Render out the properties
            base.OnInspectorGUI(rSelectedBones);

            GUILayout.Space(5);

            EditorGUILayout.LabelField("Bone List:");

            // Force the selected bone based on the input list
            bool lIsListDirty = RenderBoneList(mBones, rSelectedBones);
            if (lIsListDirty) { lIsDirty = true; }

#endif

            return lIsDirty;
        }


        /// <summary>
        /// Renders out bone details specific to the motor
        /// </summary>
        /// <param name="rIndex"></param>
        /// <param name="rBone"></param>
        /// <returns></returns>
        protected override bool RenderBone(int rIndex, BoneControllerBone rBone)
        {
            bool lIsDirty = false;

#if UNITY_EDITOR

            while (rIndex >= _BoneInfo.Count)
            {
                RotationBone lBoneInfo = new RotationBone();
                _BoneInfo.Insert(mBones.IndexOf(rBone), lBoneInfo);
            }

            float lNewWeight = EditorGUILayout.FloatField(new GUIContent("Motor Weight", "Determines how much the motor effects vs. currently animated rotation."), _BoneInfo[rIndex].Weight);
            if (lNewWeight != _BoneInfo[rIndex].Weight)
            {
                lIsDirty = true;
                _BoneInfo[rIndex].Weight = lNewWeight;
            }

            float lNewRotationLerp = EditorGUILayout.FloatField(new GUIContent("Rotation Lerp", "Determines how quickly we rotate to the target when using fixed updates."), _BoneInfo[rIndex].RotationLerp);
            if (lNewRotationLerp != _BoneInfo[rIndex].RotationLerp)
            {
                lIsDirty = true;
                _BoneInfo[rIndex].RotationLerp = lNewRotationLerp;
            }

            int lNewRotationAxis = EditorGUILayout.Popup("Rotation Space", _BoneInfo[rIndex].RotationAxis, EnumIKBoneRotationAxis.Names);
            if (lNewRotationAxis != _BoneInfo[rIndex].RotationAxis)
            {
                lIsDirty = true;
                _BoneInfo[rIndex].RotationAxis = lNewRotationAxis;
            }

            Vector3 lNewRotationSpeed = EditorGUILayout.Vector3Field(new GUIContent("Rotation Speed", "Degrees per second to rotate around the axis"), _BoneInfo[rIndex].RotationSpeed);
            if (lNewRotationSpeed != _BoneInfo[rIndex].RotationSpeed)
            {
                lIsDirty = true;
                _BoneInfo[rIndex].RotationSpeed = lNewRotationSpeed;
            }

#endif

            return lIsDirty;
        }

        /// <summary>
        /// Allows the motor to process any specific bone logic after
        /// a bone has been added
        /// </summary>
        /// <param name="rIndex">Index position of the new bone</param>
        /// <param name="rBone">New bone that was added</param>
        public override void AddBone(BoneControllerBone rBone, bool rIncludeChildren)
        {
            base.AddBone(rBone, rIncludeChildren);

            RotationBone lBoneInfo = new RotationBone();
            
            if (rBone != null)
            {
                lBoneInfo.Euler = (rBone._Transform.rotation * rBone._ToBoneForwardInv).eulerAngles;
            }

            _BoneInfo.Insert(mBones.IndexOf(rBone), lBoneInfo);
        }

        /// <summary>
        /// Allows the motor to process any specific bone logic after 
        /// a bone has been deleted
        /// </summary>
        /// <param name="rIndex">Index position the bone was at</param>
        /// <param name="rBone">Bone that was deleted</param>
        protected override void RemoveBone(BoneControllerBone rBone, bool rIncludeChildren)
        {
            int lIndex = mBones.IndexOf(rBone);
            if (lIndex >= 0)
            {
                _BoneInfo.RemoveAt(lIndex);
                base.RemoveBone(rBone, rIncludeChildren);
            }
        }
        
        // ************************************** SUPPORT CLASSES **************************************

        /// <summary>
        /// Contains information about the bone twist and weighting to use when
        /// passing the twist to the next bone
        /// </summary>
        [Serializable]
        public class RotationBone
        {
            /// <summary>
            /// Axis we're rotating around
            /// 0 = Bone
            /// 1 = Model
            /// 2 = World
            /// </summary>
            public int RotationAxis = 0;

            /// <summary>
            /// Speeds (degrees per second) to rotate around the axis
            /// </summary>
            public Vector3 RotationSpeed = Vector3.zero;

            /// <summary>
            /// Rotation the model started at
            /// </summary>
            public Quaternion BaseRotation = Quaternion.identity;

            /// <summary>
            /// Euler angles we're currently rotated at
            /// </summary>
            public Vector3 Euler = Vector3.zero;

            /// <summary>
            /// Determines how much the motor overrides the natural rotation
            /// </summary>
            public float Weight = 1f;

            /// <summary>
            /// Determines how quickly we reach the target
            /// </summary>
            public float RotationLerp = 1f;

            /// <summary>
            /// Amount to world rotation to rotate the bone to
            /// </summary>
            public Quaternion Rotation = Quaternion.identity;

            /// <summary>
            /// Target we're going to lerp towards
            /// </summary>
            public Quaternion RotationTarget = Quaternion.identity;

            /// <summary>
            /// Constructor
            /// </summary>
            public RotationBone()
            {
            }
        }
    }
}
