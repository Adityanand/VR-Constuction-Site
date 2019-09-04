using System;
using System.Collections.Generic;
using UnityEngine;
using com.ootii.Geometry;
using com.ootii.Helpers;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace com.ootii.Actors.BoneControllers
{
    /// <summary>
    /// The drag motor uses physics properties to make a chain of
    /// bones bounce and move as a system. This is great for tails,
    /// pony tails, and clothing elements.
    /// </summary>
    [Serializable]
    [IKName("Bone Chain Drag Motor")]
    [IKDescription("This motor uses a chain of bones to drag and move each bone (similiar to a tail, pony tail, or clothing).")]
    public class BoneChainDragMotor : BoneControllerMotor
    {
        /// <summary>
        /// Determines if we use gravity to pull the chain from its
        /// normal bind shape. Chains that have been modeled to a shape,
        /// we typically don't want gravity.
        /// </summary>
        public bool _IsGravityEnabled = false;
        public bool IsGravityEnabled
        {
            get { return _IsGravityEnabled; }
            set { _IsGravityEnabled = value; }
        }

        /// <summary>
        /// Gravity to use with the spring motor
        /// </summary>
        public Vector3 _Gravity = new Vector3(0f, -1f, 0f);
        public Vector3 Gravity
        {
            get
            {
                if (_Gravity.sqrMagnitude == 0f) { return UnityEngine.Physics.gravity; }
                return _Gravity;
            }

            set { _Gravity = value; }
        }

        /// <summary>
        /// Determines how gravity is applied along the length of the chain
        /// </summary>
        public AnimationCurve _GravityImpact = AnimationCurve.Linear(0f, 1f, 1f, 1f);
        public AnimationCurve GravityImpact
        {
            get { return _GravityImpact; }
            set { _GravityImpact = value; }
        }

        /// <summary>
        /// Curve that allows us to apply stiffness along the length of the chain
        /// </summary>
        public AnimationCurve _Stiffness = AnimationCurve.Linear(0f, 0.3f, 1f, 0.3f);
        public AnimationCurve Stiffness
        {
            get { return _Stiffness; }
            set { _Stiffness = value; }
        }

        /// <summary>
        /// Determines if the chain collided with other objects
        /// </summary>
        public bool _IsCollisionEnabled = true;
        public bool IsCollisionEnabled
        {
            get { return _IsCollisionEnabled; }
            set { _IsCollisionEnabled = value; }
        }

        /// <summary>
        /// Determines what the links can collide with
        /// </summary>
        public int _CollisionLayers = -1;
        public int CollisionLayers
        {
            get { return _CollisionLayers; }
            set { _CollisionLayers = value; }
        }

        /// <summary>
        /// Extra information in order to help the management of the bones. There should
        /// always be 1 extra info node compared to the bones. This way we can track 
        /// the last bone too.
        /// </summary>
        public List<BoneChainDragBone> _BoneInfo = new List<BoneChainDragBone>();

        /// <summary>
        /// Track the bone transforms for collisions
        /// </summary>
        private List<Transform> mBoneTransforms = new List<Transform>();

        /// <summary>
        /// Default constructor
        /// </summary>
        public BoneChainDragMotor()
            : base()
        {
            _IsFixedUpdateEnabled = true;
        }

        /// <summary>
        /// Skeleton constructor
        /// </summary>
        /// <param name="rSkeleton">Skeleton the motor is driving</param>
        public BoneChainDragMotor(BoneController rSkeleton)
            : base(rSkeleton)
        {
            _IsFixedUpdateEnabled = true;
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

            // Ensure we always have one more than the number of bones. The
            // last one is the target position for the last bone.
            while (_BoneInfo.Count <= mBones.Count)
            {
                BoneControllerBone lBone = (_BoneInfo.Count < mBones.Count ? mBones[_BoneInfo.Count] : null);
                AddBoneInfo(mBones.Count, lBone);
            }

            // If it's time to update, determine the positions we need to be
            // at and lerp towards them.
            if (rUpdate)
            {
                // The first bone doesn't move on it's own. It's dragged by it's parent
                _BoneInfo[0].Velocity = _BoneInfo[0].Position - _BoneInfo[0].PrevPosition;
                _BoneInfo[0].PrevPosition = _BoneInfo[0].Position;
                _BoneInfo[0].Position = mBones[0]._Transform.position;

                // Initialize the data if it isn't
                for (int i = 1; i < _BoneInfo.Count; i++)
                {
                    // Initialize any bone info that hasn't currently been set.
                    if (_BoneInfo[i].PrevPosition.sqrMagnitude == 0f)
                    {
                        if (i < mBones.Count)
                        {
                            _BoneInfo[i].Velocity = Vector3.zero;
                            _BoneInfo[i].PrevPosition = mBones[i]._Transform.position;
                            _BoneInfo[i].Position = mBones[i]._Transform.position;
                        }
                        else
                        {
                            int lPrevIndex = i - 1;
                            _BoneInfo[i].Velocity = Vector3.zero;
                            _BoneInfo[i].PrevPosition = mBones[lPrevIndex]._Transform.position + (mBones[lPrevIndex]._Transform.rotation * (mBones[lPrevIndex].BoneForward * mBones[lPrevIndex].Length));
                            _BoneInfo[i].Position = mBones[lPrevIndex]._Transform.position + (mBones[lPrevIndex]._Transform.rotation * (mBones[lPrevIndex].BoneForward * mBones[lPrevIndex].Length));
                        }
                    }
                }

                // Drag the bones one after the other
                //bool lCollision = false;
                for (int i = 0; i < mBones.Count; i++)
                {
                    // Collisions will stop movement
                    if (_IsCollisionEnabled)
                    {
                        _BoneInfo[i].Collision = ProcessBoneCollisions(i);
                    }

                    // If we're not colliding, allow the bones to move
                    if (!_BoneInfo[i].Collision)
                    {
                        // Add gravity to pull the bones down
                        if (!_IsGravityEnabled)
                        {
                            _BoneInfo[i + 1].Velocity = Vector3.zero;
                        }
                        else
                        {
                            _BoneInfo[i + 1].Velocity = _Gravity * rDeltaTime * GetGravity(i);
                        }

                        // Add pull velocity so that the bones chase after the positions
                        _BoneInfo[i + 1].Velocity += (_BoneInfo[i + 1].Position - _BoneInfo[i + 1].PrevPosition);

                        // Determine the new position (which may be too much)
                        Vector3 lNewPosition = _BoneInfo[i + 1].PrevPosition + _BoneInfo[i + 1].Velocity;

                        // Ensure the new position stays connected with the previous bones
                        Vector3 lDirection = (lNewPosition - _BoneInfo[i].Position).normalized;
                        Vector3 lDragPosition = _BoneInfo[i].Position + (lDirection * mBones[i].Length);

                        // Finally, set the new position
                        _BoneInfo[i + 1].Position = lDragPosition;
                    }
                }

                // With the bone positions determined, now we can compute the final positions
                for (int i = 0; i < mBones.Count; i++)
                {
                    // The original pose the bones were bound to
                    Vector3 lBaseEndPosition = Vector3.zero;
                    if (_BoneInfo[i].UseBindPosition)
                    {
                        lBaseEndPosition = _BoneInfo[i].Position + (mBones[i].WorldBindRotation * (Vector3.forward * mBones[i].Length));
                    }
                    else
                    {
                        lBaseEndPosition = (i < mBones.Count - 1 ? mBones[i + 1].Transform.position : mBones[i].WorldEndPosition);
                    }

                    // Determine the bending based on the stiffness of each bone
                    float lStiffness = GetStiffness(i);
                    if (_BoneInfo[i].Collision) { lStiffness = 0f; }

                    Vector3 lNewEndPosition = Vector3.Lerp(_BoneInfo[i + 1].Position, lBaseEndPosition, lStiffness);

                    // Since we lerped, the value may not actually be the right position 
                    // based on our bone length. So, we need to reprocess the lengths again
                    Vector3 lDirection = (lNewEndPosition - _BoneInfo[i].Position).normalized;
                    lNewEndPosition = _BoneInfo[i].Position + (lDirection * mBones[i].Length);

                    // Finally, set the final positions
                    _BoneInfo[i + 1].Position = lNewEndPosition;

                    // Rotation based on the positions
                    Vector3 lTargetForward = _BoneInfo[i + 1].Position - _BoneInfo[i].Position;
                    if (lTargetForward.sqrMagnitude != 0f)
                    {
                        lTargetForward = lTargetForward.normalized;

                        // TRT 2/21/2016 - Updated to reflect bone's actual values
                        //_BoneInfo[i].RotationTarget = Quaternion.LookRotation(lTargetForward.normalized, mBones[i].Transform.up);

                        // TRT 3/8/2016 - Wasn't using the current bone's actual values :(
                        //_BoneInfo[i].RotationTarget = Quaternion.LookRotation(lTargetForward.normalized, mBones[i]._BoneUp);

                        // TRT 7/9/2016 - Wasn't trying to get back to original rotation
                        //Vector3 lCurrentUp = (mBones[i].Transform.rotation * mBones[i].ToBoneForward) * Vector3.up;
                        //_BoneInfo[i].RotationTarget = Quaternion.LookRotation(lTargetForward, lCurrentUp);

                        // TRT 7/9/2016 - Want to make sure the bones try to twist to thier original rotation too
                        Vector3 lCurrentUp = (mBones[i].Transform.rotation * mBones[i].ToBoneForward) * Vector3.up;

                        if (_BoneInfo[i].UntwistLerp > 0f)
                        {
                            Vector3 lBindUp = mBones[i].WorldBindRotation * Vector3.up;
                            lCurrentUp = Vector3.Lerp(lCurrentUp, lBindUp, _BoneInfo[i].UntwistLerp);
                        }

                        _BoneInfo[i].RotationTarget = Quaternion.LookRotation(lTargetForward, lCurrentUp);

                        //Debug.DrawLine(mBones[i].Transform.position, mBones[i].Transform.position + lTargetForward, Color.black);
                        //Debug.DrawLine(mBones[i].Transform.position, mBones[i].Transform.position + lCurrentUp, Color.magenta);
                    }
                    // In the case of a 0 length bone, we just use the current rotation
                    else
                    {
                        _BoneInfo[i].RotationTarget = mBones[i].Transform.rotation * mBones[i].ToBoneForward;
                    }

                    // Slowly move towards the rotation we determined
                    _BoneInfo[i].Rotation = Quaternion.Lerp(_BoneInfo[i].Rotation, _BoneInfo[i].RotationTarget, (_IsFixedUpdateEnabled && !mIsFirstUpdate ? _BoneInfo[i].RotationLerp : 1f));

                    // Set the world rotation
                    mBones[i].SetWorldRotation(_BoneInfo[i].Rotation, Quaternion.identity, _BoneWeight);

                    // Clean up
                    _BoneInfo[i].PrevPosition = _BoneInfo[i].Position;
                }

                // Update that last extra bone info
                _BoneInfo[_BoneInfo.Count - 1].PrevPosition = _BoneInfo[_BoneInfo.Count - 1].Position;
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

        /// <summary>
        /// Returns the stiffness value based on the bone chain span. This is the
        /// percentage along the chain based on the bone index.
        /// </summary>
        /// <param name="rIndex"></param>
        /// <returns></returns>
        public float GetStiffness(int rIndex)
        {
            return Mathf.Clamp01(_Stiffness.Evaluate(GetBoneChainSpan(rIndex)));
        }

        /// <summary>
        /// Returns the gravity value based on the bone chain span. This is the
        /// percentage along the chain based on the bone index.
        /// </summary>
        /// <param name="rIndex"></param>
        /// <returns></returns>
        public float GetGravity(int rIndex)
        {
            return Mathf.Clamp01(_GravityImpact.Evaluate(GetBoneChainSpan(rIndex)));
        }

        /// <summary>
        /// Returns the span (0 to 1) that the bone index
        /// has reached compared to the total length of the chain.
        /// </summary>
        /// <param name="rIndex"></param>
        /// <returns></returns>
        public float GetBoneChainSpan(int rIndex)
        {
            if (rIndex <= 0) { return 0; }
            if (rIndex >= mBones.Count) { return 1; }

            float lLength = 0f;
            float lTotalLength = 0f;
            for (int i = 0; i < mBones.Count; i++)
            {
                if (i <= rIndex) { lLength = lLength + mBones[i].Length; }
                lTotalLength = lTotalLength + mBones[i].Length;
            }

            return lLength / lTotalLength;
        }

        /// <summary>
        /// Sphere cast collisions are slower to perform, but will test for collisions that
        /// may be parallel to the bone axis due to the radius of the bone. We do this check
        /// after the previous one.
        /// </summary>
        /// <param name="rIndex"></param>
        /// <returns>Determines if a collision occured or not</returns>
        private bool ProcessBoneCollisions(int rIndex)
        {
            // Ensure the ignore list is built
            if (mBoneTransforms.Count == 0 && mBones.Count > 0)
            {
                if (mBones[0].Parent != null)
                {
                    mBoneTransforms.Add(mBones[0].Parent._Transform);
                }

                for (int i = 0; i < mBones.Count; i++)
                {
                    mBoneTransforms.Add(mBones[i]._Transform);
                }
            }

            // Test for collisions
            float lBoneRadius = 0.02f;
            float lBoneLength = _BoneInfo[rIndex].Length;
            Vector3 lBoneVector = _BoneInfo[rIndex + 1].Position - _BoneInfo[rIndex].Position;
            Vector3 lBoneForward = lBoneVector.normalized;

            Vector3 lBoneStart = _BoneInfo[rIndex].Position + (lBoneForward * lBoneRadius);
            //Vector3 lBoneEnd = _BoneInfo[rIndex].Position + (lBoneForward * (lBoneLength - lBoneRadius));

            // Test if there is a collision within the start of the bone. If so we don't need to go on.
            Collider[] lColliders = null;
            int lHits = RaycastExt.SafeOverlapSphere(lBoneStart, lBoneRadius, out lColliders, _CollisionLayers, null, mBoneTransforms);
            if (lColliders != null && lHits > 0)
            {
                return true;
            }

            // Do a sphere cast down the length of the bone
            RaycastHit[] lHitArray = null;
            lHits = RaycastExt.SafeSphereCastAll(lBoneStart, lBoneForward, lBoneRadius, out lHitArray, lBoneLength - (lBoneRadius * 2f), _CollisionLayers, null, mBoneTransforms);
            if (lHits > 0)
            {
                return true;
            }

            // No collision
            return false;
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

            // Set the collision flag
            bool lNewIsCollisionEnabled = EditorGUILayout.Toggle(new GUIContent("Is Collison Enabled", "Determines if the links will adjust for collision."), _IsCollisionEnabled);
            if (lNewIsCollisionEnabled != _IsCollisionEnabled)
            {
                lIsDirty = true;
                _IsCollisionEnabled = lNewIsCollisionEnabled;
            }

            if (IsCollisionEnabled)
            {
                int lNewCollisionLayers = EditorHelper.LayerMaskField(new GUIContent("Collision Layers", "Layers that the links can collide with."), CollisionLayers);
                if (lNewCollisionLayers != CollisionLayers)
                {
                    lIsDirty = true;
                    CollisionLayers = lNewCollisionLayers;
                }
            }

            GUILayout.Space(5f);

            // Set the collision flag
            bool lNewIsGravityEnabled = EditorGUILayout.Toggle(new GUIContent("Is Gravity Enabled", "Gravity is good for non-modeled chains. If a pony-tail (for example), is modeled down as if gravity is effecting it, you don't need gravity."), IsGravityEnabled);
            if (lNewIsGravityEnabled != IsGravityEnabled)
            {
                lIsDirty = true;
                IsGravityEnabled = lNewIsGravityEnabled;
            }

            // Set the gravity
            Vector3 lNewGravity = EditorGUILayout.Vector3Field(new GUIContent("Gravity", "Gravity to apply."), _Gravity);
            if (lNewGravity != _Gravity)
            {
                lIsDirty = true;
                _Gravity = lNewGravity;
            }

            // Determine how gravity is applied
            AnimationCurve lNewGravityImpact = EditorGUILayout.CurveField(new GUIContent("Gravity Impact", "Determines how gravity is applied along the length of the chain"), _GravityImpact);
            if (lNewGravityImpact != _GravityImpact)
            {
                lIsDirty = true;
                _GravityImpact = lNewGravityImpact;
            }

            // Determine how stiffness factor is applied
            AnimationCurve lNewStiffness = EditorGUILayout.CurveField(new GUIContent("Stiffness", "Stiffness applied along the length of the chain"), _Stiffness);
            if (lNewStiffness != _Stiffness)
            {
                lIsDirty = true;
                _Stiffness = lNewStiffness;
            }

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

            GUILayout.Space(5);

            EditorGUILayout.LabelField("Bone List:");

            // Force the selected bone based on the input list
            bool lIsListDirty = RenderBoneList(mBones, rSelectedBones, true);
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

            // Set the collision flag
            bool lNewUseBindPosition = EditorGUILayout.Toggle(new GUIContent("Use Bind Position", "Determines if the movement is based on the current position or bind position."), _BoneInfo[rIndex].UseBindPosition);
            if (lNewUseBindPosition != _BoneInfo[rIndex].UseBindPosition)
            {
                lIsDirty = true;
                _BoneInfo[rIndex].UseBindPosition = lNewUseBindPosition;
            }

            // Set the bone yaw
            float lNewRotationLerp = EditorGUILayout.FloatField(new GUIContent("Drag Lerp", "Determines how quickly we rotate to the target."), _BoneInfo[rIndex].RotationLerp);
            if (lNewRotationLerp != _BoneInfo[rIndex].RotationLerp)
            {
                lIsDirty = true;
                _BoneInfo[rIndex].RotationLerp = lNewRotationLerp;
            }

            // Set the bone yaw
            float lNewUntwistLerp = EditorGUILayout.FloatField(new GUIContent("Untwist Lerp", "Determines how quickly we return to the bind twist. Setting to 0 disables untwisting."), _BoneInfo[rIndex].UntwistLerp);
            if (lNewUntwistLerp != _BoneInfo[rIndex].UntwistLerp)
            {
                lIsDirty = true;
                _BoneInfo[rIndex].UntwistLerp = lNewUntwistLerp;
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
            if (rBone == null || rBone._Transform == null) { return; }

            base.AddBone(rBone, rIncludeChildren);

            AddBoneInfo(mBones.IndexOf(rBone), rBone);
        }

        /// <summary>
        /// Add information specific to the bone. This helps us manage the bone
        /// </summary>
        /// <param name="rBone"></param>
        private void AddBoneInfo(int rIndex, BoneControllerBone rBone)
        {
            // Remember we want one more bone info than
            // we have bones.
            while (_BoneInfo.Count <= rIndex)
            {
                _BoneInfo.Add(new BoneChainDragBone());
            }

            if (rBone != null)
            {
                BoneChainDragBone lBoneInfo = _BoneInfo[rIndex];

                // Reset the values
                lBoneInfo.Position = rBone._Transform.position;
                lBoneInfo.PrevPosition = rBone._Transform.position;
                lBoneInfo.RotationTarget = rBone.Transform.rotation * rBone.ToBoneForward;
                lBoneInfo.Rotation = lBoneInfo.RotationTarget;

                lBoneInfo.Length = rBone.Length;
                CapsuleCollider lCollider = rBone._Transform.GetComponent<CapsuleCollider>();
                if (lCollider != null) { lBoneInfo.Length = lCollider.height * rBone._Transform.lossyScale.y; }

                // Record the transforms for collision testing
                if (!mBoneTransforms.Contains(rBone._Transform))
                {
                    mBoneTransforms.Add(rBone._Transform);
                }
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

                if (rBone != null)
                {
                    mBoneTransforms.Remove(rBone._Transform);
                }

                base.RemoveBone(rBone, rIncludeChildren);
            }

            // Extra precaution to ensure we stay in synch
            if (mBones.Count == 0)
            {
                _BoneInfo.Clear();
                mBoneTransforms.Clear();
            }
        }

        // ************************************** SUPPORT CLASSES **************************************

        /// <summary>
        /// Contains information about the bone twist and weighting to use when
        /// passing the twist to the next bone
        /// </summary>
        [Serializable]
        public class BoneChainDragBone
        {
            /// <summary>
            /// Position the bone should be this frame
            /// </summary>
            public Vector3 Position = Vector3.zero;

            /// <summary>
            /// Position of the bone last frame
            /// </summary>
            public Vector3 PrevPosition = Vector3.zero;

            /// <summary>
            /// Velocity that gets the bone from the previous
            /// position to the current position
            /// </summary>
            public Vector3 Velocity = Vector3.zero;

            /// <summary>
            /// Determines if we're colliding
            /// </summary>
            public bool Collision = false;

            /// <summary>
            /// Determines if we use the bind position as the base
            /// </summary>
            public bool UseBindPosition = true;

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
            public float RotationLerp = 1f;

            /// <summary>
            /// Determines how quickly we attempt to untwist back to the original twist rotation
            /// </summary>
            public float UntwistLerp = 0.2f;

            /// <summary>
            /// Length of the collider that wraps the bone (if there is one).
            /// We'll use this for our collision test instead of just the bone.
            /// </summary>
            public float Length = 0f;

            /// <summary>
            /// Constructor
            /// </summary>
            public BoneChainDragBone()
            {
            }
        }
    }
}
