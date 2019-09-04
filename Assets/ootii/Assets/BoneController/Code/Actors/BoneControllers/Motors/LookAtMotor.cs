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
    /// Controls the spine rotation of the character so it looks at the target
    /// </summary>
    [Serializable]
    [IKName("Look At Motor")]
    [IKDescription("Use this motor to have bones rotate towards the target. Since most spine, neck, and head bones are oriented with 'bone forward' up... we have the 'bone up' look to the target.")]
    public class LookAtMotor : BoneControllerMotor
    {
        /// <summary>
        /// Transform that represents the target we're supposed to be looking at
        /// </summary>
        public Transform _TargetTransform = null;
        public Transform TargetTransform
        {
            get { return _TargetTransform; }
            set { _TargetTransform = value; }
        }

        /// <summary>
        /// Determines if the target transform is used as a position or direction. When
        /// used as a direction, the look at will mimic the forward direction of the transform.
        /// </summary>
        public bool _UseAsDirection = false;
        public bool UseAsDirection
        {
            get { return _UseAsDirection; }
            set { _UseAsDirection = value; }
        }

        /// <summary>
        /// Vector3 that represents the target we're supposed to be looking at
        /// </summary>
        public Vector3 _TargetPosition = Vector3.zero;
        public Vector3 TargetPosition
        {
            get { return _TargetPosition; }
            set { _TargetPosition = value; }
        }

        /// <summary>
        /// Determines if we'll invert the yaw and rotations
        /// </summary>
        public bool _InvertRotations = false;
        public bool InvertRotations
        {
            get { return _InvertRotations; }
            set { _InvertRotations = value; }
        }

        /// <summary>
        /// Extra information in order to help the management of the bones
        /// </summary>
        public List<LookAtMotorBone> _BoneInfo = new List<LookAtMotorBone>();

        /// <summary>
        /// Default constructor
        /// </summary>
        public LookAtMotor()
            : base()
        {
            _FixedUpdateFPS = 60f;
            _IsFixedUpdateEnabled = false;
        }

        /// <summary>
        /// Skeleton constructor
        /// </summary>
        /// <param name="rSkeleton">Skeleton the motor is driving</param>
        public LookAtMotor(BoneController rSkeleton)
            : base(rSkeleton)
        {
            _FixedUpdateFPS = 60f;
            _IsFixedUpdateEnabled = false;
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
        /// Automatically loads bones for the developer. This is typically done by using things like
        /// the HumanBodyBones.
        /// </summary>
        /// <param name="rStyle">String that can be used to define how to load bones</param>
        public virtual void AutoLoadBones(string rStyle)
        {
            rStyle = rStyle.ToLower();

            mBones.Clear();
            _BoneInfo.Clear();

            AddBone(Skeleton.GetBone(HumanBodyBones.Head) as BoneControllerBone, false);
            AddBone(Skeleton.GetBone(HumanBodyBones.Neck) as BoneControllerBone, false);
            AddBone(Skeleton.GetBone(HumanBodyBones.Chest) as BoneControllerBone, false);

            // Reset the invalidation flag
            mIsValid = true;
        }

        /// <summary>
        /// Process the motor each frame so that it can update the bone rotations.
        /// This is the function that should be overridden in each motor
        /// </summary>
        /// <param name="rDeltaTime">Delta time to use for the update</param>
        /// <param name="rUpdate">Determines if it is officially time to do the update</param>
        protected override void Update(float rDeltaTime, bool rUpdate)
        {
            // Get out if there are no valid bones
            if (mBones.Count == 0 || mBones[0] == null || mBones[0]._Transform == null) { return; }

            // If it's time to update, determine the positions we need to be
            // at and lerp towards them.
            if (rUpdate)
            {
                // Extract out the target info and resulting rotation
                Vector3 lTargetPosition = _TargetPosition;
                if (_TargetTransform != null)
                {
                    if (_UseAsDirection)
                    {
                        lTargetPosition = mBones[0]._Transform.position + (_TargetTransform.forward * 2f);
                    }
                    else
                    {
                        lTargetPosition = _TargetTransform.position;
                    }
                }

                // Forward direction that we want to be looking from
                Vector3 lAnchorPosition = mBones[0]._Transform.position;

                // The target we want to rotate to
                Vector3 lToTarget = (lTargetPosition - lAnchorPosition).normalized;
                Quaternion lTargetRotation = Quaternion.LookRotation(lToTarget, (_InvertRotations ? -1 : 1) * (mSkeleton != null && mSkeleton.transform != null ? mSkeleton.transform.up : Vector3.up));

                // Start rotating each of the bones independantly
                for (int i = mBones.Count - 1; i >= 0; i--)
                {
                    if (_BoneInfo.Count <= i) { continue; }

                    BoneControllerBone lBone = mBones[i];
                    if (lBone == null) { continue; }

                    // This swings around the bone around it's bone's current x and y axis and twists around 
                    // the current bone direction
                    Quaternion lRotationOffset = lBone.BindRotation * lBone.ToBoneForward;
                    lRotationOffset = lRotationOffset * Quaternion.AngleAxis(_BoneInfo[i].RotationOffset.y, Vector3.up);
                    lRotationOffset = lRotationOffset * Quaternion.AngleAxis(_BoneInfo[i].RotationOffset.x, Vector3.right);
                    lRotationOffset = lRotationOffset * Quaternion.AngleAxis(_BoneInfo[i].RotationOffset.z, Vector3.forward);

                    // The current rotation we will lerp from. We remove the trailing rotation offset because we'll add it later
                    Quaternion lCurrentRotation = mBones[i].Transform.rotation * mBones[i].ToBoneForward * Quaternion.Inverse(lRotationOffset);

                    // Target rotation. We want to convert from "Bone Forward" to "Bone Up"-forward since it's the up direction
                    // that will point to the target. Then, we need to remove part of the offset since it will be re-added.
                    Quaternion lBoneTargetRotation = lTargetRotation * Quaternion.AngleAxis(180f, Vector3.up) * Quaternion.AngleAxis(-90f, Vector3.right) * Quaternion.Inverse(lBone.BindRotation * lBone.ToBoneForward);

                    // Based on the weight, use the current rotation or our target rotation
                    _BoneInfo[i].RotationTarget = Quaternion.Lerp(lCurrentRotation, lBoneTargetRotation, _Weight * _BoneInfo[i].Weight);

                    // Slowly move towards the targets we determined
                    float lLerp = _BoneInfo[i].RotationLerp;

#if UNITY_EDITOR
                    // If we're editing, don't lerp. Jut move
                    if (!EditorApplication.isPlaying) { lLerp = 1f; }
#endif
                    
                    // Slowly move towards the rotation we determined
                    _BoneInfo[i].Rotation = Quaternion.Lerp(_BoneInfo[i].Rotation, _BoneInfo[i].RotationTarget, (_IsFixedUpdateEnabled && !mIsFirstUpdate ? lLerp : 1f));

                    // Set the world rotation
                    lBone.SetWorldRotation(_BoneInfo[i].Rotation * lRotationOffset, _BoneWeight);
                }
            }
            // If it's not on a consistant update, we just want to reset the
            // last rotations that we found.
            else
            {
                for (int i = mBones.Count - 1; i >= 0; i--)
                {
                    if (_BoneInfo.Count <= i) { continue; }

                    BoneControllerBone lBone = mBones[i];
                    if (lBone == null) { continue; }

                    // This swings around the bone around it's bone's current x and y axis and twists around 
                    // the current bone direction
                    Quaternion lRotationOffset = lBone.BindRotation * lBone.ToBoneForward;
                    lRotationOffset = lRotationOffset * Quaternion.AngleAxis(_BoneInfo[i].RotationOffset.y, Vector3.up);
                    lRotationOffset = lRotationOffset * Quaternion.AngleAxis(_BoneInfo[i].RotationOffset.x, Vector3.right);
                    lRotationOffset = lRotationOffset * Quaternion.AngleAxis(_BoneInfo[i].RotationOffset.z, Vector3.forward);

                    mBones[i].SetWorldRotation(_BoneInfo[i].Rotation * lRotationOffset, _BoneWeight);
                }
            }
        }

        /// <summary>
        /// Returns the LookAtMotorBone object that is associated with the bone of the same name, if available.
        /// This object allows you to update the rotation offset, like in the inspector interface. 
        /// </summary>
        public LookAtMotorBone GetLookAtMotor(string rBoneName)
        {
            for (int i = 0; i < mBones.Count; i++)
            {
                if (string.Compare(mBones[i].Name, rBoneName, true) == 0)
                {
                    return _BoneInfo[i];
                }
            }

            return null;
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

            // Force the selected bone based on the input list
            //BoneControllerBone lSelectedBone = null;
            //int lSelectedBoneIndex = -1;
            if (rSelectedBones.Count > 0)
            {
                //lSelectedBone = rSelectedBones[0];
                //lSelectedBoneIndex = mBones.IndexOf(lSelectedBone);
            }

            GUILayout.Space(5);

            // Determines if we need to invert our rotations
            bool lNewInvertRotations = EditorGUILayout.Toggle(new GUIContent("Invert Up", "Inverts rotations to account for the way some models are created."), _InvertRotations);
            if (lNewInvertRotations != _InvertRotations)
            {
                lIsDirty = true;
                _InvertRotations = lNewInvertRotations;
            }

            GUILayout.Space(5);

            // Allow the target position to be set
            Vector3 lNewTargetPosition = EditorGUILayout.Vector3Field(new GUIContent("Target Position", "Target position to look at if no Target Transform is set."), _TargetPosition);
            if (lNewTargetPosition != _TargetPosition)
            {
                lIsDirty = true;
                _TargetPosition = lNewTargetPosition;
            }

            GUILayout.Space(5);

            // Allow the target transform to be set
            Transform lNewTargetTransform = EditorGUILayout.ObjectField(new GUIContent("Target Transform", "Target transform to look at (or in the direction of)."), _TargetTransform, typeof(Transform), true) as Transform;
            if (lNewTargetTransform != _TargetTransform)
            {
                lIsDirty = true;
                _TargetTransform = lNewTargetTransform;
            }

            if (EditorHelper.BoolField("Use As Direction", "Determines if the Target Transform is used as a direction or as a position.", UseAsDirection, mSkeleton))
            {
                lIsDirty = true;
                UseAsDirection = EditorHelper.FieldBoolValue;
            }

            GUILayout.Space(5);

            EditorGUILayout.LabelField("Bone List:");

            EditorGUILayout.BeginVertical(GUI.skin.box);

            EditorGUILayout.LabelField("Auto-Generate Humanoid");

            if (GUILayout.Button(new GUIContent("Head, Neck, and Core", "Auto setup for humanoid."), EditorStyles.miniButton))
            {
                if (EditorUtility.DisplayDialog("Bone Controller", "Update settings?", "Yes", "No"))
                {
                    AutoLoadBones("");
                }
            }

            GUILayout.Space(3);

            EditorGUILayout.EndVertical();
            
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

            if (_IsDebugEnabled)
            {
                //if (mBones.Count > 0)
                //{
                //    Color lHandlesColor = Handles.color;

                //    Vector3 lAnchorPosition = mBones[0].TransformLocalPointToWorldPoint(_AnchorOffset);
                //    Vector3 lTargetPosition = (_TargetTransform != null ? _TargetTransform.position : _TargetPosition);

                //    Handles.color = Color.green;
                //    Handles.SphereCap(0, lAnchorPosition, Quaternion.identity, 0.02f);
                //    Handles.DrawLine(mBones[0]._Transform.position, lAnchorPosition);

                //    Handles.color = Color.red;
                //    Handles.SphereCap(0, lTargetPosition, Quaternion.identity, 0.02f);
                //    Handles.DrawLine(lAnchorPosition, lTargetPosition);

                //    Handles.color = lHandlesColor;
                //}
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
                LookAtMotorBone lBoneInfo = new LookAtMotorBone();
                lBoneInfo.Weight = 1 / Mathf.Pow(2, _BoneInfo.Count);

                _BoneInfo.Insert(mBones.IndexOf(rBone), lBoneInfo);
            }

            // Set the bone weight
            float lNewWeight = EditorGUILayout.FloatField(new GUIContent("Weight", "Determines how much the motor effects vs. currently animated rotation."), _BoneInfo[rIndex].Weight);
            if (lNewWeight != _BoneInfo[rIndex].Weight)
            {
                lIsDirty = true;
                _BoneInfo[rIndex].Weight = lNewWeight;
            }

            // Set the bone pitch
            Vector3 lNewRotationOffset = EditorGUILayout.Vector3Field(new GUIContent("Rotation Offset", "Allows for adjusting the base rotation as needed."), _BoneInfo[rIndex].RotationOffset);
            if (lNewRotationOffset != _BoneInfo[rIndex].RotationOffset)
            {
                lIsDirty = true;
                _BoneInfo[rIndex].RotationOffset = lNewRotationOffset;
            }

            // Set the bone yaw
            float lNewRotationLerp = EditorGUILayout.FloatField(new GUIContent("Rotation Lerp", "Determines how quickly we rotate to the target."), _BoneInfo[rIndex].RotationLerp);
            if (lNewRotationLerp != _BoneInfo[rIndex].RotationLerp)
            {
                lIsDirty = true;
                _BoneInfo[rIndex].RotationLerp = lNewRotationLerp;
            }

#endif

            return lIsDirty;
        }

        /// <summary>
        /// Allows the motor to process any specific bone logic after
        /// a bone has been added
        /// </summary>
        /// <param name="rBone">New bone that was added</param>
        public override void AddBone(BoneControllerBone rBone, bool rIncludeChildren)
        {
            base.AddBone(rBone, rIncludeChildren);

            LookAtMotorBone lBoneInfo = new LookAtMotorBone();
            lBoneInfo.Weight = 1 / Mathf.Pow(2, _BoneInfo.Count);

            if (rBone == null)
            {
                _BoneInfo.Add(lBoneInfo);
            }
            else
            {
                int lIndex = mBones.IndexOf(rBone);
                _BoneInfo.Insert(lIndex, lBoneInfo);
            }
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
        public class LookAtMotorBone
        {
            /// <summary>
            /// Additional rotation we'll apply to the bone in order to adjust
            /// it as needed.
            /// </summary>
            public Vector3 RotationOffset = Vector3.zero;

            /// <summary>
            /// Amount to world rotation to rotate the bone to
            /// </summary>
            public Quaternion Rotation = Quaternion.identity;

            /// <summary>
            /// Target we're going to lerp towards
            /// </summary>
            public Quaternion RotationTarget = Quaternion.identity;

            /// <summary>
            /// Determines how quickly we reach the target
            /// </summary>
            public float RotationLerp = 0.1f;

            /// <summary>
            /// Determines how much the motor overrides the natural rotation
            /// </summary>
            public float Weight;

            /// <summary>
            /// Constructor
            /// </summary>
            public LookAtMotorBone()
            {
            }
        }
    }
}
