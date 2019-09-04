using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using com.ootii.Base;
using com.ootii.Data.Serializers;
using com.ootii.Geometry;
using com.ootii.Helpers;
using com.ootii.Utilities.Debug;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace com.ootii.Actors.BoneControllers
{
    /// <summary>
    /// Defines a single bone as well as it's limits
    /// and position within the hierarchy.
    /// </summary>
    [Serializable]
    public class BoneControllerBone : IKBone, ISerializationCallbackReceiver
    {
        /// <summary>
        /// Underlying transform this bone represents
        /// </summary>
        public override Transform Transform
        {
            get { return _Transform; }
            
            set 
            {
                if (value != null)
                {
                    if (Name.Length == 0)
                    {
                        Name = value.name;
                        mCleanName = BoneController.CleanBoneName(Name);
                    }

                    if (mParent != null)
                    {
                        if (_Transform != null) { _Transform.parent = null; }
                        if (value != null && value.parent != mParent.Transform) { value.parent = mParent.Transform; }
                    }
                }

                // Transform this bone represents
                _Transform = value;

                // Given the transform and parent, determine the bind information
                InitializeBoneRotations();

                // With the transform set, if this transform has children,
                // we'll need to add a bunch of new bones to the skeleton.
                if (mSkeleton != null)
                {
                    mSkeleton.InitializeBone(this);
                }
            }
        }

        /// <summary>
        /// Joint that represents the base of this bone and is
        /// used to control it's rotation and constraints. We need to serialize
        /// this ourselves since we used derived classes in prefabs.
        /// </summary>
        [NonSerialized]
        public BoneControllerJoint _Joint = null;
        public BoneControllerJoint Joint
        {
            get { return _Joint; }
            
            set 
            {
                if (_Joint != null && value == null) { _Joint.Bone = null; }

                _Joint = value;
                if (_Joint != null && _Joint.Bone != this)
                {
                    _Joint.Initialize(this);
                }
            }
        }

        /// <summary>
        /// Skeleton this bone belongs to. 
        /// 
        /// Note: We still need to add the 'NonSerialized' flag so 
        /// Unity doesn't give the serialization-depth error
        /// </summary>
        [NonSerialized]
        protected BoneController mSkeleton;
        public BoneController Skeleton
        {
            get { return mSkeleton; }
            set { mSkeleton = value; }
        }

        /// <summary>
        /// Bone this bone is a child of
        /// 
        /// Note: We still need to add the 'NonSerialized' flag so 
        /// Unity doesn't give the serialization-depth error
        /// </summary>
        [NonSerialized]
        protected BoneControllerBone mParent;
        public BoneControllerBone Parent
        {
            get { return mParent; }

            set
            {
                mParent = value;

                // Force the Unity parent using the transforms
                if (_Transform != null) 
                {
                    if (mParent == null)
                    {
                        _Transform.parent = null;
                    }
                    else
                    {
                        // Don't reset the transform since we don't want it
                        // to change places in the index. Otherwise, our add
                        // process will be going over the same items
                        if (_Transform.parent != mParent.Transform)
                        {
                            _Transform.parent = mParent.Transform;
                        }
                    }
                }

                // Given the transform and parent, determine the bind information
                InitializeBoneRotations();
            }
        }

        /// <summary>
        /// Quaternion that will rotate a forward facing direction
        /// to a bone-forward facing direction.
        /// </summary>
        public override Quaternion ToBoneForward
        {
            get { return _ToBoneForward; }

            set
            {
                _ToBoneForward = value;
                _ToBoneForwardInv = _ToBoneForward.Conjugate();
            }
        }

        /// <summary>
        /// Primarily used in order to speed up the update process
        /// </summary>
        public Quaternion _ToBoneForwardInv = Quaternion.identity.Conjugate();
        public Quaternion ToBoneForwardInv
        {
            get { return _ToBoneForwardInv; }
        }

        /// <summary>
        /// Children that this bone is the parent of
        /// 
        /// Note: We keep fields public so they are serialized
        /// </summary>
        [NonSerialized]
        protected List<BoneControllerBone> mChildren = new List<BoneControllerBone>();
        public List<BoneControllerBone> Children
        {
            get { return mChildren; }
            set { mChildren = value; }
        }

        /// <summary>
        /// Determines if the bone joint is within the specified limits
        /// </summary>
        protected bool mIsInLimits = true;
        public virtual bool IsInLimits
        {
            get { return mIsInLimits; }
        }

        /// <summary>
        /// Determines if we'll apply joint limits to the bone rotations
        /// </summary>
        protected bool _ApplyLimits = true;
        public virtual bool ApplyLimits
        {
            get { return _ApplyLimits; }
            set { _ApplyLimits = value; }
        }

        /// <summary>
        /// Determines if we'll apply joint limits to the bone rotations this frame
        /// </summary>
        protected bool _ApplyLimitsInFrame = true;
        public virtual bool ApplyLimitsInFrame
        {
            get { return _ApplyLimitsInFrame; }
            set { _ApplyLimitsInFrame = value; }
        }

        /// <summary>
        /// Local rotation that we're attempting to apply. The swing
        /// represents rotation along the axes that are perpendicular
        /// to the direction of the bone. For example, if the bone
        /// is built along the 'y' axis, this is the rotation 
        /// along the x and z axis.
        /// </summary>
        public Quaternion _TargetSwing = Quaternion.identity;
        public Quaternion TargetSwing
        {
            get { return _TargetSwing; }
        }
        
        /// <summary>
        /// Local rotation that we're attempting to apply. The twist
        /// represents rotation along the axis that is the bone direction.
        /// If the bone is built along the 'y' axis, this is the roll of
        /// the bone along that y axis.
        /// </summary>
        public Quaternion _TargetTwist = Quaternion.identity;
        public Quaternion TargetTwist
        {
            get { return _TargetTwist; }
        }

        /// <summary>
        /// Provides the world space rotation that represents the
        /// bone rotation when it was bound to the skeleton
        /// </summary>
        public Quaternion WorldBindRotation
        {
            get { return (mParent != null ? mParent.Transform.rotation : Quaternion.identity) * _BindRotation * _ToBoneForward; }
        }

        /// <summary>
        /// Provides the current world space position that represents the
        /// bone placement when it was bound
        /// </summary>
        public Vector3 WorldBindPosition
        {
            get 
            {
                if (mParent == null)
                {
                    return mSkeleton.gameObject.transform.position + (mSkeleton.gameObject.transform.rotation * _BindPosition);
                }
                else
                {
                    return mParent._Transform.position + (mParent._Transform.rotation * _BindPosition);
                }
            }
        }

        /// <summary>
        /// Grab the end point of the bone in world space
        /// </summary>
        public Vector3 WorldEndPosition
        {
            get
            {
                return _Transform.position + (_Transform.rotation * (_BoneForward * _Length));
            }
        }

        /// <summary>
        /// List of bone modifiers that we're storing and will apply during the update
        /// </summary>
        private List<IKBoneModifier> mModifiers = new List<IKBoneModifier>();

        /// <summary>
        /// Flags that we need to reload the our bones from the skeleton
        /// </summary>
        private bool mIsValid = true;

        /// <summary>
        /// Unity has some interesting quirks. One is that it doesn't like polymorphism.
        /// So, we can't simply serialize the _Joint field when it is a derived class
        /// (like FixedJoint). If it were a SerializedObject we could, but then it
        /// can't be added to prefabs.
        /// 
        /// To get around this, we serialize the joint ourselves.
        /// </summary>
        public string SerializationJoint = "";

#if UNITY_EDITOR

        /// <summary>
        /// Used by the editor to determine if we show the bone directions
        /// </summary>
        protected bool mEditorShowDirections = false;

#endif

        /// <summary>
        /// Default Constructor
        /// </summary>
        public BoneControllerBone()
            : base()
        {
            //Log.ConsoleWrite("BoneControllerBone.Constructor() ");
        }

        /// <summary>
        /// Transform Constructor
        /// </summary>
        /// <param name="rSkeleton">Skeleton the bone is part of</param>
        /// <param name="rTransform">Transform the bone wraps</param>
        public BoneControllerBone(BoneController rSkeleton)
            : base()
        {
            mSkeleton = rSkeleton;
        }

        /// <summary>
        /// Given the transform and parent, generate the bone information we'll use
        /// when rotating and positioning the bone
        /// </summary>
        private void InitializeBoneRotations()
        {
            if (_Transform == null)
            {
                _BindRotation = Quaternion.identity;
                _BindMatrix = Matrix4x4.identity;

                _BoneForward = Vector3.forward;
                _BoneUp = Vector3.up;
                _BoneRight = Vector3.right;

                _ToBoneForward = Quaternion.identity;

                _Length = 0f;
            }
            else
            {
                Transform lParentTransform = null;

                Transform lPrefabTransform = mSkeleton.GetPrefabTransform(_Transform.name);
                if (lPrefabTransform != null)
                {
                    lParentTransform = lPrefabTransform.parent;

                    _BindRotation = lPrefabTransform.localRotation;
                    _BindPosition = lPrefabTransform.localPosition;
                    _BindMatrix = lPrefabTransform.worldToLocalMatrix * (lPrefabTransform.parent != null ? lPrefabTransform.parent.localToWorldMatrix : Matrix4x4.identity);
                }
                else
                {
                    lParentTransform = _Transform.parent;

                    _BindRotation = _Transform.localRotation;
                    _BindPosition = _Transform.localPosition;
                    _BindMatrix = _Transform.worldToLocalMatrix * (mParent != null ? mParent.Transform.localToWorldMatrix : Matrix4x4.identity);
                }

                // Track the world bind rotation so we can get some other values
                Quaternion lBindRotation = (mParent != null ? mParent.Transform.rotation : Quaternion.identity) * _BindRotation;

                // Ensure we have valid bones
                int lChildBoneCount = _Transform.childCount;
                if (lChildBoneCount > 0)
                {
                    // Since we have multiple children, average the info
                    int lValidChildCount = 0;
                    Vector3 lAverageChildPosition = Vector3.zero;
                    for (int i = 0; i < _Transform.childCount; i++)
                    {
                        Transform lChild = _Transform.GetChild(i);
                        if (lChild.localPosition.magnitude < 0.001f) { continue; }
                        if (mSkeleton.gameObject.transform != null && Vector3.Distance(lChild.position, mSkeleton.gameObject.transform.position) < 0.001f) { continue; }
                        if (mSkeleton._RootTransform != null && Vector3.Distance(lChild.position, mSkeleton._RootTransform.position) < 0.001f) { continue; }

                        if (mSkeleton.TestBoneNameFilter(_Transform.GetChild(i).name))
                        {
                            lValidChildCount++;
                            lAverageChildPosition += _Transform.GetChild(i).position;
                        }
                    }

                    lChildBoneCount = lValidChildCount;
                    if (lChildBoneCount > 0)
                    {
                        Vector3 lChildPosition = lAverageChildPosition / (float)lValidChildCount;
                        Vector3 lTargetForward = lChildPosition - _Transform.position;

                        if (float.IsNaN(lTargetForward.x))
                        {
                            lChildPosition = _Transform.GetChild(0).position;
                            lTargetForward = lChildPosition - _Transform.position;
                        }

                        // It's posible the children surround the parent. So, the average
                        // becomes 0. In this case, we grab the first bone and go with it
                        if (lValidChildCount > 1 && lTargetForward.magnitude < 0.01f)
                        {
                            for (int i = 0; i < _Transform.childCount; i++)
                            {
                                if (mSkeleton.TestBoneNameFilter(_Transform.GetChild(i).name))
                                {
                                    lChildPosition = _Transform.GetChild(i).position;
                                    lTargetForward = lChildPosition - _Transform.position;

                                    break;
                                }
                            }
                        }

                        // Find the original bone forward given the current bind rotation and target. This is the
                        // forward that when multiplied by the world bind rotation will result in the target bind pose
                        _BoneForward = lBindRotation.Conjugate() * lTargetForward.normalized;

                        // Set the length
                        _Length = lTargetForward.magnitude;
                    }
                }

                // If there are no children, we've hit a stub (end point)
                if (lChildBoneCount == 0)
                {
                    // Grab the collider on the bone (if there is one)
                    Collider lBaseCollider = _Transform.GetComponent<Collider>();

                    // Go with the parent
                    _BoneForward = (mParent != null ? mParent._BoneForward : mSkeleton.transform.forward);

                    // Default the bone length
                    if (_Length > 0)
                    {
                    }
                    else if (lBaseCollider is CapsuleCollider)
                    {
                        CapsuleCollider lCollider = (CapsuleCollider)lBaseCollider;
                        float lScale = (lCollider.direction == 0 ? _Transform.lossyScale.x : (lCollider.direction == 1 ? _Transform.lossyScale.y : _Transform.lossyScale.z));
                        
                        _Length = lCollider.height * lScale;
                    }
                    else if (lBaseCollider is SphereCollider)
                    {
                        SphereCollider lCollider = (SphereCollider)lBaseCollider;
                        float lScale = _Transform.lossyScale.x;

                        _Length = lCollider.radius * 2f * lScale;
                    }
                    else if (lBaseCollider is BoxCollider)
                    {
                        BoxCollider lCollider = (BoxCollider)lBaseCollider;
                        float lScale = Mathf.Max(_Transform.lossyScale.x, Mathf.Max(_Transform.lossyScale.y, _Transform.lossyScale.z));

                        _Length = lCollider.bounds.size.magnitude * lScale;
                    }
                    else if (mParent != null)
                    {
                        _Length = mParent._Length * 0.2f;
                    }
                    else
                    {
                        _Length = 0.05f;
                    }
                }

                // To find the up, we want to see how the bone is oriented in world space. If it's
                // mostly vertical, the 'up' will be the character's forward. Otherwise, it's the character's up
                //Vector3 lWorldBoneForward = WorldBindRotation * _BoneForward;
                Vector3 lWorldBoneForward = (lParentTransform != null ? lParentTransform.rotation : Quaternion.identity) * _BindRotation * _BoneForward;
                if (Mathf.Abs(lWorldBoneForward.y) > 0.9f)
                {
                    _BoneRight = Vector3.Cross(lBindRotation.Conjugate() * mSkeleton.transform.forward, _BoneForward);
                    _BoneUp = Vector3.Cross(_BoneForward, _BoneRight).normalized;
                }
                else
                {
                    _BoneRight = Vector3.Cross(lBindRotation.Conjugate() * mSkeleton.transform.up, _BoneForward);
                    _BoneUp = Vector3.Cross(_BoneForward, _BoneRight).normalized;
                }

                // Create the quaternion that will be used to rotate from a 'unity forward'
                // direction to a 'bone forward' direction.
                _ToBoneForward = Quaternion.identity.RotationTo(Quaternion.LookRotation(_BoneForward, _BoneUp));
                _ToBoneForwardInv = _ToBoneForward.Conjugate();
            }
        }

        /// <summary>
        /// If any bones are saved, they should be invalidated and re-grabbed
        /// from the skeleton.
        /// </summary>
        public override void InvalidateBones()
        {
            mIsValid = false;
        }

        /// <summary>
        /// We can't load the bones in InvalidateBones because that's called from
        /// a seperate thread sometimes. So, we'll load the actual bone
        /// references (if needed) in this seperate function.
        /// </summary>
        public override void LoadBones()
        {
            if (mSkeleton == null) { return; }

            // Ensure we have a clean name to display
            mCleanName = BoneController.CleanBoneName(Name);

            // Grab the parent
            mParent = (SerializationParentIndex < 0 ? null : mSkeleton.Bones[SerializationParentIndex]);

            // Clear the children and regrab them
            mChildren.Clear();
            for (int i = 0; i < SerializationChildIndexes.Count; i++)
            {
                if (SerializationChildIndexes[i] >= 0)
                {
                    mChildren.Add(mSkeleton.Bones[SerializationChildIndexes[i]]);
                }
            }

            // Reset info on the joint. This happens when we need to reload the bones
            if (_Joint != null) { _Joint.Initialize(this); }

            // Reset the invalidation flag
            mIsValid = true;
        }

        /// <summary>
        /// Get a child bone base dont the transform
        /// </summary>
        /// <param name="rTransform"></param>
        /// <returns></returns>
        public BoneControllerBone GetChild(Transform rTransform)
        {
            for (int i = 0; i < mChildren.Count; i++)
            {
                if (mChildren[i]._Transform == rTransform)
                {
                    return mChildren[i];
                }
            }

            return null;
        }

        /// <summary>
        /// Creates and adds a child to this bone
        /// </summary>
        /// <param name="rTransform"></param>
        /// <returns></returns>
        public BoneControllerBone AddChild()
        {
            BoneControllerBone lChild = new BoneControllerBone(mSkeleton);
            AddChild(lChild, false);

            lChild.Transform = null;

            return lChild;
        }

        /// <summary>
        /// Creates and adds a child to this bone
        /// </summary>
        /// <param name="rTransform"></param>
        /// <returns></returns>
        public BoneControllerBone InsertChild()
        {
            BoneControllerBone lChild = new BoneControllerBone(mSkeleton);
            AddChild(lChild, true);

            lChild.Transform = null;

            return lChild;
        }

        /// <summary>
        /// Creates and adds a child to this bone
        /// </summary>
        /// <param name="rTransform"></param>
        /// <returns></returns>
        public BoneControllerBone AddChild(Transform rTransform)
        {
            // First, check if a child already exists for this transform
            BoneControllerBone lChild = GetChild(rTransform);

            if (lChild == null)
            {
                lChild = new BoneControllerBone(mSkeleton);
                AddChild(lChild, false);

                lChild.Transform = rTransform;
            }

            return lChild;
        }

        /// <summary>
        /// Adds a child to this bone
        /// </summary>
        /// <param name="rChild"></param>
        private void AddChild(BoneControllerBone rChild, bool rInsert)
        {
            if (rChild == null) { return; }

            if (rChild.Parent != this)
            {
                rChild.Parent = this;
            }

            if (!mChildren.Contains(rChild))
            {
                if (rInsert)
                {
                    mChildren.Insert(0, rChild);
                }
                else
                {
                    mChildren.Add(rChild);
                }
            }

            // Set the Unity parent (if needed)
            if (_Transform != null && rChild.Transform != null)
            {
                // Don't reset the transform since we don't want it
                // to change places in the index. Otherwise, our add
                // process will be going over the same items
                if (rChild.Transform.parent != _Transform)
                {
                    rChild.Transform.parent = _Transform;
                }
            }
        }

        /// <summary>
        /// Removes the child bone
        /// </summary>
        /// <param name="rChild"></param>
        public void RemoveChild(BoneControllerBone rChild)
        {
            if (rChild == null) { return; }

            rChild.Parent = null;

            if (mChildren.Contains(rChild))
            {
                mChildren.Remove(rChild);
            }

            if (rChild.Transform != null)
            {
                rChild.Transform.parent = null;
            }
        }

        /// <summary>
        /// Clears the bone of any objects it maintains.
        /// </summary>
        public override void Clear()
        {
            mSkeleton = null;

            _Swing = Quaternion.identity;
            _TargetSwing = Quaternion.identity;

            _Twist = Quaternion.identity;
            _TargetTwist = Quaternion.identity;

            // Get rid of the children
            for (int i = Children.Count - 1; i >= 0; i--)
            {
                Children[i].Clear();
            }

            Children.Clear();

            // Get rid of the joint
            _Joint = null;
        }

        /// <summary>
        /// Clears the rotation value so that it's not applied
        /// </summary>
        public override void ClearRotation()
        {
            _Swing = Quaternion.identity;
            _TargetSwing = Quaternion.identity;

            _Twist = Quaternion.identity;
            _TargetTwist = Quaternion.identity;
        }

        /// <summary>
        /// Converts a full "world" rotation to a local rotation. The local rotation can
        /// than be used to process constraints.
        /// </summary>
        /// <param name="rWorldRotation"></param>
        /// <returns></returns>
        public override Quaternion TransformWorldRotationToLocalRotation(Quaternion rWorldRotation)
        {
            Quaternion lRotation = (mParent != null ? mParent.Transform.rotation : Quaternion.identity) * _BindRotation * _ToBoneForward;
            return Quaternion.Inverse(lRotation) * rWorldRotation;
        }

        /// <summary>
        /// Converts a full "world" direction to a local direction.
        /// </summary>
        /// <param name="rWorldDirection"></param>
        /// <returns></returns>
        public override Vector3 TransformWorldRotationToLocalRotation(Vector3 rWorldDirection)
        {
            Quaternion lRotation = (mParent != null ? mParent.Transform.rotation : Quaternion.identity) * _BindRotation * _ToBoneForward;
            return Quaternion.Inverse(lRotation) * rWorldDirection;
        }

        /// <summary>
        /// Allows us to transform just the twist portion of the rotation
        /// </summary>
        /// <param name="rWorldTwist"></param>
        /// <returns></returns>
        public Quaternion TransformWorldTwistToLocalTwist(Quaternion rWorldTwist)
        {
            //Quaternion lLocalRotation = TransformWorldRotationToLocalRotation(rWorldTwist);

            //Quaternion lLocalSwing = Quaternion.identity;
            //Quaternion lLocalTwist = Quaternion.identity;
            //lLocalRotation.DecomposeSwingTwist(Vector3.forward, ref lLocalSwing, ref lLocalTwist);
            //return lLocalTwist;

            Quaternion lParentRotation = (mParent != null ? mParent.Transform.rotation : Quaternion.identity);

            Quaternion lBindSwing = Quaternion.identity;
            Quaternion lBindTwist = Quaternion.identity;
            _BindRotation.DecomposeSwingTwist(_BoneForward, ref lBindSwing, ref lBindTwist);

            Quaternion lParentSwing = Quaternion.identity;
            Quaternion lParentTwist = Quaternion.identity;
            lParentRotation.DecomposeSwingTwist(_BoneForward, ref lParentSwing, ref lParentTwist);

            Quaternion lRotation = rWorldTwist * Quaternion.Inverse(lParentTwist);
            lRotation = lRotation * Quaternion.Inverse(lBindTwist);




            lRotation = rWorldTwist;
            return lRotation;
        }

        /// <summary>
        /// Converts a local rotation to a world rotation. The local rotation can
        /// than be used to process constraints.
        /// </summary>
        /// <param name="rLocalRotation"></param>
        /// <returns></returns>
        public override Quaternion TransformLocalRotationToWorldRotation(Quaternion rLocalRotation)
        {
            Quaternion lRotation = (mParent != null ? mParent.Transform.rotation : Quaternion.identity) * _BindRotation * _ToBoneForward;

            Quaternion lResult = lRotation * rLocalRotation;
            if (lResult.x == 0f && lResult.y == 0f && lResult.z == 0f && lResult.w == -1f) { lResult = Quaternion.identity; }

            return lResult;
        }

        /// <summary>
        /// Converts a local direction to a world direction.
        /// </summary>
        /// <param name="rLocalRotation"></param>
        /// <returns></returns>
        public override Vector3 TransformLocalRotationToWorldRotation(Vector3 rLocalDirection)
        {
            Quaternion lRotation = (mParent != null ? mParent.Transform.rotation : Quaternion.identity) * _BindRotation * _ToBoneForward;
            if (lRotation.x == 0f && lRotation.y == 0f && lRotation.z == 0f && lRotation.w == -1f) { lRotation = Quaternion.identity; }

            return lRotation * rLocalDirection;
        }

        /// <summary>
        /// Converts a local point to a world point
        /// </summary>
        /// <param name="rLocalPoint"></param>
        /// <returns></returns>
        public override Vector3 TransformLocalPointToWorldPoint(Vector3 rLocalPoint)
        {
            Quaternion lRotation = (mParent != null ? mParent.Transform.rotation : Quaternion.identity) * _BindRotation * _ToBoneForward;
            if (lRotation.x == 0f && lRotation.y == 0f && lRotation.z == 0f && lRotation.w == -1f) { lRotation = Quaternion.identity; }

            return _Transform.position + (lRotation * rLocalPoint);
        }

        /// <summary>
        /// Rotates the bone using the specified rotation
        /// </summary>
        /// <param name="rRotation">Quaternion containing the rotation</param>
        public override void SetWorldSwing(Quaternion rRotation, float rWeight)
        {
            SetWorldRotation(rRotation, Quaternion.identity, rWeight);
        }

        /// <summary>
        /// Rotates the bone using the specified angles (in degrees)
        /// </summary>
        /// <param name="rEuler">Vector containing the angles (pitch, yaw, roll)</param>
        public override void SetWorldRotation(Vector3 rEuler, float rWeight)
        {
            // Each delta value is transformed by the bone's current rotation axis and then applied to the current rotation.
            Quaternion lWorldRotation = Quaternion.AngleAxis(rEuler.y, Vector3.up) * Quaternion.AngleAxis(rEuler.x, Vector3.right) * Quaternion.AngleAxis(rEuler.z, Vector3.forward);

            // Set this local rotation
            SetWorldRotation(lWorldRotation, Quaternion.identity, rWeight);
        }

        /// <summary>
        /// Rotates the bone using the specified angles (in degrees)
        /// </summary>
        /// <param name="rPitch">Rotation around the x axis (posative is down)</param>
        /// <param name="rYaw">Rotation around the y axis (posative is right)</param>
        /// <param name="rRoll">Rotation around the z axis (posative is right)</param>
        public override void SetWorldRotation(float rPitch, float rYaw, float rRoll, float rWeight)
        {
            // Each delta value is transformed by the bone's current rotation axis and then applied to the current rotation.
            Quaternion lWorldRotation = Quaternion.AngleAxis(rYaw, Vector3.up) * Quaternion.AngleAxis(rPitch, Vector3.right) * Quaternion.AngleAxis(rRoll, Vector3.forward);

            // Set this local rotation
            SetWorldRotation(lWorldRotation, Quaternion.identity, rWeight);
        }

        /// <summary>
        /// Rotates the bone using the specified rotation
        /// </summary>
        /// <param name="rRotation">Quaternion containing the rotation</param>
        public override void SetWorldRotation(Quaternion rRotation, float rWeight)
        {
            _IsDirty = true;

            // Ensure that we're thinking in terms of our "BoneForward". The swing is 
            // controlling the calculated bone, not the original transform
            rRotation = rRotation * _ToBoneForwardInv;

            // Extract out the components
            Quaternion lSwing = Quaternion.identity;
            Quaternion lTwist = Quaternion.identity;
            rRotation.DecomposeSwingTwist(WorldBindRotation * Vector3.forward, ref lSwing, ref lTwist);

            // Allow the update function to transform out of world coordinates and decompose
            IKBoneModifier lModifier = IKBoneModifier.Allocate(EnumIKBoneModifierType.WORLD_ROTATION, lSwing, lTwist, rWeight);
            mModifiers.Add(lModifier);
        }

        /// <summary>
        /// Rotates the bone using the specified rotation
        /// </summary>
        /// <param name="rRotation">Quaternion containing the rotation</param>
        public override void SetWorldRotation(Quaternion rSwing, Quaternion rTwist, float rWeight)
        {
            _IsDirty = true;

            // Ensure that we're thinking in terms of our "BoneForward". The swing is 
            // controlling the calculated bone, not the original transform
            rSwing = rSwing * _ToBoneForwardInv;

            // Allow the update function to transform out of world coordinates and decompose
            IKBoneModifier lModifier = IKBoneModifier.Allocate(EnumIKBoneModifierType.WORLD_ROTATION, rSwing, rTwist, rWeight);
            mModifiers.Add(lModifier);
        }

        /// <summary>
        /// Rotates the bone using the specified angles (in degrees)
        /// </summary>
        /// <param name="rEuler">Vector containing the angles (pitch, yaw, roll)</param>
        public override void SetLocalRotation(Vector3 rEuler, float rWeight)
        {
            SetLocalRotation(rEuler.x, rEuler.y, rEuler.z, rWeight);
        }

        /// <summary>
        /// Force the local rotation to the specified angles (in degrees)
        /// </summary>
        /// <param name="rPitch">Angle (in degrees) around the X-axis</param>
        /// <param name="rYaw">Angle (in degrees) around the Y-axis</param>
        /// <param name="rRoll">Angle (in degrees) around the Z-axis</param>
        public override void SetLocalRotation(float rPitch, float rYaw, float rRoll, float rWeight)
        {
            // Each delta value is transformed by the bone's current rotation axis and then applied to the current rotation.
            Quaternion lWorldRotation = Quaternion.AngleAxis(rYaw, _Transform.up) * Quaternion.AngleAxis(rPitch, _Transform.right) * Quaternion.AngleAxis(rRoll, _Transform.forward) * _Transform.rotation;

            // Extract out the rotation that gets us from the bind rotation to this world rotation
            Quaternion lLocalRotation = TransformWorldRotationToLocalRotation(lWorldRotation);

            // Set this local rotation
            SetLocalRotation(lLocalRotation, rWeight);
        }

        /// <summary>
        /// Rotates the bone using the specified local rotation. We'll extract
        /// the swing and twist values so we can process them seperately
        /// </summary>
        /// <param name="rRotation">Quaternion containing the rotation</param>
        public override void SetLocalRotation(Quaternion rRotation, float rWeight)
        {
            _IsDirty = true;

            Quaternion lSwing = Quaternion.identity;
            Quaternion lTwist = Quaternion.identity;
            rRotation.DecomposeSwingTwist(Vector3.forward, ref lSwing, ref lTwist);

            IKBoneModifier lModifier = IKBoneModifier.Allocate(EnumIKBoneModifierType.LOCAL_ROTATION, lSwing, lTwist, rWeight);
            mModifiers.Add(lModifier);
        }

        /// <summary>
        /// Rotates the bone along the swing and twist axis. Useful if we're 
        /// dealing with three degrees of freedome
        /// </summary>
        /// <param name="rSwing">Rotation along the axes perpedicular to the bone axis</param>
        /// <param name="rTwist">Rotation along the bone axis</param>
        public override void SetLocalRotation(Quaternion rSwing, Quaternion rTwist, float rWeight)
        {
            _IsDirty = true; 

            IKBoneModifier lModifier = IKBoneModifier.Allocate(EnumIKBoneModifierType.LOCAL_ROTATION, rSwing, rTwist, rWeight);
            mModifiers.Add(lModifier);
        }

        /// <summary>
        /// Rotates the bone to point to the specified position. The up vector of the bone
        /// will be it's current up vector
        /// </summary>
        /// <param name="rPosition">Position to rotat towards</param>
        /// <param name="rWeight">Impact the modifier has</param>
        public override void SetWorldEndPosition(Vector3 rPosition, float rWeight)
        {
            SetWorldEndPosition(rPosition, _Transform.rotation * _BoneUp, rWeight);
        }

        /// <summary>
        /// Rotates the bone to point to the specified position. The up vector of the bone
        /// can be provided to assist
        /// </summary>
        /// <param name="rPosition">Position to rotat towards</param>
        /// <param name="rUp">Up direction to use in the look at rotation</param>
        /// <param name="rWeight">Impact the modifier has</param>
        public override void SetWorldEndPosition(Vector3 rPosition, Vector3 rUp, float rWeight)
        {
            _IsDirty = true;

            if (rUp.sqrMagnitude == 0f) { rUp = _Transform.rotation * BoneUp; }

            IKBoneModifier lModifier = IKBoneModifier.Allocate(EnumIKBoneModifierType.END_WORLD_POSITION, rPosition, rUp, rWeight);
            mModifiers.Add(lModifier);
        }

        /// <summary>
        /// Update the bone transformation each frame in order to override/blend with the
        /// animation.
        /// </summary>
        public void Update()
        {
            // It's possible our bones have become invalidated. If so, we need to reload them from the skeleton
            if (!mIsValid) 
            { 
                LoadBones(); 
            }

            // Unity serialization won't allow us to create circular references.
            // So, this ensures that bone is set for any valid joint.
            if (_Joint != null && _Joint.Bone == null) 
            {
                _Joint.Initialize(this);
            }

            // If we can apply modifiers, continue
            if (_IsDirty && _Weight > 0f)
            {
                Quaternion lSwing = Quaternion.identity;
                Quaternion lTwist = Quaternion.identity;
                Quaternion lLocalRotation = Quaternion.identity;

                // Apply all the modifications
                if (mModifiers.Count > 0)
                {
                    for (int i = 0; i < mModifiers.Count; i++)
                    {
                        // As we extract out the swing and twist, we want the swing to be oriented based on 
                        // the bone's forward. That means the bone's bind pose + the forward we created when
                        // the bone was imported. That means the swing becomes local. So, a local swing of "identity"
                        // will put us back to the bind pose.

                        // Since we're dealing with a local rotation, this is easy. Just apply it.
                        if (mModifiers[i].Type == EnumIKBoneModifierType.LOCAL_ROTATION)
                        {
                            lSwing = mModifiers[i].Swing;
                            lTwist = mModifiers[i].Twist;
                        }
                        // If we know the position of the next bone, we need to turn that into a local rotation.
                        else if (mModifiers[i].Type == EnumIKBoneModifierType.WORLD_ROTATION)
                        {
                            // The current rotation we will lerp from.
                            Quaternion lWorldRotation = mModifiers[i].Swing * mModifiers[i].Twist;
                            lLocalRotation = Quaternion.Inverse(WorldBindRotation) * lWorldRotation;

                            // Now, we need to set the rotation as if the bone's forward is the starting point
                            lLocalRotation = lLocalRotation * _ToBoneForward;

                            // Extract out the components
                            lLocalRotation.DecomposeSwingTwist(Vector3.forward, ref lSwing, ref lTwist);
                        }
                        else if (mModifiers[i].Type == EnumIKBoneModifierType.END_WORLD_POSITION)
                        {
                            // The current rotation we will lerp from.
                            Quaternion lWorldRotation = Quaternion.LookRotation(mModifiers[i].Position - _Transform.position, mModifiers[i].Up) * _ToBoneForwardInv;
                            lLocalRotation = Quaternion.Inverse(WorldBindRotation) * lWorldRotation;

                            // Now, we need to set the rotation as if the bone's forward is the starting point
                            lLocalRotation = lLocalRotation * _ToBoneForward;

                            // Extract out the components
                            lLocalRotation.DecomposeSwingTwist(Vector3.forward, ref lSwing, ref lTwist);
                        }

                        // We simply apply the first modifier we find
                        if (i == 0)
                        {
                            _TargetSwing = lSwing;
                            _TargetTwist = lTwist;
                        }
                        // After the first one, we blend them
                        else
                        {
                            _TargetSwing = Quaternion.Lerp(_TargetSwing, lSwing, mModifiers[i].Weight);
                            _TargetTwist = Quaternion.Lerp(_TargetTwist, lTwist, mModifiers[i].Weight);
                        }

                        IKBoneModifier.Release(mModifiers[i]);
                    }

                    // Clear the modifiers
                    mModifiers.Clear();
                }
                
                // Process all the constraints to make sure we're not violating the joint
                if (_Joint != null && _ApplyLimits && _ApplyLimitsInFrame)
                {
                    mIsInLimits = _Joint.ApplyLimits(ref _TargetSwing, ref _TargetTwist);
                }

                // Without ToBoneForward, we're just rotating around the bone's initial orientation.
                // In the case of the Unity character, the right arm's "forward" is down the x-axis.
                // so a swing with a pitch (x-axis) will actually twist the bone.
                Quaternion lBindForward = _BindRotation * _ToBoneForward;

                // Before we can apply the local swing to the bind-forward rotation, we
                // need to take convert the coordinates so x=pitch, y=yaw, and z=roll. This
                // will match our goal of all bones being z-forward.
                Quaternion lAdjustedLocalRotation = _TargetSwing * _TargetTwist * _ToBoneForwardInv;

                // Apply our local rotation by the bind forward. This is the final 'local' rotation to apply
                lAdjustedLocalRotation = lBindForward * lAdjustedLocalRotation;
                if (!QuaternionExt.IsEqual(_Transform.localRotation, lAdjustedLocalRotation))
                {
                    _Transform.localRotation = Quaternion.Lerp(_Transform.localRotation, lAdjustedLocalRotation, _Weight);
                }

                // Store the rotation components
                _Swing = _TargetSwing;
                _Twist = _TargetTwist;                
            }

            // Update each of the bone's children
            for (int i = 0; i < mChildren.Count; i++)
            {
                mChildren[i].Update();
            }

            // Clean up for the next frame
            _ApplyLimitsInFrame = true;

            // Report the bone as no longer dirty
            _IsDirty = false;
        }

        /// <summary>
        /// Tests if the point is contained by the bone's collider
        /// </summary>
        /// <param name="rPoint"></param>
        /// <returns></returns>
        public override bool TestPointCollision(Vector3 rPoint)
        {
            if (_ColliderSize.x == 0f && _ColliderSize.y == 0f && _ColliderSize.z == 0f) { return false; }

            Matrix4x4 lToObjectSpace = Matrix4x4.TRS(_Transform.position, _Transform.rotation * _ToBoneForward, Vector3.one).inverse;
            Vector3 lLocalPoint = lToObjectSpace.MultiplyPoint(rPoint);

            // Sphere type
            if (_ColliderType == 1)
            {
                if (lLocalPoint.magnitude > _ColliderSize.x) { return false; }
            }
            // Box type
            else
            {
                if (lLocalPoint.x < _ColliderSize.x * -0.5f) { return false; }
                if (lLocalPoint.x > _ColliderSize.x * 0.5f) { return false; }
                if (lLocalPoint.y < _ColliderSize.y * -0.5f) { return false; }
                if (lLocalPoint.y > _ColliderSize.y * 0.5f) { return false; }
                if (lLocalPoint.z < 0f) { return false; }
                if (lLocalPoint.z > _ColliderSize.z) { return false; }
            }

            return true;
        }

        /// <summary>
        /// Tests if the point is contained by the bone's collider
        /// </summary>
        /// <param name="rPoint"></param>
        /// <returns></returns>
        public bool TestLocalPointCollision(Vector3 rLocalPoint)
        {
            if (_ColliderSize.x == 0f && _ColliderSize.y == 0f && _ColliderSize.z == 0f) { return false; }

            // Sphere type
            if (_ColliderType == 1)
            {
                if (rLocalPoint.magnitude > _ColliderSize.x) { return false; }
            }
            // Box type
            else
            {
                if (rLocalPoint.x < _ColliderSize.x * -0.5f) { return false; }
                if (rLocalPoint.x > _ColliderSize.x * 0.5f) { return false; }
                if (rLocalPoint.y < _ColliderSize.y * -0.5f) { return false; }
                if (rLocalPoint.y > _ColliderSize.y * 0.5f) { return false; }
                if (rLocalPoint.z < 0f) { return false; }
                if (rLocalPoint.z > _ColliderSize.z) { return false; }
            }

            return true;
        }

        /// <summary>
        /// Tests if the ray collides with the bone's collider and if so, it returns the collision point
        /// </summary>
        /// <param name="rStart"></param>
        /// <param name="rDirection"></param>
        /// <param name="rRange"></param>
        /// <param name="rHitPoint"></param>
        /// <returns></returns>
        public override bool TestRayCollision(Vector3 rStart, Vector3 rDirection, float rRange, out Vector3 rHitPoint)
        {
            rHitPoint = Vector3.zero;

            if (rRange <= 0f) { return false; }
            if (rDirection.sqrMagnitude == 0f) { return false; }
            if (_ColliderSize.x == 0f && _ColliderSize.y == 0f && _ColliderSize.z == 0f) { return false; }

            // Grab the matrix that gets us to the local point. Doing it here saves processing time
            Matrix4x4 lToObjectSpace = Matrix4x4.TRS(_Transform.position, _Transform.rotation * _ToBoneForward, Vector3.one).inverse;

            // If our starting point is in the bone, this is easy
            if (TestLocalPointCollision(lToObjectSpace.MultiplyPoint(rStart))) 
            { 
                rHitPoint = rStart;
                return true;
            }

            Vector3 lStart = rStart;
            float lStep = Mathf.Min(rRange / 10f, 0.05f);

            // The first phase: Do a step-by-step to see if we hit anything along the ray
            for (float lDistance = lStep; lDistance < rRange; lDistance += lStep)
            {
                Vector3 lEnd = lStart + (rDirection * lDistance);
                if (TestPointCollision(lEnd))
                {
                    // The second phase: Determine the closest point by cutting the distance in half
                    do
                    {
                        lDistance = lDistance * 0.5f;
                        rHitPoint = lStart + (rDirection * lDistance);
                        if (!TestLocalPointCollision(lToObjectSpace.MultiplyPoint(rHitPoint)))
                        {
                            lStart = rHitPoint;
                        }
                    }
                    while (lDistance > 0.001f);

                    // Return this collision point
                    return true;
                }
            }

            // No collision found
            return false;
        }

        /// <summary>
        /// Finds the closes point to the bone based on the collider information
        /// </summary>
        /// <param name="rPoint">World oriented point we're looking at</param>
        /// <returns>World oriented point that is on the collider bounds</returns>
        public override Vector3 ClosetPoint(Vector3 rPoint)
        {
            // Sphere type
            if (_ColliderType == 1)
            {
                // Direction from the collider to our position
                Vector3 lDirection = Vector3.Normalize(rPoint - _Transform.position);

                // Get a good position relative to the collider
                Vector3 lLocalPosition = lDirection * (_ColliderSize.x * _Transform.localScale.x);

                // Turn that into world space
                return _Transform.position + lLocalPosition;
            }
            // Box type
            else
            {
                Matrix4x4 lToWorldSpace = Matrix4x4.TRS(_Transform.position, _Transform.rotation * _ToBoneForward, Vector3.one);
                Matrix4x4 lToObjectSpace = lToWorldSpace.inverse;

                // Move the world space point to local space
                Vector3 lLocalPosition = lToObjectSpace.MultiplyPoint(rPoint);

                lLocalPosition.x = Mathf.Clamp(lLocalPosition.x, _ColliderSize.x * -0.5f, _ColliderSize.x * 0.5f);
                lLocalPosition.y = Mathf.Clamp(lLocalPosition.y, _ColliderSize.y * -0.5f, _ColliderSize.y * 0.5f);
                lLocalPosition.z = Mathf.Clamp(lLocalPosition.z, 0f, _ColliderSize.z);

                // Finally, go back to world space
                return lToWorldSpace.MultiplyPoint(lLocalPosition);
            }
        }

        /// <summary>
        /// Due to Unity's serialization limit on nested objects (7 levels),
        /// we have to store the bones in a flat list and then reconstruct
        /// our hierarchy after deserialization.
        /// 
        /// This function is called BEFORE the skeleton is serialized
        /// </summary>
        /// <param name="rSkeleton"></param>
        public void OnBeforeSkeletonSerialized(BoneController rSkeleton)
        {
            //Log.ConsoleWrite("BoneControllerBone.OnBeforeSkeletonSerialized()");

            if (mSkeleton == null) { return; }
            if (mSkeleton != rSkeleton) { return; }

            // Grab the index of the parent bone
            SerializationParentIndex = mSkeleton.Bones.IndexOf(mParent);

            // Grab the indexes of the child bones
            SerializationChildIndexes.Clear();
            for (int i = 0; i < mChildren.Count; i++)
            {
                SerializationChildIndexes.Add(mSkeleton.Bones.IndexOf(mChildren[i]));
            }

            // We need to serialize the joint
            SerializationJoint = JSONSerializer.Serialize(_Joint, false);
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
        /// </summary>
        /// <param name="rSkeleton"></param>
        public void OnAfterSkeletonDeserialized(BoneController rSkeleton)
        {
            //Log.ConsoleWrite("BoneControllerBone.OnAfterSkeletonDeserialized()   pi:" + SerializationParentIndex);
            //Log.FileWrite("BoneControllerBone.OnAfterSkeletonDeserialized");

            // Set the new skeleton
            mSkeleton = rSkeleton;

            // Initialize the friendly name
            mCleanName = BoneController.CleanBoneName(Name);

            // Reload the bones based on the IDs that we serialized earlier.
            // We do this since the references aren't kept as expected. We can't
            // call the LoadBones() function because the test for mSkeleton == null
            // isn't allowed on the load thread.
            mParent = (SerializationParentIndex < 0 ? null : mSkeleton.Bones[SerializationParentIndex]);

            mChildren.Clear();
            for (int i = 0; i < SerializationChildIndexes.Count; i++)
            {
                if (SerializationChildIndexes[i] >= 0)
                {
                    mChildren.Add(mSkeleton.Bones[SerializationChildIndexes[i]]);
                }
            }

            // We need to deserialize the joint here
            _Joint = JSONSerializer.Deserialize(SerializationJoint) as BoneControllerJoint;

            // We put this in a try/catch since we can't compare it to null when in
            // the seperate initialization thread. So, we'll just fall out if needed

#if !UNITY_EDITOR && (UNITY_WEBPLAYER || UNITY_WEBGL)
            if (_Joint != null)
            {
                _Joint.Initialize(this);
            }
#else
            try
            {
                _Joint.Initialize(this);
            }
            catch {}
#endif

            //Flag our bones as initialized and valid
            mIsValid = true;
        }

        /// <summary>
        /// Due to Unity's serialization limit on nested objects (7 levels),
        /// we have to store the bones in a flat list and then reconstruct
        /// our hierarchy after deserialization.
        /// 
        /// This function is called BEFORE the skeleton is serialized
        /// </summary>
        public void OnBeforeSerialize()
        {
            //Log.ConsoleWrite("BoneControllerBone.OnBeforeSerialize()");
        }

        /// <summary>
        /// Due to Unity's serialization limit on nested objects (7 levels),
        /// we have to store the bones in a flat list and then reconstruct
        /// our hierarchy after deserialization.
        /// 
        /// This function is called AFTER the skeleton has been deserialized
        /// </summary>
        public void OnAfterDeserialize()
        {
            //Log.ConsoleWrite("BoneControllerBone.OnAfterDeserialize()");
            //Log.FileWrite("BoneControllerBone.OnAfterDeserialize", false);

            InvalidateBones();
        }

        // **************************************************************************************************
        // Following properties and function only valid while editing
        // **************************************************************************************************

        /// <summary>
        /// Determins if we'll draw debug information in
        /// the editor
        /// </summary>
        public bool _ShowDebug = false;
        public bool ShowDebug
        {
            get { return _ShowDebug; }
            set { _ShowDebug = value; }
        }

        /// <summary>
        /// Defines the joint index that is currently selected
        /// </summary>
        private int mSelectedJointIndex = 0;

        /// <summary>
        /// Store the joint type names that are available
        /// </summary>
        private static GUIContent[] sJointNames = null;

        /// <summary>
        /// Store the joint types that are available
        /// </summary>
        private static Type[] sJointTypes = null;

        /// <summary>
        /// Raised when the bone is selected in the editor
        /// </summary>
        public virtual void OnEnable()
        {
            if (_Joint != null) { _Joint.OnEnable(); }
        }

        /// <summary>
        /// Raised when the bone is deselected in the editor
        /// </summary>
        public virtual void OnDisable()
        {
            if (_Joint != null) { _Joint.OnDisable(); }
        }

        /// <summary>
        /// Allow the constraint to render it's own GUI
        /// </summary>
        /// <returns>Reports if the object's value was changed</returns>
        public virtual bool OnInspectorGUI(bool rIsSelected)
        {
            bool lIsDirty = false;

#if UNITY_EDITOR

            // Build the list of joint names (and types) that the user
            // can select from.
            if (sJointNames == null)
            {
                
                // Grab all type types that are subclassed to the BoneControllerJoint
                List<Type> lTypes = new List<Type>();
                // CDL 07/03/2018 - might as well used the cached types list here as well
                //foreach (Assembly lAssembly in AppDomain.CurrentDomain.GetAssemblies())
                //{
                //    try
                //    {
                //        foreach (Type lType in lAssembly.GetTypes())
                //        {
                //            if (!lType.IsAbstract && lType != typeof(BoneControllerJoint) && typeof(BoneControllerJoint).IsAssignableFrom(lType))
                //            {
                //                lTypes.Add(lType);
                //            }
                //        }
                //    }
                //    catch { }
                //}

                List<Type> lFoundTypes = AssemblyHelper.FoundTypes;
                try
                {
                    foreach (Type lType in lFoundTypes)
                    {
                        if (!lType.IsAbstract && lType != typeof(BoneControllerJoint) && typeof(BoneControllerJoint).IsAssignableFrom(lType))
                        {
                            lTypes.Add(lType);
                        }
                    }
                }
                catch { }
                

                // Build our arrays
                    sJointNames = new GUIContent[lTypes.Count + 1];
                sJointTypes = new Type[lTypes.Count + 1];

                sJointNames[0] = new GUIContent("Free (no joint type specified)");
                sJointTypes[0] = null;

                for (int i = 0; i < lTypes.Count; i++)
                {
                    IKBoneJointNameAttribute lNameAttribute = ReflectionHelper.GetAttribute<IKBoneJointNameAttribute>(lTypes[i]);
                    string lName = (lNameAttribute != null ? lNameAttribute.Value : lTypes[i].Name);

                    sJointNames[i + 1] = new GUIContent(lName);
                    sJointTypes[i + 1] = lTypes[i];
                }

                // Clear out our list
                lTypes.Clear();
            }

            // Ensure we have the right type selected
            mSelectedJointIndex = 0;
            for (int i = 0; i < sJointTypes.Length; i++)
            {
                if (_Joint != null && _Joint.GetType() == sJointTypes[i])
                {
                    mSelectedJointIndex = i;
                }
            }

            string lNewName = EditorGUILayout.TextField(new GUIContent("Name", "Name of the bone."), _Name);
            if (lNewName != _Name)
            {
                lIsDirty = true;
                Name = _Name;
            }

            Transform lNewTransform = EditorGUILayout.ObjectField(new GUIContent("Transform", "Transform this bone represents"), _Transform, typeof(Transform), true) as Transform;
            if (lNewTransform != _Transform && mSkeleton.Root != this)
            {
                lIsDirty = true;
                Transform = lNewTransform;
            }

            GUILayout.Space(5f);

            float lNewLength = EditorGUILayout.FloatField(new GUIContent("Bone Length", "Length of the bone when the skeleton was imported."), _Length);
            if (lNewLength != _Length)
            {
                lIsDirty = true;
                Length = lNewLength;
            }

            Vector3 lNewPosition = EditorGUILayout.Vector3Field(new GUIContent("Bind Position", "Position values when the bone was imported."), _BindPosition);
            if (lNewPosition != _BindPosition)
            {
                lIsDirty = true;
                _BindPosition = lNewPosition;
            }

            Vector3 lNewRotation = EditorGUILayout.Vector3Field(new GUIContent("Bind Rotation", "Axis rotation values when the bone was imported."), _BindRotation.eulerAngles);
            if (lNewRotation != _BindRotation.eulerAngles)
            {
                lIsDirty = true;
                _BindRotation.eulerAngles = lNewRotation;
            }

            GUILayout.Space(5);

            Vector3 lEuler = Quaternion.LookRotation(_BoneForward, _BoneUp).eulerAngles;
            Vector3 lNewEuler = EditorGUILayout.Vector3Field(new GUIContent("Rotation Adjust", "Allows you to adjust the bind rotation to make it easier to use in motors. [pitch, yaw, roll]"), lEuler);
            if (lNewEuler != lEuler)
            {
                lIsDirty = true;

                if (Mathf.Abs(lNewEuler.x) < 0.0001f) { lNewEuler.x = 0f; }
                if (Mathf.Abs(lNewEuler.y) < 0.0001f) { lNewEuler.y = 0f; }
                if (Mathf.Abs(lNewEuler.z) < 0.0001f) { lNewEuler.z = 0f; }
                Quaternion lAdjust = Quaternion.Euler(lNewEuler);

                _BoneForward = lAdjust.Forward();
                _BoneUp = lAdjust.Up();
                _BoneRight = lAdjust.Right();

                _ToBoneForward = Quaternion.identity.RotationTo(Quaternion.LookRotation(_BoneForward, _BoneUp));
                _ToBoneForwardInv = _ToBoneForward.Conjugate();
            }

            if (mEditorShowDirections)
            {
                Vector3 lNewBoneForward = EditorGUILayout.Vector3Field(new GUIContent("Bone Forward", "Direction the bone was created in."), _BoneForward);
                if (lNewBoneForward != _BoneForward)
                {
                    lIsDirty = true;
                    _BoneForward = lNewBoneForward.normalized;

                    _BoneUp = Vector3.Cross(_BoneForward, _BoneRight).normalized;
                    _ToBoneForward = Quaternion.identity.RotationTo(Quaternion.LookRotation(_BoneForward, _BoneUp));
                    _ToBoneForwardInv = _ToBoneForward.Conjugate();
                }

                Vector3 lNewBoneUp = EditorGUILayout.Vector3Field(new GUIContent("Bone Up", "Direction the bone was created in."), _BoneUp);
                if (lNewBoneUp != _BoneUp)
                {
                    lIsDirty = true;
                    _BoneUp = lNewBoneUp.normalized;

                    _BoneForward = Vector3.Cross(_BoneUp, _BoneRight).normalized;
                    _ToBoneForward = Quaternion.identity.RotationTo(Quaternion.LookRotation(_BoneForward, _BoneUp));
                    _ToBoneForwardInv = _ToBoneForward.Conjugate();
                }

                Vector3 lNewBoneRight = EditorGUILayout.Vector3Field(new GUIContent("Bone Right", "Direction the bone was created in."), _BoneRight);
                if (lNewBoneRight != _BoneRight)
                {
                    lIsDirty = true;
                    _BoneRight = lNewBoneRight.normalized;

                    _BoneUp = Vector3.Cross(_BoneForward, _BoneRight).normalized;
                    _ToBoneForward = Quaternion.identity.RotationTo(Quaternion.LookRotation(_BoneForward, _BoneUp));
                    _ToBoneForwardInv = _ToBoneForward.Conjugate();
                }
            }

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button(new GUIContent((mEditorShowDirections ? "Hide Directions" : "Show Directions"), "Hide or show the bone directions"), EditorStyles.label, GUILayout.Width(EditorGUIUtility.labelWidth)))
            {
                mEditorShowDirections = !mEditorShowDirections;
            }

            if (GUILayout.Button(new GUIContent("Reset", "Reloads the bind pose and initial bone information"), EditorStyles.miniButtonLeft))
            {
                _Length = 0f;
                InitializeBoneRotations();
            }

            if (GUILayout.Button(new GUIContent("Clear", "Clears the bone directions to match the initial model"), EditorStyles.miniButtonRight))
            {
                _BoneForward = Vector3.forward;
                _BoneUp = Vector3.up;
                _BoneRight = Vector3.right;
                _ToBoneForward = Quaternion.identity;
                _ToBoneForwardInv = _ToBoneForward.Conjugate();
            }

            EditorGUILayout.EndHorizontal();

            GUILayout.Space(5);

            int lNewColliderType = EditorGUILayout.Popup("Collider Type", _ColliderType, EnumIKBoneColliderType.Names);
            if (lNewColliderType != _ColliderType)
            {
                lIsDirty = true;
                _ColliderType = lNewColliderType;
            }

            if (_ColliderType == EnumIKBoneColliderType.SPHERE)
            {
                float lNewColliderSize = EditorGUILayout.FloatField(new GUIContent("Collider Radius", "Radius of the sphere used for collisions. The origin is based on the bone's position."), _ColliderSize.x);
                if (lNewColliderSize != _ColliderSize.x)
                {
                    lIsDirty = true;
                    _ColliderSize.x = lNewColliderSize;
                }
            }
            else
            {
                Vector3 lNewColliderSize = EditorGUILayout.Vector3Field(new GUIContent("Collider Size", "Size of the bounding box used for collisions. The origin is based on the bone's position and follows the bone right (x), bone up (y), and bone forward (z) axes."), _ColliderSize);
                if (lNewColliderSize != _ColliderSize)
                {
                    lIsDirty = true;
                    _ColliderSize = lNewColliderSize;
                }
            }

            GUILayout.Space(5);

            float lNewIKWeight = Mathf.Clamp01(EditorGUILayout.FloatField(new GUIContent("Blend Weight", "Defines the impact the rotation has relative to the animated rotation (0 to 1)."), _Weight));
            if (lNewIKWeight != _Weight)
            {
                _IsDirty = true;
                _Weight = lNewIKWeight;
            }

            bool lNewDebug = EditorGUILayout.Toggle(new GUIContent("Show Debug", "Determines if we show debug info in the editor."), _ShowDebug);
            if (lNewDebug != _ShowDebug)
            {
                lIsDirty = true;
                _ShowDebug = lNewDebug;
            }

            GUILayout.Space(5);

            int lNewSelectedJointIndex = EditorGUILayout.Popup(new GUIContent("Joint Type", "Defines the rotation type and limits of the bone."), mSelectedJointIndex, sJointNames, GUILayout.MinWidth(50));
            if (lNewSelectedJointIndex != mSelectedJointIndex)
            {
                lIsDirty = true;

                // We need to change the joint type. In the simple case, the
                // joint has full 3 degrees-of-freedom
                if (lNewSelectedJointIndex == 0)
                {
                    _Joint = null;
                }
                // Otherwise, we need to create the new joint type
                else
                {
                    BoneControllerJoint lJoint = Activator.CreateInstance(sJointTypes[lNewSelectedJointIndex]) as BoneControllerJoint;
                    lJoint.Initialize(this);

                    _Joint = lJoint;
                }
            }

            // If a joint is selected, we need to give it a chance to be editor
            if (_Joint == null)
            {
                bool lIsJointDirty = OnInspectorConstraintGUI(rIsSelected);
                lIsDirty = lIsDirty || lIsJointDirty;
            }
            else
            {
                bool lIsJointDirty = _Joint.OnInspectorConstraintGUI(rIsSelected);
                lIsDirty = lIsDirty || lIsJointDirty;
            }

#endif

            return lIsDirty;
        }

        /// <summary>
        /// Allow for rendering in the editor. The joints handle
        /// thier own GUI for editing the bone placement
        /// </summary>
        /// <returns></returns>
        public bool OnSceneGUI(bool rIsSelected)
        {
            if (mSkeleton == null) { return false; }

            bool lIsDirty = false;

#if UNITY_EDITOR

            Color lHandleColor = Handles.color;

            if (_Joint == null)
            {
                bool lIsJointDirty = OnSceneConstraintGUI(rIsSelected);
                if (lIsJointDirty) { lIsDirty = true; }
            }
            else
            {
                // With the editor, it's possible we get here before the joint 
                // is initialized. If so, do it here
                if (_Joint.Bone == null) { _Joint.Initialize(this); }

                // Now we can run the GUI
                bool lIsJointDirty = _Joint.OnSceneConstraintGUI(rIsSelected);
                if (lIsJointDirty) { lIsDirty = true; }
            }

            if (_ShowDebug || (rIsSelected && mSkeleton.EditorShowBoneColliders))
            {
                HandlesHelper.DrawBoneCollider(this, Color.cyan);
            }

            Handles.color = lHandleColor;

#endif

            return lIsDirty;
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
        /// 
        /// NOTE: The values in the modifier are "Bone Forward", not "Unity Forward"
        /// </summary>
        /// <param name="rModifier">Bone modifier containing information we'll update</param>
        /// <returns>Reports if the object's value was changed</returns>
        public virtual bool OnInspectorManipulatorGUI(IKBoneModifier rModifier)
        {

#if UNITY_EDITOR

            // Determine if the swing is changing
            Vector3 lSwing = rModifier.Swing.eulerAngles;
            Vector3 lNewSwing = InspectorHelper.Vector3Fields("Swing", "Euler angles to swing the bone.", lSwing, true, true, false); //lBoneForward.x == 0f, lBoneForward.y == 0f, lBoneForward.z == 0f);
            if (lNewSwing != lSwing)
            {
                // Grab the amount that was just rotated by based on the current rotation.
                // We do this so the change is relative to the current swing rotation
                Vector3 lDeltaRotations = lNewSwing - lSwing;
                rModifier.Swing = rModifier.Swing * Quaternion.Euler(lDeltaRotations);

                rModifier.IsDirty = true;
            }

            float lTwist = Vector3Ext.SignedAngle(Vector3.up, rModifier.Twist * Vector3.up, Vector3.forward);
            
            float lNewTwist = EditorGUILayout.FloatField("Twist", lTwist);
            if (lNewTwist != lTwist)
            {
                rModifier.Twist = Quaternion.AngleAxis(lNewTwist, Vector3.forward); 
                rModifier.IsDirty = true;
            }

            // Reset the values if needed
            if (GUILayout.Button("reset rotation", EditorStyles.miniButton))
            {
                rModifier.Swing = Quaternion.identity;
                rModifier.Twist = Quaternion.identity;
                rModifier.IsDirty = true;

                _Transform.localRotation = _BindRotation;
            }

#endif

            return rModifier.IsDirty;
        }

        /// <summary>
        /// Allows us to render joint info into the scene. This GUI is
        /// used for displaying and manipulating the joint itself.
        /// </summary>
        /// <param name="rModifier">Bone modifier containing information we'll update</param>
        /// <returns>Reports if the object's value was changed</returns>
        public virtual bool OnSceneManipulatorGUI(IKBoneModifier rModifier)
        {

#if UNITY_EDITOR

            bool lIsSwingDirty = HandlesHelper.JointSwingHandle(this, rModifier);
            if (lIsSwingDirty)
            {
                rModifier.IsDirty = true;
            }

            bool lIsTwistDirty = HandlesHelper.JointTwistHandle(this, rModifier);
            if (lIsTwistDirty)
            {
                rModifier.IsDirty = true;
            }

#endif

            return rModifier.IsDirty;
        }
    }
}
