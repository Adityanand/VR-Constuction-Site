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
    /// Fixed joint that prevents swing or twist
    /// </summary>
    [Serializable]
    [IKBoneJointNameAttribute("Fixed Swing and Twist")]
    public class FixedSwingJoint : BoneControllerJoint
    {
        /// <summary>
        /// Swing the weld enforces
        /// </summary>
        public Quaternion _Swing = Quaternion.identity;
        public Quaternion Swing
        {
            get { return _Swing; }
            set { _Swing = value; }
        }

        /// <summary>
        /// Twist the weld enforces
        /// </summary>
        public Quaternion _Twist = Quaternion.identity;
        public Quaternion Twist
        {
            get { return _Twist; }
            set { _Twist = value; }
        }

        /// <summary>
        /// Default constructor
        /// </summary>
        public FixedSwingJoint()
            : base()
        {
        }

        /// <summary>
        /// Bone constructor
        /// </summary>
        /// <param name="rBone">Bone the joint is tied to</param>
        public FixedSwingJoint(BoneControllerBone rBone)
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
            rSwing = _Swing;
            rTwist = _Twist;
            return true;
        }

        // ************************************** EDITOR SUPPORT **************************************

        /// <summary>
        /// Allow the constraint to render it's own GUI
        /// </summary>
        /// <returns>Reports if the object's value was changed</returns>
        public override bool OnInspectorConstraintGUI(bool rIsSelected)
        {
            bool lIsDirty = false;

#if UNITY_EDITOR

            GUILayout.Space(5);

            // Determine if the swing is changing
            if (mBone != null)
            {
                Vector3 lSwing = _Swing.eulerAngles;
                Vector3 lNewSwing = InspectorHelper.Vector3Fields("Swing", "Euler angles to swing the bone.", lSwing, true, true, false);
                if (lNewSwing != lSwing)
                {
                    lIsDirty = true;

                    // Grab the amount that was just rotated by based on the current rotation.
                    // We do this so the change is relative to the current swing rotation
                    Vector3 lDeltaRotations = lNewSwing - lSwing;
                    _Swing = _Swing * Quaternion.Euler(lDeltaRotations);
                }

                // Determine if the twist is changing
                float lTwist = Vector3Ext.SignedAngle(Vector3.up, _Twist * Vector3.up, Vector3.forward); 
                float lNewTwist = EditorGUILayout.FloatField("Twist", lTwist);
                if (lNewTwist != lTwist)
                {
                    lIsDirty = true;
                    _Twist = Quaternion.AngleAxis(lNewTwist, Vector3.forward);
                }

                // Reset the values if needed
                if (GUILayout.Button("reset rotation", EditorStyles.miniButton))
                {
                    _Swing = Quaternion.identity;
                    _Twist = Quaternion.identity;
                    lIsDirty = true;
                }

                if (lIsDirty)
                {
                    mBone.SetLocalRotation(_Swing, _Twist, 1f);
                }
            }

#endif

            return lIsDirty;
        }

        /// <summary>
        /// Allow for rendering in the editor
        /// </summary>
        /// <returns></returns>
        public override bool OnSceneConstraintGUI(bool rIsSelected)
        {
            bool lIsDirty = false;

#if UNITY_EDITOR

            IKBoneModifier lModifier = IKBoneModifier.Allocate();
            lModifier.Swing = _Swing;
            lModifier.Twist = _Twist;

            bool lIsSwingDirty = HandlesHelper.JointSwingHandle(mBone, lModifier);
            if (lIsSwingDirty)
            {
                lIsDirty = true;
            }

            bool lIsTwistDirty = HandlesHelper.JointTwistHandle(mBone, lModifier);
            if (lIsTwistDirty)
            {
                lIsDirty = true;
            }

            if (lIsDirty)
            {
                _Swing = lModifier.Swing;
                _Twist = lModifier.Twist;
                mBone.SetLocalRotation(_Swing, _Twist, 1f);
            }

            IKBoneModifier.Release(lModifier);

#endif

            return lIsDirty;
        }
    }
}
