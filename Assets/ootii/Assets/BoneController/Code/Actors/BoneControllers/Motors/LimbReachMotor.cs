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
    /// The limb reach motor is a special motor used for 
    /// 2-bone systems: UpperArm + ForeArm or UpperLeg + Leg.
    /// 
    /// This allows for easy bending at the elbow, knee, etc.
    /// </summary>
    [Serializable]
    [IKName("Limb Reach Motor")]
    public class LimbReachMotor : BoneControllerMotor
    {
        /// <summary>
        /// IK solve results to keep us from reallocating
        /// </summary>
        //private static Dictionary<BoneControllerBone, Quaternion> sIKResults = new Dictionary<BoneControllerBone, Quaternion>();

        /// <summary>
        /// Transform that defines the position we're attempting
        /// to reach. If set, it overrides the target position.
        /// </summary>
        public Transform _TargetTransform = null;
        public Transform TargetTransform
        {
            get { return _TargetTransform; }
            set
            {
                _TargetTransform = value;
                if (_TargetTransform == null)
                {
                    _TargetTransformName = "";
                }
                else
                {
                    _TargetTransformName = _TargetTransform.name;
                }
            }
        }

        /// <summary>
        /// Name of the target transform
        /// </summary>
        public string _TargetTransformName = "";

        /// <summary>
        /// Target position that we're attempting to reach. If a 
        /// target transform is set, it will override the position.
        /// </summary>
        public Vector3 _TargetPosition;
        public Vector3 TargetPosition
        {
            get { return _TargetPosition; }
            set { _TargetPosition = value; }
        }

        /// <summary>
        /// Determines if the limb reaches and twists based on it's current 
        /// rotation or the bind rotation
        /// </summary>
        public bool _UseBindRotation = true;
        public bool UseBindRotation
        {
            get { return _UseBindRotation; }
            set { _UseBindRotation = value; }
        }

        /// <summary>
        /// Determines if the limb reaches and twists on the current plane normal
        /// represented by the three joints
        /// </summary>
        public bool _UsePlaneNormal = false;
        public bool UsePlaneNormal
        {
            get { return _UsePlaneNormal; }
            set { _UsePlaneNormal = value; }
        }

        /// <summary>
        /// Allows us to modify the length of the second bone.
        /// </summary>
        public float _Bone2Extension = 0f;
        public float Bone2Extension
        {
            get { return _Bone2Extension; }
            set { _Bone2Extension = value; }
        }

        /// <summary>
        /// Extra information in order to help the management of the bones
        /// </summary>
        public List<LimbReachMotorBone> _BoneInfo = new List<LimbReachMotorBone>();

        /// <summary>
        /// Default constructor
        /// </summary>
        public LimbReachMotor() 
            : base()
        {
            _FixedUpdateFPS = 60f;
            _IsFixedUpdateEnabled = false;
        }

        /// <summary>
        /// Skeleton constructor
        /// </summary>
        /// <param name="rSkeleton">Skeleton the motor is driving</param>
        public LimbReachMotor(BoneController rSkeleton)
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

            if (rStyle == "humanoid left arm")
            {
                AddBone(Skeleton.GetBone(HumanBodyBones.LeftUpperArm) as BoneControllerBone, false);
                AddBone(Skeleton.GetBone(HumanBodyBones.LeftLowerArm) as BoneControllerBone, false);

                if (_BoneInfo.Count == 2)
                {
                    _BoneInfo[0].BendAxis = -Vector3.up;
                    _BoneInfo[0].Twist = 0f;

                    _BoneInfo[1].BendAxis = -Vector3.up;
                    _BoneInfo[1].Twist = 90f;
                }
            }
            else if (rStyle == "humanoid left leg")
            {
                AddBone(Skeleton.GetBone(HumanBodyBones.LeftUpperLeg) as BoneControllerBone, false);
                AddBone(Skeleton.GetBone(HumanBodyBones.LeftLowerLeg) as BoneControllerBone, false);

                if (_BoneInfo.Count == 2)
                {
                    _BoneInfo[0].BendAxis = -Vector3.right;
                    _BoneInfo[0].Twist = 0f;

                    _BoneInfo[1].BendAxis = Vector3.right;
                    _BoneInfo[1].Twist = 0f;
                }
            }
            else if (rStyle == "humanoid right arm")
            {
                AddBone(Skeleton.GetBone(HumanBodyBones.RightUpperArm) as BoneControllerBone, false);
                AddBone(Skeleton.GetBone(HumanBodyBones.RightLowerArm) as BoneControllerBone, false);

                if (_BoneInfo.Count == 2)
                {
                    _BoneInfo[0].BendAxis = Vector3.up;
                    _BoneInfo[0].Twist = 0f;

                    _BoneInfo[1].BendAxis = Vector3.up;
                    _BoneInfo[1].Twist = -90f;
                }
            }
            else if (rStyle == "humanoid right leg")
            {
                AddBone(Skeleton.GetBone(HumanBodyBones.RightUpperLeg) as BoneControllerBone, false);
                AddBone(Skeleton.GetBone(HumanBodyBones.RightLowerLeg) as BoneControllerBone, false);

                if (_BoneInfo.Count == 2)
                {
                    _BoneInfo[0].BendAxis = -Vector3.right;
                    _BoneInfo[0].Twist = 0f;

                    _BoneInfo[1].BendAxis = Vector3.right;
                    _BoneInfo[1].Twist = 0f;
                }
            }

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
            if (mBones == null || mBones.Count < 2) { return; }

            if (_TargetTransform == null && _TargetTransformName.Length > 0)
            {
                GameObject lObject = GameObject.Find(_TargetTransformName);
                if (lObject != null) { _TargetTransform = lObject.transform; }
            }

            if (_TargetTransform != null && !_TargetTransform.gameObject.activeInHierarchy)
            {
                return;
            }

            // Ensure we have the correct amount of bone infos... we should
            while (_BoneInfo.Count < mBones.Count)
            {
                LimbReachMotorBone lBoneInfo = new LimbReachMotorBone();
                _BoneInfo.Add(lBoneInfo);
            }

            // If it's time to update, determine the positions we need to be
            // at and lerp towards them.
            if (rUpdate)
            {
                // Grab the target. Priority is given to the transform
                Vector3 lTargetPosition = (_TargetTransform != null ? _TargetTransform.position : _TargetPosition);
                if (lTargetPosition == Vector3.zero) { return; }

                // Simplify the bone names
                BoneControllerBone lBoneChainRoot = mBones[0];
                BoneControllerBone lBoneChainEnd = mBones[1];

                // If we have valid bones, solve
                if (lBoneChainRoot != null && lBoneChainEnd != null)
                {
                    //HingeSwingAndTwistJoint lEndJoint = lBoneChainEnd.Joint as HingeSwingAndTwistJoint;

                    IKSolverState lState = IKSolverState.Allocate();
                    lState.TargetPosition = lTargetPosition;
                    lState.UseBindRotation = _UseBindRotation;
                    lState.UsePlaneNormal = _UsePlaneNormal;
                    lState.IsDebugEnabled = _IsDebugEnabled;

                    lState.Bones.Add(lBoneChainRoot);
                    lState.Bones.Add(lBoneChainEnd);

                    lState.BoneBendAxes.Add(_BoneInfo[0].BendAxis);
                    lState.BoneBendAxes.Add(_BoneInfo[1].BendAxis);

                    CosineSolver.SolveIK(ref lState, _Bone2Extension);

                    // Process the results of the solve. We use the enumerator to
                    // avoid garbage from the ForEach
                    Dictionary<BoneControllerBone, Quaternion>.Enumerator lEnumerator = lState.Rotations.GetEnumerator();
                    while (lEnumerator.MoveNext())
                    {
                        BoneControllerBone lBone = lEnumerator.Current.Key;

                        int lIndex = mBones.IndexOf(lBone);

                        // The current rotation we will lerp from. We remove the trailing rotation offset because we'll add it later
                        Quaternion lCurrentRotation = lBone.Transform.rotation * lBone.ToBoneForward;

                        // Rotation based on the target position
                        Quaternion lTargetRotation = lState.Rotations[lBone] * Quaternion.Euler(0f, 0f, _BoneInfo[lIndex].Twist);

                        // Rotation as determined by the target
                        _BoneInfo[lIndex].RotationTarget = Quaternion.Lerp(lCurrentRotation, lTargetRotation, _Weight * _BoneInfo[lIndex].Weight);

                        // Slowly move towards the rotation we determined
                        _BoneInfo[lIndex].Rotation = Quaternion.Lerp(_BoneInfo[lIndex].Rotation, _BoneInfo[lIndex].RotationTarget, (_IsFixedUpdateEnabled && !mIsFirstUpdate ? _BoneInfo[lIndex].RotationLerp : 1f));

                        // Set the world rotation
                        lBone.SetWorldRotation(_BoneInfo[lIndex].Rotation, _BoneWeight);
                        if (lBone.ApplyLimitsInFrame) { lBone.ApplyLimitsInFrame = _ApplyLimits; }
                    }

                    //foreach (BoneControllerBone lBone in lState.Rotations.Keys)
                    //{
                    //    int lIndex = mBones.IndexOf(lBone);

                    //    // The current rotation we will lerp from. We remove the trailing rotation offset because we'll add it later
                    //    Quaternion lCurrentRotation = lBone.Transform.rotation * lBone.ToBoneForward;

                    //    // Rotation based on the target position
                    //    Quaternion lTargetRotation = lState.Rotations[lBone] * Quaternion.Euler(0f, 0f, _BoneInfo[lIndex].Twist);

                    //    // Rotation as determined by the target
                    //    _BoneInfo[lIndex].RotationTarget = Quaternion.Lerp(lCurrentRotation, lTargetRotation, _Weight * _BoneInfo[lIndex].Weight);

                    //    // Slowly move towards the rotation we determined
                    //    _BoneInfo[lIndex].Rotation = Quaternion.Lerp(_BoneInfo[lIndex].Rotation, _BoneInfo[lIndex].RotationTarget, (_IsFixedUpdateEnabled && !mIsFirstUpdate ? _BoneInfo[lIndex].RotationLerp : 1f));

                    //    // Set the world rotation
                    //    lBone.SetWorldRotation(_BoneInfo[lIndex].Rotation, _BoneWeight);
                    //    if (lBone.ApplyLimitsInFrame) { lBone.ApplyLimitsInFrame = _ApplyLimits; }
                    //}

                    IKSolverState.Release(lState);
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

                    lBone.SetWorldRotation(_BoneInfo[i].Rotation, _BoneWeight);
                }
            }
        }

        /// <summary>
        /// Due to Unity's serialization limit on nested objects (7 levels),
        /// we have to store the bones in a flat list and then reconstruct
        /// our hierarchy after deserialization.
        /// 
        /// Also, object references aren't actually kept after deserialization. 
        /// Instead, we get a new instance of the BoneControllerBone (parent and children). So,
        /// we need to reset the local object based on the index that was stored.
        /// 
        /// This function is called AFTER the skeleton has been deserialized
        /// 
        /// 
        /// </summary>
        /// <param name="rSkeleton"></param>
        public override void OnAfterSkeletonDeserialized(BoneController rSkeleton)
        {
            base.OnAfterSkeletonDeserialized(rSkeleton);

            if (_TargetTransform == null && _TargetTransformName.Length > 0)
            {
                Transform[] lObjects = Resources.FindObjectsOfTypeAll<Transform>();
                for (int i = 0; i < lObjects.Length; i++)
                {
                    if (lObjects[i].name == _TargetTransformName)
                    {
                        _TargetTransform = lObjects[i];
                    }
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

            bool lNewUseBindRotation = EditorGUILayout.Toggle(new GUIContent("Use Bind Rotation", "Determines if we use the bind rotation or current rotation for determining twisting."), _UseBindRotation);
            if (lNewUseBindRotation != _UseBindRotation)
            {
                lIsDirty = true;
                _UseBindRotation = lNewUseBindRotation;
            }

            bool lNewUsePlaneNormal = EditorGUILayout.Toggle(new GUIContent("Use Plane Normal", "Determines if we use the normal of the current plane for determining twisting."), _UsePlaneNormal);
            if (lNewUsePlaneNormal != _UsePlaneNormal)
            {
                lIsDirty = true;
                _UsePlaneNormal = lNewUsePlaneNormal;
            }

            bool lNewApplyLimits = EditorGUILayout.Toggle(new GUIContent("Apply Joint Limits", "Determines if the reach motor will apply joint limits"), _ApplyLimits);
            if (lNewApplyLimits != _ApplyLimits)
            {
                lIsDirty = true;
                _ApplyLimits = lNewApplyLimits;
            }

            GUILayout.Space(5);

            float lNewBone2Extension = EditorGUILayout.FloatField(new GUIContent("Bone 2 Extension", "Allows us to modify the length of the second bone."), _Bone2Extension);
            if (lNewBone2Extension != _Bone2Extension)
            {
                lIsDirty = true;
                Bone2Extension = lNewBone2Extension;
            }

            GUILayout.Space(5);

            Transform lNewTargetTransform = EditorGUILayout.ObjectField(new GUIContent("Target Transform", "Target transform to reachf or."), _TargetTransform, typeof(Transform), true) as Transform;
            if (lNewTargetTransform != _TargetTransform)
            {
                lIsDirty = true;
                TargetTransform = lNewTargetTransform;
            }

            Vector3 lNewTargetPosition = EditorGUILayout.Vector3Field(new GUIContent("Target Position", "Target position to reach for."), _TargetPosition);
            if (lNewTargetPosition != _TargetPosition)
            {
                lIsDirty = true;
                _TargetPosition = lNewTargetPosition;
            }

            GUILayout.Space(5);

            EditorGUILayout.LabelField("Bone List:");

            EditorGUILayout.BeginVertical(GUI.skin.box);

            EditorGUILayout.LabelField("Auto-Generate Humanoid");

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button(new GUIContent("Left Arm", "Auto setup for a left humanoid leg."), EditorStyles.miniButton))
            {
                if (EditorUtility.DisplayDialog("Bone Controller", "Update settings?", "Yes", "No"))
                {
                    AutoLoadBones("Humanoid Left Arm");
                }
            }

            if (GUILayout.Button(new GUIContent("Right Arm", "Auto setup for a left humanoid leg."), EditorStyles.miniButton))
            {
                if (EditorUtility.DisplayDialog("Bone Controller", "Update settings?", "Yes", "No"))
                {
                    AutoLoadBones("Humanoid Right Arm");
                }
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button(new GUIContent("Left Leg", "Auto setup for a right humanoid leg."), EditorStyles.miniButton))
            {
                if (EditorUtility.DisplayDialog("Bone Controller", "Update settings?", "Yes", "No"))
                {
                    AutoLoadBones("Humanoid Left Leg");
                }
            }

            if (GUILayout.Button(new GUIContent("Right Leg", "Auto setup for a right humanoid leg."), EditorStyles.miniButton))
            {
                if (EditorUtility.DisplayDialog("Bone Controller", "Update settings?", "Yes", "No"))
                {
                    AutoLoadBones("Humanoid Right Leg");
                }
            }

            EditorGUILayout.EndHorizontal();

            GUILayout.Space(3);

            EditorGUILayout.EndVertical();

            // Force the selected bone based on the input list
            bool lIsListDirty = RenderBoneList(mBones, rSelectedBones, 2);
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
                LimbReachMotorBone lBoneInfo = new LimbReachMotorBone();
                _BoneInfo.Insert(mBones.IndexOf(rBone), lBoneInfo);
            }

            // Set the bone weight
            float lNewWeight = EditorGUILayout.FloatField(new GUIContent("Weight", "Determines how much the motor effects vs. currently animated rotation."), _BoneInfo[rIndex].Weight);
            if (lNewWeight != _BoneInfo[rIndex].Weight)
            {
                lIsDirty = true;
                _BoneInfo[rIndex].Weight = lNewWeight;
            }

            // Set the bone yaw
            float lNewRotationLerp = EditorGUILayout.FloatField(new GUIContent("Rotation Lerp", "Determines how quickly we rotate to the target."), _BoneInfo[rIndex].RotationLerp);
            if (lNewRotationLerp != _BoneInfo[rIndex].RotationLerp)
            {
                lIsDirty = true;
                _BoneInfo[rIndex].RotationLerp = lNewRotationLerp;
            }

            // Axis to rotate around
            Vector3 lNewBendAxis = EditorGUILayout.Vector3Field(new GUIContent("Support Axis", "Axis used for rotating the bone."), _BoneInfo[rIndex].BendAxis);
            if (lNewBendAxis != _BoneInfo[rIndex].BendAxis)
            {
                lIsDirty = true;
                _BoneInfo[rIndex].BendAxis = lNewBendAxis;
            }

            // Final twist to apply to the bone after it's rotated
            float lNewTwist = EditorGUILayout.FloatField(new GUIContent("Final Twist Adjust", "Twist applied to the bone after movement."), _BoneInfo[rIndex].Twist);
            if (lNewTwist != _BoneInfo[rIndex].Twist)
            {
                lIsDirty = true;
                _BoneInfo[rIndex].Twist = lNewTwist;
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

            LimbReachMotorBone lBoneInfo = new LimbReachMotorBone();
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
        public class LimbReachMotorBone
        {
            /// <summary>
            /// Additional rotation we'll apply to the bone in order to adjust
            /// it as needed.
            /// </summary>
            public Vector3 BendAxis = Vector3.right;

            /// <summary>
            /// Final twist to apply to the bone that is being moved
            /// </summary>
            public float Twist = 0f;

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
            public float Weight = 1f;

            /// <summary>
            /// Constructor
            /// </summary>
            public LimbReachMotorBone()
            {
            }
        }
    }
}
