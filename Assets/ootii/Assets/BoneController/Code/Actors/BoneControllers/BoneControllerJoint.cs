using System;
using UnityEngine;
using com.ootii.Base;
using com.ootii.Utilities.Debug;

namespace com.ootii.Actors.BoneControllers
{
    /// <summary>
    /// Base class that defines the bone rotation capabilities.
    /// </summary>
    [Serializable]
    public class BoneControllerJoint : BaseObject
    {
        /// <summary>
        /// Underlying bone this joint manipulates. We don't
        /// serialize it since we don't want a circular reference
        /// during serialization.
        /// </summary>
        [NonSerialized]
        protected BoneControllerBone mBone;
        public BoneControllerBone Bone
        {
            get { return mBone; } 
            set { mBone = value; }
        }

        /// <summary>
        /// The axis that joint will prefer to swing around. In some cases,
        /// it is the only axis is can use.
        /// </summary>
        public Vector3 _UpAxis = Vector3.forward;
        public Vector3 UpAxis
        {
            get { return _UpAxis; }
            set { _UpAxis = value; }
        }

        /// <summary>
        /// Default constructor
        /// </summary>
        public BoneControllerJoint() : 
            base()
        {
        }

        /// <summary>
        /// Default constructor
        /// </summary>
        public BoneControllerJoint(BoneControllerBone rBone) :
            base()
        {
            Initialize(rBone);
        }

        /// <summary>
        /// Associates the bone and allows for any setup
        /// </summary>
        /// <param name="rBone">Bone the joint is tied to</param>
        public virtual void Initialize(BoneControllerBone rBone)
        {
            mBone = rBone;
        }

        /// <summary>
        /// Defines the min angle the bone can reach while twisting (-180 to 0)
        /// </summary>
        public virtual float MinTwistAngle
        {
            get { return -180f; }
            set { }
        }

        /// <summary>
        /// </summary>
        public virtual float MaxTwistAngle
        {
            get { return 180f; }
            set { }
        }

        /// <summary>
        /// Determines how close the bone is from reaching its twist limit. Values
        /// are from -1 (at min limit) to 0 (at bind angle) to 1 (at max limit).
        /// </summary>
        public virtual float GetTwistStress()
        {
            return GetTwistStress(mBone.Twist);
        }

        /// <summary>
        /// Determines how close the bone is from reaching its twist limit. Values
        /// are from -1 (at min limit) to 0 (at bind angle) to 1 (at max limit).
        /// </summary>
        public virtual float GetTwistStress(Quaternion rLocalTwist)
        {
            return 0f;
        }

        /// <summary>
        /// Apply any rotational limits to the local rotation so it
        /// meets the constraints of this bone type
        /// </summary>
        /// <param name="rBone">Bone being processed</param>
        /// <param name="rRotation">Target local rotation of the bone to be modified</param>
        public virtual void ApplyLimits(ref Quaternion rRotation)
        {
        }

        /// <summary>
        /// Apply any rotational limits to the local rotation so it
        /// meets the constraints of this bone type
        /// </summary>
        /// <param name="rBone">Bone being processed</param>
        /// <returns>Determines if the rotations are within limits</returns>
        public virtual bool ApplyLimits(ref Quaternion rSwing, ref Quaternion rTwist)
        {
            return true;
        }

        // ************************************** EDITOR SUPPORT **************************************

        /// <summary>
        /// Raised when the bone is selected in the editor
        /// </summary>
        public virtual void OnEnable()
        {
        }

        /// <summary>
        /// Raised when the bone is deselected in the editor
        /// </summary>
        public virtual void OnDisable()
        {
        }

        /// <summary>
        /// Allow the joint to render it's own GUI. This GUI is used
        /// for displaying and manipulating the constraints of the joint.
        /// </summary>
        /// <returns>Reports if the object's value was changed</returns>
        public virtual bool OnInspectorConstraintGUI(bool rIsSelected)
        {
            return false;
        }
    
        /// <summary>
        /// Allows us to render constraint info into the scene. This GUI is
        /// used for displaying and manipulating the constraints of the joint.
        /// </summary>
        /// <returns>Reports if the object's value was changed</returns>
        public virtual bool OnSceneConstraintGUI(bool rIsSelected)
        {
            return false;
        }

        /// <summary>
        /// Allow the joint to render it's own GUI. This GUI is used
        /// for displaying and manipulating the joint itself.
        /// </summary>
        /// <returns>Reports if the object's value was changed</returns>
        public virtual bool OnInspectorManipulatorGUI(IKBoneModifier rModifier)
        {
            return false;
        }

        /// <summary>
        /// Allows us to render joint info into the scene. This GUI is
        /// used for displaying and manipulating the joint itself.
        /// </summary>
        /// <returns>Reports if the object's value was changed</returns>
        public virtual bool OnSceneManipulatorGUI(IKBoneModifier rModifier)
        {
            return false;
        }

        // ************************************** STATIC PROPERTIES **************************************

        /// <summary>
        /// Non-selected style to use with the name when showing editors
        /// </summary>
        private static GUIStyle sRowStyle = null;
        public static GUIStyle RowStyle
        {
            get
            {
                if (sRowStyle == null)
                {
                    sRowStyle = new GUIStyle();
                    sRowStyle.border = new RectOffset(1, 1, 1, 1);
                    sRowStyle.margin = new RectOffset(0, 0, 0, 0);
                    sRowStyle.padding = new RectOffset(0, 0, 0, 0);
                }

                return sRowStyle;
            }
        }

        /// <summary>
        /// Selected style to use with the name when showing editors
        /// </summary>
        private static GUIStyle sSelectedRowStyle = null;
        public static GUIStyle SelectedRowStyle
        {
            get
            {
                if (sSelectedRowStyle == null)
                {
                    sSelectedRowStyle = new GUIStyle();
                    sSelectedRowStyle.normal.background = (Texture2D)Resources.Load<Texture>("IKBorder"); ;
                    sSelectedRowStyle.border = new RectOffset(1, 1, 1, 1);
                    sSelectedRowStyle.margin = new RectOffset(0, 0, 0, 0);
                    sSelectedRowStyle.padding = new RectOffset(0, 0, 0, 0);
                }

                return sSelectedRowStyle;
            }
        }

        /// <summary>
        /// Selector icon for choosing a row
        /// </summary>
        private static Texture sItemSelector = null;
        public static Texture ItemSelector
        {
            get
            {
                if (sItemSelector == null)
                {
                    sItemSelector = Resources.Load<Texture>("IKDot");
                }

                return sItemSelector;
            }
        }
    }
}
