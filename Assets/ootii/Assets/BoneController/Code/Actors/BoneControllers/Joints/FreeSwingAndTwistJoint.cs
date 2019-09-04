using System;
using System.Collections.Generic;
using UnityEngine;
using com.ootii.Geometry;
using com.ootii.Helpers;
using com.ootii.Utilities.Debug;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace com.ootii.Actors.BoneControllers
{
    /// <summary>
    /// The hinge and twist joint allows for rotation around a main
    /// axis and twisting. This represents two degrees of freedom.
    /// </summary>
    [Serializable]
    [IKBoneJointNameAttribute("Free Swing and Twist")]
    public class FreeSwingAndTwistJoint : BoneControllerJoint
    {
        /// <summary>
        /// Due to how quaternions work, swinging a bone introduces some twist
        /// on it's own. If we want to prevent that twist, we can.
        /// </summary>
        public bool _PreventSwingTwisting = false;
        public bool PreventSwingTwisting
        {
            get { return _PreventSwingTwisting; }
            set { _PreventSwingTwisting = value; }
        }

        /// <summary>
        /// Determines if twisting is even enabled. If not, any twist value is removed
        /// </summary>
        public bool _AllowTwist = true;
        public bool AllowTwist
        {
            get { return _AllowTwist; }
            set { _AllowTwist = value; }
        }

        /// <summary>
        /// Determines if we actually limit the twisting (rotation along the forward
        /// direction) of the bone
        /// </summary>
        public bool _LimitTwist = true;
        public bool LimitTwist
        {
            get { return _LimitTwist; }
            set { _LimitTwist = value; }
        }

        /// <summary>
        /// Defines the min angle the bone can reach while twisting (-180 to 0)
        /// </summary>
        public float _MinTwistAngle = -20f;
        public override float MinTwistAngle
        {
            get { return _MinTwistAngle; }
            set { _MinTwistAngle = value; }
        }

        /// <summary>
        /// Defines the max angle the bone can reach while twisting (0 to 180)
        /// </summary>
        public float _MaxTwistAngle = 20f;
        public override float MaxTwistAngle
        {
            get { return _MaxTwistAngle; }
            set { _MaxTwistAngle = value; }
        }

        /// <summary>
        /// Default constructor
        /// </summary>
        public FreeSwingAndTwistJoint()
            : base()
        {
            //Log.ConsoleWrite("SwingPointAndTwistJoint.Constructor");
        }

        /// <summary>
        /// Bone constructor
        /// </summary>
        /// <param name="rBone">Bone the joint is tied to</param>
        public FreeSwingAndTwistJoint(BoneControllerBone rBone)
            : base(rBone)
        {
        }

        /// <summary>
        /// Apply any rotational limits to the local rotation so it
        /// meets the constraints of this bone type
        /// </summary>
        /// <param name="rBone">Bone being processed</param>
        /// <param name="rRotation">Target local rotation of the bone to be modified</param>
        public override bool ApplyLimits(ref Quaternion rSwing, ref Quaternion rTwist)
        {
            // We may need to get rid of the twisting associated with the swing. 
            if (_PreventSwingTwisting) 
            {
                // Create a longitudinal axis based on the initial bone
                // direction and the current rotation. This is the "local" direction
                // of the bone relative to the bind position.
                Vector3 lTargetBoneForward = rSwing * Vector3.forward;

                // If we are preserving the the automatic twisting, we can remove it
                // by grabing the simple rotation from the start position to the new position
                rSwing = Quaternion.FromToRotation(Vector3.forward, lTargetBoneForward);
            }

            // Only process the twist if it's allowed
            if (_AllowTwist)
            {
                // Test if we should limit the twist
                if (_LimitTwist && (_MinTwistAngle > -180 || _MaxTwistAngle < 180) && !rTwist.IsIdentity())
                {
                    float lTwistAngle = Vector3Ext.SignedAngle(Vector3.up, rTwist * Vector3.up, Vector3.forward);

                    // Force the angle if it's exceeeded.
                    if (lTwistAngle > _MaxTwistAngle)
                    {
                        rTwist = Quaternion.AngleAxis(_MaxTwistAngle, Vector3.forward);
                    }
                    else if (lTwistAngle < _MinTwistAngle)
                    {
                        rTwist = Quaternion.AngleAxis(_MinTwistAngle, Vector3.forward);
                    }
                    else
                    {
                        rTwist = Quaternion.AngleAxis(lTwistAngle, Vector3.forward);
                    }
                }
            }
            else
            {
                rTwist = Quaternion.identity;
            }

            return true;
        }

        // ************************************** EDITOR SUPPORT **************************************

        /// <summary>
        /// Allow the joint to render it's own GUI. This GUI is used
        /// for displaying and manipulating the constraints of the joint.
        /// </summary>
        /// <returns>Reports if the object's value was changed</returns>
        public override bool OnInspectorConstraintGUI(bool rIsSelected)
        {
            bool lIsDirty = false;

#if UNITY_EDITOR

            GUILayout.Space(5);

            bool lNewPreventSwingTwisting = EditorGUILayout.Toggle(new GUIContent("Prevent Swing Twisting", "When swinging, a natural twist occurs. This can prevent that twist."), _PreventSwingTwisting);
            if (lNewPreventSwingTwisting != _PreventSwingTwisting)
            {
                lIsDirty = true;
                _PreventSwingTwisting = lNewPreventSwingTwisting;
            }

            GUILayout.Space(5);

            bool lNewAllowTwist = EditorGUILayout.Toggle(new GUIContent("Allow Twist", "Determines if we allow the bone to twist."), _AllowTwist);
            if (lNewAllowTwist != _AllowTwist)
            {
                lIsDirty = true;
                _AllowTwist = lNewAllowTwist;
            }

            if (_AllowTwist)
            {
                bool lNewLimitTwist = EditorGUILayout.Toggle(new GUIContent("Limit Twist", "Determines if we use the limits set below."), _LimitTwist);
                if (lNewLimitTwist != _LimitTwist)
                {
                    lIsDirty = true;
                    _LimitTwist = lNewLimitTwist;
                }

                if (_LimitTwist)
                {
                    GUILayout.BeginVertical("Twist Limits", GUI.skin.window);

                    float lNewMinTwist = Mathf.Clamp(EditorGUILayout.FloatField(new GUIContent("Min Twist Angle", "Minimum angle (-180 to 0) that the twist can make."), _MinTwistAngle), -180, 0);
                    if (lNewMinTwist != _MinTwistAngle)
                    {
                        lIsDirty = true;
                        _MinTwistAngle = lNewMinTwist;
                    }

                    float lNewMaxTwist = Mathf.Clamp(EditorGUILayout.FloatField(new GUIContent("Max Twist Angle", "Maximum angle (0 to 180) that the twist can make."), _MaxTwistAngle), 0, 180);
                    if (lNewMaxTwist != _MaxTwistAngle)
                    {
                        lIsDirty = true;
                        _MaxTwistAngle = lNewMaxTwist;
                    }

                    GUILayout.EndVertical();
                }
            }

            // Ensure we update the joint based on any new limits
            if (lIsDirty)
            {
                ApplyLimits(ref mBone._Swing, ref mBone._Twist);
            }

#endif

            return lIsDirty;
        }

        /// <summary>
        /// Allows us to render joint info into the scene. This GUI is
        /// used for displaying and manipulating the joint itself.
        /// </summary>
        /// <returns></returns>
        public override bool OnSceneConstraintGUI(bool rIsSelected)
        {
            bool lIsDirty = false;

#if UNITY_EDITOR

            //Color lLineColor = new Color(0f, 1f, 1f, 0.25f);
            //Color lFillColor = new Color(0f, 1f, 1f, 0.05f);
            Color lHandleColor = Handles.color;

            // Render out the twist limit
            if (_AllowTwist && _LimitTwist)
            {
                bool lIsLimitsDirty = HandlesHelper.JointTwistLimitsHandle(mBone, ref _MinTwistAngle, ref _MaxTwistAngle);
                if (lIsLimitsDirty)
                {
                    lIsDirty = true;
                }
            }

            // Reset
            Handles.color = lHandleColor;

#endif

            return lIsDirty;
        }

        /// <summary>
        /// Allow the joint to render it's own GUI. This GUI is used
        /// for displaying and manipulating the joint itself.
        /// </summary>
        /// <returns>Reports if the object's value was changed</returns>
        public override bool OnInspectorManipulatorGUI(IKBoneModifier rModifier)
        {

#if UNITY_EDITOR

            // Determine if the swing is changing
            if (mBone != null)
            {
                // Determine if the swing is changing
                Vector3 lSwing = rModifier.Swing.eulerAngles;
                Vector3 lNewSwing = InspectorHelper.Vector3Fields("Swing", "Euler angles to swing the bone.", lSwing, true, true, false);
                if (lNewSwing != lSwing)
                {
                    // Grab the amount that was just rotated by based on the current rotation.
                    // We do this so the change is relative to the current swing rotation
                    Vector3 lDeltaRotations = lNewSwing - lSwing;
                    rModifier.Swing = rModifier.Swing * Quaternion.Euler(lDeltaRotations);

                    rModifier.IsDirty = true;
                }

                // Determine if the twist is changing
                float lTwist = Vector3Ext.SignedAngle(Vector3.up, mBone.Twist * Vector3.up, Vector3.forward);
                float lNewTwist = EditorGUILayout.FloatField("Twist", lTwist);
                if (_AllowTwist && lNewTwist != lTwist)
                {
                    rModifier.Twist = Quaternion.AngleAxis(lNewTwist, Vector3.forward);
                    rModifier.IsDirty = true;
                }

                // Reset the values if needed
                if (GUILayout.Button("reset rotation", EditorStyles.miniButton))
                {
                    rModifier.Swing = Quaternion.identity;
                    rModifier.Twist = (_AllowTwist ? Quaternion.identity : mBone.Twist);
                    rModifier.IsDirty = true;

                    mBone._Transform.localRotation = mBone._BindRotation;
                }

                if (rModifier.IsDirty)
                {
                    ApplyLimits(ref rModifier.Swing, ref rModifier.Twist);
                }
            }             

#endif

            return rModifier.IsDirty;
        }

        /// <summary>
        /// Allows us to render joint info into the scene. This GUI is
        /// used for displaying and manipulating the joint itself.
        /// </summary>
        /// <returns>Reports if the object's value was changed</returns>
        public override bool OnSceneManipulatorGUI(IKBoneModifier rModifier)
        {

#if UNITY_EDITOR

            //Quaternion lSwing = mBone.Swing;
            //Quaternion lTwist = mBone.Twist;

            bool lIsSwingDirty = HandlesHelper.JointSwingHandle(mBone, rModifier);
            if (lIsSwingDirty)
            {
                //rModifier.Swing = lSwing;
                rModifier.IsDirty = true;
            }

            if (_AllowTwist)
            {
                bool lIsTwistDirty = HandlesHelper.JointTwistHandle(mBone, rModifier);
                if (lIsTwistDirty)
                {
                    //rModifier.Twist = lTwist;
                    rModifier.IsDirty = true;
                }
            }

            if (rModifier.IsDirty)
            {
                ApplyLimits(ref rModifier.Swing, ref rModifier.Twist);
            }

#endif

            return rModifier.IsDirty;
        }
    }
}
