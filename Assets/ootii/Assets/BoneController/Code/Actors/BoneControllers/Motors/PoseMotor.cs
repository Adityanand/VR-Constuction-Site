using System;
using System.Collections.Generic;
using UnityEngine;
using com.ootii.Geometry;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace com.ootii.Actors.BoneControllers
{
    /// <summary>
    /// The Pose Motor forces the bones to a specific rotation. The result
    /// of one or bones being rotated is a pose.
    /// </summary>
    [Serializable]
    [IKName("Pose Motor")]
    public class PoseMotor : BoneControllerMotor
    {
        /// <summary>
        /// Rotations to apply to the bones
        /// </summary>
        public List<PoseMotorBone> _BoneInfo = new List<PoseMotorBone>();

        /// <summary>
        /// Default constructor
        /// </summary>
        public PoseMotor() 
            : base()
        {
            FixedUpdateFPS = 60f;
            IsFixedUpdateEnabled = false;
            IsEditorEnabled = true;
        }

        /// <summary>
        /// Skeleton constructor
        /// </summary>
        /// <param name="rSkeleton">Skeleton the motor is driving</param>
        public PoseMotor(BoneController rSkeleton)
            : base(rSkeleton)
        {
            FixedUpdateFPS = 60f;
            IsFixedUpdateEnabled = false;
            IsEditorEnabled = true;
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
            // Ensure we have the correct amount of bone infos... we should
            while (_BoneInfo.Count < mBones.Count)
            {
                PoseMotorBone lBoneInfo = new PoseMotorBone();
                _BoneInfo.Add(lBoneInfo);
            }

            // If it's time to update, determine the positions we need to be
            // at and lerp towards them.
            if (rUpdate)
            {
                // Start rotating each of the bones independantly
                for (int i = mBones.Count - 1; i >= 0; i--)
                {
                    if (_BoneInfo.Count <= i) { continue; }
                    if (!_BoneInfo[i].IsEnabled) { continue; }

                    BoneControllerBone lBone = mBones[i];
                    if (lBone == null || lBone._Transform == null) { continue; }

                    // The current rotation we will lerp from.
                    Quaternion lWorldRotation = lBone._Transform.rotation;
                    Quaternion lLocalRotation = Quaternion.Inverse(lBone.WorldBindRotation) * lWorldRotation;

                    // Now, we need to set the rotation as if the bone's forward is the starting point
                    lLocalRotation = lLocalRotation * lBone._ToBoneForward;

                    // Extract out the components
                    Quaternion lLocalSwing = Quaternion.identity;
                    Quaternion lLocalTwist = Quaternion.identity;
                    lLocalRotation.DecomposeSwingTwist(Vector3.forward, ref lLocalSwing, ref lLocalTwist);

                    // Based on the weight, use the current rotation or our target rotations
                    Quaternion lSwingTarget = Quaternion.Lerp(lLocalSwing, _BoneInfo[i].Swing, _Weight * _BoneInfo[i].Weight);
                    Quaternion lTwistTarget = Quaternion.Lerp(lLocalTwist, _BoneInfo[i].Twist, _Weight * _BoneInfo[i].Weight);

                    // Slowly move towards the targets we determined
                    float lLerp = _BoneInfo[i].RotationLerp;

#if UNITY_EDITOR
                    // If we're editing, don't lerp. Jut move
                    if (!EditorApplication.isPlaying) { lLerp = 1f; }
#endif

                    // Move towards the target
                    _BoneInfo[i].ActualSwing = Quaternion.Lerp(_BoneInfo[i].ActualSwing, lSwingTarget, (_IsFixedUpdateEnabled && !mIsFirstUpdate ? lLerp : 1f));
                    _BoneInfo[i].ActualTwist = Quaternion.Lerp(_BoneInfo[i].ActualTwist, lTwistTarget, (_IsFixedUpdateEnabled && !mIsFirstUpdate ? lLerp : 1f));

                    // Set the world rotation
                    lBone.SetLocalRotation(_BoneInfo[i].ActualSwing, _BoneInfo[i].ActualTwist, _BoneWeight);
                }
            }
            // If it's not on a consistant update, we just want to reset the
            // last rotations that we found.
            else
            {
                for (int i = mBones.Count - 1; i >= 0; i--)
                {
                    mBones[i].SetLocalRotation(_BoneInfo[i].ActualSwing, _BoneInfo[i].ActualTwist, _BoneWeight);
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

            // Ensure our bones are valid
            if (mBones != null && mBones.Count > 0)
            {
                for (int i = mBones.Count - 1; i >= 0; i--)
                {
                    if (mBones[i] == null) { mBones.RemoveAt(i); }
                }
            }

            // Load bones if they are invalid
            if (mBones == null || mBones.Count == 0) { LoadBones(); }

            // Force the selected bone based on the input list
            //BoneControllerBone lSelectedBone = null;
            //int lSelectedBoneIndex = -1;
            if (rSelectedBones.Count > 0)
            {
                //lSelectedBone = rSelectedBones[0];
                //lSelectedBoneIndex = mBones.IndexOf(lSelectedBone);
            }

            GUILayout.Space(5);

            EditorGUILayout.LabelField("Bone List:");

            // Force the selected bone based on the input list
            bool lIsListDirty = RenderBoneList(mBones, rSelectedBones);
            if (lIsListDirty) { lIsDirty = true; }

#endif      
      
            return lIsDirty;
        }

        /// <summary>
        /// Allow the motor to control the scene GUI
        /// </summary>
        /// <returns></returns>
        public override bool OnSceneGUI(List<BoneControllerBone> rSelectedBones)
        {
            bool lIsDirty = false;

#if UNITY_EDITOR

            // Force the selected bone based on the input list
            BoneControllerBone lSelectedBone = null;
            int lSelectedBoneIndex = -1;
            if (rSelectedBones.Count > 0)
            {
                lSelectedBone = rSelectedBones[0];
                lSelectedBoneIndex = mBones.IndexOf(lSelectedBone);
            }

            // If we have a bone index, we can process the motor
            if (lSelectedBoneIndex >= 0 && lSelectedBoneIndex < mBones.Count)
            {
                PoseMotorBone lBoneInfo = _BoneInfo[lSelectedBoneIndex];
                lBoneInfo.IsDirty = false;

                if (lSelectedBone != null)
                {
                    // Local space rotators
                    if (lSelectedBone.Joint == null)
                    {
                        lSelectedBone.OnSceneManipulatorGUI(lBoneInfo);
                    }
                    else
                    {
                        lSelectedBone.Joint.OnSceneManipulatorGUI(lBoneInfo);
                    }

                    // Update the actual dirty flag
                    if (lBoneInfo.IsDirty) { lIsDirty = true; }
                }
            }

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
                PoseMotorBone lBoneInfo = new PoseMotorBone();
                _BoneInfo.Insert(mBones.IndexOf(rBone), lBoneInfo);
            }

            // Set the bone enable
            bool lNewIsEnabled = EditorGUILayout.Toggle(new GUIContent("Is Enabled", "Determines if this bone pose is enabled."), _BoneInfo[rIndex].IsEnabled);
            if (lNewIsEnabled != _BoneInfo[rIndex].IsEnabled)
            {
                lIsDirty = true;
                _BoneInfo[rIndex].IsEnabled = lNewIsEnabled;
            }

            // Set the bone weight
            float lNewWeight = EditorGUILayout.FloatField(new GUIContent("Weight", "Determines how much the motor effects vs. currently animated rotation."), _BoneInfo[rIndex].Weight);
            if (lNewWeight != _BoneInfo[rIndex].Weight)
            {
                lIsDirty = true;
                _BoneInfo[rIndex].Weight = lNewWeight;
            }

            // Set the bone lerp
            float lNewRotationLerp = EditorGUILayout.FloatField(new GUIContent("Rotation Lerp", "Determines how quickly we rotate to the target."), _BoneInfo[rIndex].RotationLerp);
            if (lNewRotationLerp != _BoneInfo[rIndex].RotationLerp)
            {
                lIsDirty = true;
                _BoneInfo[rIndex].RotationLerp = lNewRotationLerp;
            }
            
            // Render inspector controls based on the joint type
            if (rBone != null)
            {
                PoseMotorBone lBoneInfo = _BoneInfo[rIndex];
                lBoneInfo.IsDirty = false;

                if (rBone.Joint == null)
                {
                    rBone.OnInspectorManipulatorGUI(lBoneInfo);
                }
                else
                {
                    rBone.Joint.OnInspectorManipulatorGUI(lBoneInfo);
                }

                // Update the actual dirty flag
                if (_BoneInfo[rIndex].IsDirty) { lIsDirty = true; }
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

            PoseMotorBone lBoneInfo = new PoseMotorBone();
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
        public class PoseMotorBone : IKBoneModifier
        {
            /// <summary>
            /// Determines if this bone pose is enabled.
            /// </summary>
            public bool IsEnabled = true;

            /// <summary>
            /// Rotation we'll actually apply
            /// </summary>
            public Quaternion Rotation = Quaternion.identity;

            /// <summary>
            /// Rotation we'll actually apply
            /// </summary>
            public Quaternion ActualSwing = Quaternion.identity;

            /// <summary>
            /// Rotation we'll actually apply
            /// </summary>
            public Quaternion ActualTwist = Quaternion.identity;

            /// <summary>
            /// Determines how quickly we reach the target
            /// </summary>
            public float RotationLerp = 0.1f;

            /// <summary>
            /// Constructor
            /// </summary>
            public PoseMotorBone() 
                : base()
            {
            }
        }
    }
}
