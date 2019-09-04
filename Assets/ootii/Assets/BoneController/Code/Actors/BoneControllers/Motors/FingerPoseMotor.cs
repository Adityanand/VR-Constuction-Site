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
    /// Motor used to control the curling of the fingers and thumbs.
    /// </summary>
    [Serializable]
    [IKName("Finger Pose Motor")]
    [IKDescription("Allows for the posing of the fingers of each hand.")]
    public class FingerPoseMotor : BoneControllerMotor
    {
        /// <summary>
        /// Weight value for managing the left hand
        /// </summary>
        public float _LeftWeight = 1f;
        public float LeftWeight
        {
            get { return _LeftWeight; }
            set { _LeftWeight = value; }
        }

        /// <summary>
        /// Weight value for managing the right hand
        /// </summary>
        public float _RightWeight = 1f;
        public float RightWeight
        {
            get { return _RightWeight; }
            set { _RightWeight = value; }
        }

        /// <summary>
        /// Simple factoring for determining if we should curl the fingers or not
        /// </summary>
        public float _LeftCurl = 0f;
        public float LeftCurl
        {
            get { return _LeftCurl; }

            set
            {
                _LeftCurl = value;

                LeftThumbCurl = value;
                LeftIndexCurl = value;
                LeftMiddleCurl = value;
                LeftRingCurl = value;
                LeftLittleCurl = value;
            }
        }

        /// <summary>
        /// Curls the individual finger/thumb
        /// </summary>
        public float LeftThumbCurl
        {
            get { return _FingerCurls[0]; }

            set
            {
                if (_FingerCurls[0] != value)
                {
                    _FingerCurls[0] = value;
                    SetBoneRotation(HumanBodyBones.LeftThumbProximal, value * 90f * 0.2f, Vector3.right - Vector3.up);
                    SetBoneRotation(HumanBodyBones.LeftThumbIntermediate, value * 90f * 0.2f, Vector3.right - Vector3.up);
                    SetBoneRotation(HumanBodyBones.LeftThumbDistal, value * 90f, Vector3.right - Vector3.up);
                }
            }
        }

        /// <summary>
        /// Curls the individual finger/thumb
        /// </summary>
        public float LeftIndexCurl
        {
            get { return _FingerCurls[1]; }

            set
            {
                if (_FingerCurls[1] != value)
                {
                    _FingerCurls[1] = value;
                    SetBoneRotation(HumanBodyBones.LeftIndexProximal, value * 90f, Vector3.right);
                    SetBoneRotation(HumanBodyBones.LeftIndexIntermediate, value * 90f, Vector3.right);
                    SetBoneRotation(HumanBodyBones.LeftIndexDistal, value * 90f, Vector3.right);
                }
            }
        }

        /// <summary>
        /// Curls the individual finger/thumb
        /// </summary>
        public float LeftMiddleCurl
        {
            get { return _FingerCurls[2]; }

            set
            {
                if (_FingerCurls[2] != value)
                {
                    _FingerCurls[2] = value;
                    SetBoneRotation(HumanBodyBones.LeftMiddleProximal, value * 90f, Vector3.right);
                    SetBoneRotation(HumanBodyBones.LeftMiddleIntermediate, value * 90f, Vector3.right);
                    SetBoneRotation(HumanBodyBones.LeftMiddleDistal, value * 90f, Vector3.right);
                }
            }
        }

        /// <summary>
        /// Curls the individual finger/thumb
        /// </summary>
        public float LeftRingCurl
        {
            get { return _FingerCurls[3]; }

            set
            {
                if (_FingerCurls[3] != value)
                {
                    _FingerCurls[3] = value;
                    SetBoneRotation(HumanBodyBones.LeftRingProximal, value * 90f, Vector3.right);
                    SetBoneRotation(HumanBodyBones.LeftRingIntermediate, value * 90f, Vector3.right);
                    SetBoneRotation(HumanBodyBones.LeftRingDistal, value * 90f, Vector3.right);
                }
            }
        }

        /// <summary>
        /// Curls the individual finger/thumb
        /// </summary>
        public float LeftLittleCurl
        {
            get { return _FingerCurls[4]; }

            set
            {
                if (_FingerCurls[4] != value)
                {
                    _FingerCurls[4] = value;
                    SetBoneRotation(HumanBodyBones.LeftLittleProximal, value * 90f, Vector3.right);
                    SetBoneRotation(HumanBodyBones.LeftLittleIntermediate, value * 90f, Vector3.right);
                    SetBoneRotation(HumanBodyBones.LeftLittleDistal, value * 90f, Vector3.right);
                }
            }
        }

        /// <summary>
        /// Simple factoring for determining if we should curl the fingers or not
        /// </summary>
        public float _RightCurl = 0f;
        public float RightCurl
        {
            get { return _RightCurl; }

            set
            {
                _RightCurl = value;

                RightThumbCurl = value;
                RightIndexCurl = value;
                RightMiddleCurl = value;
                RightRingCurl = value;
                RightLittleCurl = value;
            }
        }

        /// <summary>
        /// Curls the individual finger/thumb
        /// </summary>
        public float RightThumbCurl
        {
            get { return _FingerCurls[5]; }

            set
            {
                if (_FingerCurls[5] != value)
                {
                    _FingerCurls[5] = value;
                    SetBoneRotation(HumanBodyBones.RightThumbProximal, value * 90f * 0.2f, Vector3.right + Vector3.up);
                    SetBoneRotation(HumanBodyBones.RightThumbIntermediate, value * 90f * 0.2f, Vector3.right + Vector3.up);
                    SetBoneRotation(HumanBodyBones.RightThumbDistal, value * 90f, Vector3.right + Vector3.up);
                }
            }
        }

        /// <summary>
        /// Curls the individual finger/thumb
        /// </summary>
        public float RightIndexCurl
        {
            get { return _FingerCurls[6]; }

            set
            {
                if (_FingerCurls[6] != value)
                {
                    _FingerCurls[6] = value;
                    SetBoneRotation(HumanBodyBones.RightIndexProximal, value * 90f, Vector3.right);
                    SetBoneRotation(HumanBodyBones.RightIndexIntermediate, value * 90f, Vector3.right);
                    SetBoneRotation(HumanBodyBones.RightIndexDistal, value * 90f, Vector3.right);
                }
            }
        }

        /// <summary>
        /// Curls the individual finger/thumb
        /// </summary>
        public float RightMiddleCurl
        {
            get { return _FingerCurls[7]; }

            set
            {
                if (_FingerCurls[7] != value)
                {
                    _FingerCurls[7] = value;
                    SetBoneRotation(HumanBodyBones.RightMiddleProximal, value * 90f, Vector3.right);
                    SetBoneRotation(HumanBodyBones.RightMiddleIntermediate, value * 90f, Vector3.right);
                    SetBoneRotation(HumanBodyBones.RightMiddleDistal, value * 90f, Vector3.right);
                }
            }
        }

        /// <summary>
        /// Curls the individual finger/thumb
        /// </summary>
        public float RightRingCurl
        {
            get { return _FingerCurls[8]; }

            set
            {
                if (_FingerCurls[8] != value)
                {
                    _FingerCurls[8] = value;
                    SetBoneRotation(HumanBodyBones.RightRingProximal, value * 90f, Vector3.right);
                    SetBoneRotation(HumanBodyBones.RightRingIntermediate, value * 90f, Vector3.right);
                    SetBoneRotation(HumanBodyBones.RightRingDistal, value * 90f, Vector3.right);
                }
            }
        }

        /// <summary>
        /// Curls the individual finger/thumb
        /// </summary>
        public float RightLittleCurl
        {
            get { return _FingerCurls[9]; }

            set
            {
                if (_FingerCurls[9] != value)
                {
                    _FingerCurls[9] = value;
                    SetBoneRotation(HumanBodyBones.RightLittleProximal, value * 90f, Vector3.right);
                    SetBoneRotation(HumanBodyBones.RightLittleIntermediate, value * 90f, Vector3.right);
                    SetBoneRotation(HumanBodyBones.RightLittleDistal, value * 90f, Vector3.right);
                }
            }
        }

        /// <summary>
        /// Array that tracks the curl value for the individual fingers. Order is:
        /// l-thumb, l-index, l-middle, l-ring, l-little
        /// r-thumb, r-index, r-middle, r-ring, r-little
        /// </summary>
        public float[] _FingerCurls = new float[10];

        /// <summary>
        /// Serialized rotations to apply to the bones based on index. Note that these
        /// modifiers correlate 1:1 to the mBones list.
        /// </summary>
        public List<FingerPoseMotorBone> _BoneInfo = new List<FingerPoseMotorBone>();

        /// <summary>
        /// Default constructor
        /// </summary>
        public FingerPoseMotor()
            : base()
        {
        }

        /// <summary>
        /// Skeleton constructor
        /// </summary>
        /// <param name="rSkeleton">Skeleton the motor is driving</param>
        public FingerPoseMotor(BoneController rSkeleton)
            : base(rSkeleton)
        {
        }

        /// <summary>
        /// We can't load the bones in InvalidateBones because that's called from
        /// as seperate thread sometimes. So, we'll load the actual bone
        /// references (if needed) in a seperate function.
        /// </summary>
        public override void LoadBones()
        {
            mBones.Clear();

            AddBone(HumanBodyBones.LeftThumbProximal);
            AddBone(HumanBodyBones.LeftThumbIntermediate);
            AddBone(HumanBodyBones.LeftThumbDistal);
            AddBone(HumanBodyBones.LeftIndexProximal);
            AddBone(HumanBodyBones.LeftIndexIntermediate);
            AddBone(HumanBodyBones.LeftIndexDistal);
            AddBone(HumanBodyBones.LeftMiddleProximal);
            AddBone(HumanBodyBones.LeftMiddleIntermediate);
            AddBone(HumanBodyBones.LeftMiddleDistal);
            AddBone(HumanBodyBones.LeftRingProximal);
            AddBone(HumanBodyBones.LeftRingIntermediate);
            AddBone(HumanBodyBones.LeftRingDistal);
            AddBone(HumanBodyBones.LeftLittleProximal);
            AddBone(HumanBodyBones.LeftLittleIntermediate);
            AddBone(HumanBodyBones.LeftLittleDistal);

            AddBone(HumanBodyBones.RightThumbProximal);
            AddBone(HumanBodyBones.RightThumbIntermediate);
            AddBone(HumanBodyBones.RightThumbDistal);
            AddBone(HumanBodyBones.RightIndexProximal);
            AddBone(HumanBodyBones.RightIndexIntermediate);
            AddBone(HumanBodyBones.RightIndexDistal);
            AddBone(HumanBodyBones.RightMiddleProximal);
            AddBone(HumanBodyBones.RightMiddleIntermediate);
            AddBone(HumanBodyBones.RightMiddleDistal);
            AddBone(HumanBodyBones.RightRingProximal);
            AddBone(HumanBodyBones.RightRingIntermediate);
            AddBone(HumanBodyBones.RightRingDistal);
            AddBone(HumanBodyBones.RightLittleProximal);
            AddBone(HumanBodyBones.RightLittleIntermediate);
            AddBone(HumanBodyBones.RightLittleDistal);

            while (_BoneInfo.Count < mBones.Count)
            {
                _BoneInfo.Add(new FingerPoseMotorBone());
            }

            // Initialize the rotations with the curl values
            SetBoneRotation(HumanBodyBones.LeftThumbProximal, _FingerCurls[0] * 90f * 0.2f, Vector3.right - Vector3.up);
            SetBoneRotation(HumanBodyBones.LeftThumbIntermediate, _FingerCurls[0] * 90f * 0.2f, Vector3.right - Vector3.up);
            SetBoneRotation(HumanBodyBones.LeftThumbDistal, _FingerCurls[0] * 90f, Vector3.right - Vector3.up);

            SetBoneRotation(HumanBodyBones.LeftIndexProximal, _FingerCurls[1] * 90f, Vector3.right);
            SetBoneRotation(HumanBodyBones.LeftIndexIntermediate, _FingerCurls[1] * 90f, Vector3.right);
            SetBoneRotation(HumanBodyBones.LeftIndexDistal, _FingerCurls[1] * 90f, Vector3.right);

            SetBoneRotation(HumanBodyBones.LeftMiddleProximal, _FingerCurls[2] * 90f, Vector3.right);
            SetBoneRotation(HumanBodyBones.LeftMiddleIntermediate, _FingerCurls[2] * 90f, Vector3.right);
            SetBoneRotation(HumanBodyBones.LeftMiddleDistal, _FingerCurls[2] * 90f, Vector3.right);

            SetBoneRotation(HumanBodyBones.LeftRingProximal, _FingerCurls[3] * 90f, Vector3.right);
            SetBoneRotation(HumanBodyBones.LeftRingIntermediate, _FingerCurls[3] * 90f, Vector3.right);
            SetBoneRotation(HumanBodyBones.LeftRingDistal, _FingerCurls[3] * 90f, Vector3.right);

            SetBoneRotation(HumanBodyBones.LeftLittleProximal, _FingerCurls[4] * 90f, Vector3.right);
            SetBoneRotation(HumanBodyBones.LeftLittleIntermediate, _FingerCurls[4] * 90f, Vector3.right);
            SetBoneRotation(HumanBodyBones.LeftLittleDistal, _FingerCurls[4] * 90f, Vector3.right);

            SetBoneRotation(HumanBodyBones.RightThumbProximal, _FingerCurls[5] * 90f * 0.2f, Vector3.right + Vector3.up);
            SetBoneRotation(HumanBodyBones.RightThumbIntermediate, _FingerCurls[5] * 90f * 0.2f, Vector3.right + Vector3.up);
            SetBoneRotation(HumanBodyBones.RightThumbDistal, _FingerCurls[5] * 90f, Vector3.right + Vector3.up);

            SetBoneRotation(HumanBodyBones.RightIndexProximal, _FingerCurls[6] * 90f, Vector3.right);
            SetBoneRotation(HumanBodyBones.RightIndexIntermediate, _FingerCurls[6] * 90f, Vector3.right);
            SetBoneRotation(HumanBodyBones.RightIndexDistal, _FingerCurls[6] * 90f, Vector3.right);

            SetBoneRotation(HumanBodyBones.RightMiddleProximal, _FingerCurls[7] * 90f, Vector3.right);
            SetBoneRotation(HumanBodyBones.RightMiddleIntermediate, _FingerCurls[7] * 90f, Vector3.right);
            SetBoneRotation(HumanBodyBones.RightMiddleDistal, _FingerCurls[7] * 90f, Vector3.right);

            SetBoneRotation(HumanBodyBones.RightRingProximal, _FingerCurls[8] * 90f, Vector3.right);
            SetBoneRotation(HumanBodyBones.RightRingIntermediate, _FingerCurls[8] * 90f, Vector3.right);
            SetBoneRotation(HumanBodyBones.RightRingDistal, _FingerCurls[8] * 90f, Vector3.right);

            SetBoneRotation(HumanBodyBones.RightLittleProximal, _FingerCurls[9] * 90f, Vector3.right);
            SetBoneRotation(HumanBodyBones.RightLittleIntermediate, _FingerCurls[9] * 90f, Vector3.right);
            SetBoneRotation(HumanBodyBones.RightLittleDistal, _FingerCurls[9] * 90f, Vector3.right);

            // Reset the invalidation flag
            mIsValid = true;
        }

        /// <summary>
        /// Sets the bone rotation for a finger
        /// </summary>
        /// <param name="rBoneID"></param>
        /// <param name="rAngle"></param>
        /// <param name="rAxis"></param>
        public void SetBoneRotation(HumanBodyBones rBoneID, float rAngle, Vector3 rAxis)
        {
            int rIndex = GetBoneIndex(rBoneID);
            SetBoneRotation(rIndex, rAngle, rAxis);
        }

        /// <summary>
        /// Sets the bone rotation for a finger
        /// </summary>
        /// <param name="rBoneID"></param>
        /// <param name="rAngle"></param>
        /// <param name="rAxis"></param>
        public void SetBoneRotation(int rIndex, float rAngle, Vector3 rAxis)
        {
            // If we don't have the right bones, stop
            if (rIndex < 0 || rIndex >= mBones.Count) { return; }

            // Ensure we have the right amount of bone supports
            while (_BoneInfo.Count < mBones.Count)
            {
                _BoneInfo.Add(new FingerPoseMotorBone());
            }

            // Update the modifier
            FingerPoseMotorBone lModifier = _BoneInfo[rIndex];
            lModifier.IsDirty = true;
            lModifier.Swing = Quaternion.AngleAxis(rAngle, rAxis);
            lModifier.Twist = Quaternion.identity;

            _BoneInfo[rIndex] = lModifier;
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

            // Ensure we have the right amount of bone supports
            while (_BoneInfo.Count < mBones.Count)
            {
                _BoneInfo.Add(new FingerPoseMotorBone());
            }

            // If it's time to update, determine the positions we need to be
            // at and lerp towards them.
            if (rUpdate)
            {
                // Apply the pose we know about
                for (int i = 0; i < mBones.Count; i++)
                {
                    BoneControllerBone lBone = mBones[i];
                    if (lBone == null) { continue; }

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
                    float lWeight = (i < 15 ? _LeftWeight : _RightWeight);
                    Quaternion lSwingTarget = Quaternion.Lerp(lLocalSwing, _BoneInfo[i].Swing, _Weight * lWeight * _BoneInfo[i].Weight);
                    Quaternion lTwistTarget = Quaternion.Lerp(lLocalTwist, _BoneInfo[i].Twist, _Weight * lWeight * _BoneInfo[i].Weight);

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

        /// <summary>
        /// Givent a bone id, attempt to find it. If
        /// we do, we'll update our structures with the new info so
        /// we don't have to look up again
        /// </summary>
        private int AddBone(HumanBodyBones rBoneID)
        {
            int lIndex = -1;
            BoneControllerBone lBone = null;

            // See if we can  find a matching bone
            lBone = Skeleton.GetBone(rBoneID) as BoneControllerBone;
            if (lBone != null)
            {
                // Grab the index
                lIndex = mBones.Count;

                // Add the bone and the index
                mBones.Add(lBone);

                // If we don't have a modifier, add one
                while (_BoneInfo.Count <= lIndex)
                {
                    _BoneInfo.Add(new FingerPoseMotorBone());
                }
            }

            return lIndex;
        }

        // **************************************************************************************************
        // Following properties and function only valid while editing
        // **************************************************************************************************

#if UNITY_EDITOR

        // Determine if we show the finter details
        private bool mShowLeftDetails = false;
        private bool mShowRightDetails = false;

#endif

        /// <summary>
        /// Render a unique inspector
        /// </summary>
        /// <returns></returns>
        public override bool OnInspectorGUI(List<BoneControllerBone> rSelectedBones)
        {
            bool lIsDirty = false;

#if UNITY_EDITOR

            GUILayout.Space(5);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(new GUIContent("Left Weight", "Weight of the motor for the left hand"));
            float lNewLeftWeight = EditorGUILayout.Slider(_LeftWeight, 0, 1);
            EditorGUILayout.EndHorizontal();

            if (lNewLeftWeight != _LeftWeight)
            {
                lIsDirty = true;
                LeftWeight = lNewLeftWeight;
            }

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(new GUIContent("Left Hand", "Controls the curling of the left hand"));
            float lNewLeftCurl = EditorGUILayout.Slider(_LeftCurl, 0, 1);
            EditorGUILayout.EndHorizontal();

            if (lNewLeftCurl != _LeftCurl)
            {
                lIsDirty = true;
                LeftCurl = lNewLeftCurl;
            }

            EditorGUI.indentLevel++;
            mShowLeftDetails = EditorGUILayout.Foldout(mShowLeftDetails, new GUIContent("Show finger controls"));

            if (mShowLeftDetails)
            {
                bool lIsFingerDirty = OnInspectorFingerGUI(0, "Thumb");
                if (lIsFingerDirty) { lIsDirty = true; }

                lIsFingerDirty = OnInspectorFingerGUI(1, "Index");
                if (lIsFingerDirty) { lIsDirty = true; }

                lIsFingerDirty = OnInspectorFingerGUI(2, "Middle");
                if (lIsFingerDirty) { lIsDirty = true; }

                lIsFingerDirty = OnInspectorFingerGUI(3, "Ring");
                if (lIsFingerDirty) { lIsDirty = true; }

                lIsFingerDirty = OnInspectorFingerGUI(4, "Little");
                if (lIsFingerDirty) { lIsDirty = true; }
            }

            EditorGUI.indentLevel--;

            GUILayout.Space(5);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(new GUIContent("Right Weight", "Weight of the motor for the right hand"));
            float lNewRightWeight = EditorGUILayout.Slider(_RightWeight, 0, 1);
            EditorGUILayout.EndHorizontal();

            if (lNewRightWeight != _RightWeight)
            {
                lIsDirty = true;
                RightWeight = lNewRightWeight;
            }

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(new GUIContent("Right Hand", "Controls the curling of the right hand"));
            float lNewRightCurl = EditorGUILayout.Slider(_RightCurl, 0, 1);
            EditorGUILayout.EndHorizontal();

            if (lNewRightCurl != _RightCurl)
            {
                lIsDirty = true;
                RightCurl = lNewRightCurl;
            }

            EditorGUI.indentLevel++;
            mShowRightDetails = EditorGUILayout.Foldout(mShowRightDetails, new GUIContent("Show finger controls"));

            if (mShowRightDetails)
            {
                bool lIsFingerDirty = OnInspectorFingerGUI(5, "Thumb");
                if (lIsFingerDirty) { lIsDirty = true; }

                lIsFingerDirty = OnInspectorFingerGUI(6, "Index");
                if (lIsFingerDirty) { lIsDirty = true; }

                lIsFingerDirty = OnInspectorFingerGUI(7, "Middle");
                if (lIsFingerDirty) { lIsDirty = true; }

                lIsFingerDirty = OnInspectorFingerGUI(8, "Ring");
                if (lIsFingerDirty) { lIsDirty = true; }

                lIsFingerDirty = OnInspectorFingerGUI(9, "Little");
                if (lIsFingerDirty) { lIsDirty = true; }
            }

            EditorGUI.indentLevel--;

#endif

            return lIsDirty;
        }

        /// <summary>
        /// Helper function for rendering out the finger UI
        /// </summary>
        /// <returns></returns>
        private bool OnInspectorFingerGUI(int rFingerIndex, string rName)
        {
            bool lIsDirty = false;

#if UNITY_EDITOR

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(new GUIContent(rName, "Controls the curling of the " + rName.ToLower() + ". Values are from 0 to 1."));
            float lNewCurl = EditorGUILayout.Slider(_FingerCurls[rFingerIndex], 0, 1);
            EditorGUILayout.EndHorizontal();

            if (lNewCurl != _FingerCurls[rFingerIndex])
            {
                switch (rFingerIndex)
                {
                    case 0:
                        LeftThumbCurl = lNewCurl;
                        break;

                    case 1:
                        LeftIndexCurl = lNewCurl;
                        break;

                    case 2:
                        LeftMiddleCurl = lNewCurl;
                        break;

                    case 3:
                        LeftRingCurl = lNewCurl;
                        break;

                    case 4:
                        LeftLittleCurl = lNewCurl;
                        break;

                    case 5:
                        RightThumbCurl = lNewCurl;
                        break;

                    case 6:
                        RightIndexCurl = lNewCurl;
                        break;

                    case 7:
                        RightMiddleCurl = lNewCurl;
                        break;

                    case 8:
                        RightRingCurl = lNewCurl;
                        break;

                    case 9:
                        RightLittleCurl = lNewCurl;
                        break;
                }

                lIsDirty = true;
            }

#endif

            return lIsDirty;
        }

        // ************************************** SUPPORT CLASSES **************************************

        /// <summary>
        /// Contains information about the bone twist and weighting to use when
        /// passing the twist to the next bone
        /// </summary>
        [Serializable]
        public class FingerPoseMotorBone : IKBoneModifier
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
            public FingerPoseMotorBone()
                : base()
            {
            }
        }
    }
}
