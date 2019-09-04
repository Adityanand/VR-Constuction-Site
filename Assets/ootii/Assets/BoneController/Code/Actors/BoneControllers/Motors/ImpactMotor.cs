using System;
using System.Collections.Generic;
using UnityEngine;
using com.ootii.Collections;
using com.ootii.Geometry;
using com.ootii.Physics;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace com.ootii.Actors.BoneControllers
{
    /// <summary>
    /// The impact motor allows for the skeleton to react
    /// to forces that push against the bones. Think of bullets, swords, or explosions.
    /// </summary>
    [Serializable]
    [IKName("Impact Motor")]
    [IKDescription("Allows the skeleton to react as if it were hit by bullets, swords, explosions, etc.")]
    public class ImpactMotor : BoneControllerMotor
    {
        /// <summary>
        /// Determines if we testimpact using all bones or just specific ones
        /// </summary>
        public bool _UseAllBones = false;
        public bool UseAllBones
        {
            get { return _UseAllBones; }
            set { _UseAllBones = value; }
        }

        /// <summary>
        /// When using all bones, the minimum length of the bone that can be used
        /// </summary>
        public float _MinBoneLength = 0.08f;
        public float MinBoneLength
        {
            get { return _MinBoneLength; }
            set { _MinBoneLength = value; }
        }

        /// <summary>
        /// Defines the lerp we use to go from the current pose to the impact pose
        /// </summary>
        public float _Power = 0.1f;
        public float Power
        {
            get { return _Power; }
            set { _Power = value; }
        }

        /// <summary>
        /// Defines the amount of time (in seconds) to go into the impact pose
        /// </summary>
        public float _ImpactTime = 0.1f;
        public float ImpactTime
        {
            get { return _ImpactTime; }
            set { _ImpactTime = value; }
        }

        /// <summary>
        /// Defines the amount of time (in seconds) to recover from the impact pose
        /// </summary>
        public float _RecoveryTime = 0.2f;
        public float RecoveryTime
        {
            get { return _RecoveryTime; }
            set { _RecoveryTime = value; }
        }

        /// <summary>
        /// Determines how the recovery occurs so that it isn't linear
        /// </summary>
        public AnimationCurve _RecoveryCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
        public AnimationCurve RecoveryCurve
        {
            get { return _RecoveryCurve; }
            set { _RecoveryCurve = value; }
        }

        /// <summary>
        /// Number of parent bones to spread the impact to
        /// </summary>
        public int _ParentSpread = 1;
        public int ParentSpread
        {
            get { return _ParentSpread; }
            set { _ParentSpread = value; }
        }

        /// <summary>
        /// Amount to change the impact by with each generation
        /// </summary>
        public float _ParentDamping = 0.5f;
        public float ParentDamping
        {
            get { return _ParentDamping; }
            set { _ParentDamping = value; }
        }

        /// <summary>
        /// Number of child generations to spread the impact to
        /// </summary>
        public int _ChildSpread = 1;
        public int ChildSpread
        {
            get { return _ChildSpread; }
            set { _ChildSpread = value; }
        }

        /// <summary>
        /// Number of child generations to spread the impact to
        /// </summary>
        public float _ChildDamping = 0.5f;
        public float ChildDamping
        {
            get { return _ChildDamping; }
            set { _ChildDamping = value; }
        }

        /// <summary>
        /// Extra information in order to help the management of the bones.
        /// </summary>
        public List<ImpactMotorBone> _BoneInfo = new List<ImpactMotorBone>();

        /// <summary>
        /// We don't serialize this list, but this is the one we actually run. The reason is
        /// that we support 'temporary' bones. These aren't permanently stored and come and go.
        /// </summary>
        private Dictionary<BoneControllerBone, ImpactMotorBone> mActiveBoneInfo = new Dictionary<BoneControllerBone, ImpactMotorBone>();

        /// <summary>
        /// Tracks bone info to remove
        /// </summary>
        private List<BoneControllerBone> mInactiveBoneInfo = new List<BoneControllerBone>();

        /// <summary>
        /// Default constructor
        /// </summary>
        public ImpactMotor()
            : base()
        {
        }

        /// <summary>
        /// Skeleton constructor
        /// </summary>
        /// <param name="rSkeleton">Skeleton the motor is driving</param>
        public ImpactMotor(BoneController rSkeleton)
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
            mInactiveBoneInfo.Clear();

            // Process the results of the solve. We use the enumerator to
            // avoid garbage from the ForEach
            Dictionary<BoneControllerBone, ImpactMotorBone>.Enumerator lEnumerator = mActiveBoneInfo.GetEnumerator();
            while (lEnumerator.MoveNext())
            {
                BoneControllerBone lBone = lEnumerator.Current.Key;

                Vector3 lChange = Vector3.zero;
                ImpactMotorBone lBoneInfo = mActiveBoneInfo[lBone];

                // If we're not in a state, move on
                if (lBoneInfo.State == 0)
                {
                    continue;
                }
                // If we're recovering from the impact, decrease the lerp over time
                else if (lBoneInfo.State == 1)
                {
                    lBoneInfo.Time -= rDeltaTime;

                    float lTime = 1f - Mathf.Clamp01(lBoneInfo.Time / _ImpactTime);
                    lChange = lBoneInfo.Change * lTime;

                    if (lBoneInfo.Time <= 0f)
                    {
                        lBoneInfo.State = 2;
                        lBoneInfo.Time = _RecoveryTime;
                    }
                }
                else if (lBoneInfo.State == 2)
                {
                    lBoneInfo.Time -= rDeltaTime;

                    float lTime = Mathf.Clamp01(lBoneInfo.Time / _RecoveryTime);
                    lChange = lBoneInfo.Change * lTime;

                    if (lBoneInfo.Time <= 0f)
                    {
                        lBoneInfo.State = 3;
                        mInactiveBoneInfo.Add(lBone);
                    }
                }

                // Set the new position based on the state
                lBone.SetWorldEndPosition(lBone.WorldEndPosition + lChange, 1f);
            }

            //// Report the rotation to reach the new pose
            //foreach (BoneControllerBone lBone in mActiveBoneInfo.Keys)
            //{
            //    Vector3 lChange = Vector3.zero;
            //    ImpactMotorBone lBoneInfo = mActiveBoneInfo[lBone];

            //    // If we're not in a state, move on
            //    if (lBoneInfo.State == 0) 
            //    { 
            //        continue; 
            //    }
            //    // If we're recovering from the impact, decrease the lerp over time
            //    else if (lBoneInfo.State == 1)
            //    {
            //        lBoneInfo.Time -= rDeltaTime;

            //        float lTime = 1f - Mathf.Clamp01(lBoneInfo.Time / _ImpactTime);
            //        lChange = lBoneInfo.Change * lTime;

            //        if (lBoneInfo.Time <= 0f)
            //        {
            //            lBoneInfo.State = 2;
            //            lBoneInfo.Time = _RecoveryTime;
            //        }
            //    }
            //    else if (lBoneInfo.State == 2)
            //    {
            //        lBoneInfo.Time -= rDeltaTime;

            //        float lTime = Mathf.Clamp01(lBoneInfo.Time / _RecoveryTime);
            //        lChange = lBoneInfo.Change * lTime;

            //        if (lBoneInfo.Time <= 0f)
            //        {
            //            lBoneInfo.State = 3;
            //            mInactiveBoneInfo.Add(lBone);
            //        }
            //    }

            //    // Set the new position based on the state
            //    lBone.SetWorldEndPosition(lBone.WorldEndPosition + lChange, 1f);
            //}
            
            // Clean up ones we need to
            for (int i = mInactiveBoneInfo.Count - 1; i >= 0; i--)
            {
                BoneControllerBone lBone = mInactiveBoneInfo[i];
                if (mActiveBoneInfo.ContainsKey(lBone))
                {
                    ImpactMotorBone.Release(mActiveBoneInfo[lBone]);
                    mActiveBoneInfo.Remove(lBone);
                }
            }
        }

        /// <summary>
        /// Shoots a ray into the character that simply tests for a hit
        /// </summary>
        /// <param name="rOrigin"></param>
        /// <param name="rDirection"></param>
        /// <param name="rRange"></param>
        /// <param name="rForce"></param>
        public BoneControllerBone Raycast(Vector3 rOrigin, Vector3 rVelocity, float rRange, bool rStopIfBlocked, ref Vector3 lHitPoint)
        {
            int lHitIndex = 0;
            BoneControllerBone lHitBone = null;

            //float lSpeed = rVelocity.magnitude;
            Vector3 lDirection = rVelocity.normalized;

            // Cast the ray and see if we hit a bone we care about
            //RaycastHit[] lHits = RaycastExt.SafeRaycastAll(rOrigin, lDirection, rRange).OrderBy(h => h.distance).ToArray();
            //RaycastHit[] lHits = RaycastExt.SafeRaycastAll(rOrigin, lDirection, rRange, false);

            RaycastHit[] lHits = null;
            int lHitCount = RaycastExt.SafeRaycastAll(rOrigin, lDirection, out lHits, rRange);
            if (lHitCount > 1) { RaycastExt.Sort(lHits, lHitCount); }

            for (lHitIndex = 0; lHitIndex < lHitCount; lHitIndex++)
            {
                if (_UseAllBones)
                {
                    lHitBone = mSkeleton.GetBone(lHits[lHitIndex].collider.transform) as BoneControllerBone;
                }
                else
                {
                    for (int i = 0; i < mBones.Count; i++)
                    {
                        // Grab the collider on the bone (if there is one)
                        Collider lBaseCollider = mBones[i]._Transform.GetComponent<Collider>();

                        if (lHits[lHitIndex].collider == lBaseCollider)
                        {
                            lHitBone = mBones[i];
                            break;
                        }
                    }
                }

                if (lHitBone != null) { break; }

                // If our first hit isn't a bone, then we're blocked
                if (rStopIfBlocked) { return null; }
            }

            // Start grabbing the hit info
            if (lHitBone != null)
            {
                lHitPoint = lHits[lHitIndex].point;
            }

            // Return the fact if we hit a bone or not
            return lHitBone;
        }

        /// <summary>
        /// Shoots a ray into the character that will impact a bone and cause the
        /// bone to react to the force.
        /// </summary>
        /// <param name="rOrigin"></param>
        /// <param name="rDirection"></param>
        /// <param name="rRange"></param>
        /// <param name="rForce"></param>
        public BoneControllerBone RaycastImpact(Vector3 rOrigin, Vector3 rVelocity, float rRange, float rMass, bool rStopIfBlocked, ref Vector3 rHitPoint)
        {
            BoneControllerBone lHitBone = null;

            //float lSpeed = rVelocity.magnitude;
            Vector3 lDirection = rVelocity.normalized;

            // Grab the bones to test
            List<BoneControllerBone> lBones = (_UseAllBones ? mSkeleton.Bones : mBones);
            if (lBones == null || lBones.Count == 0) { return null; }

            // Start testing the bones
            for (int i = 0; i < lBones.Count; i++)
            {
                Vector3 lHitPoint;
                BoneControllerBone lBone = lBones[i];

                if (lBone.TestRayCollision(rOrigin, lDirection, rRange, out lHitPoint))
                {
                    lHitBone = lBone;
                    rHitPoint = lHitPoint;

                    if (!AddTemporaryBone(lHitBone))
                    {
                        lHitBone = null;
                    }

                    if (lHitBone != null)
                    {
                        // With the bone hit, we need to apply the impact. This
                        // impact will actually happen over time (hit, react, and recover)
                        float lHitDistance = Vector3.Distance(lHitBone._Transform.position, rHitPoint);
                        //float lHitSpan = lHitDistance / lHitBone.Length;

                        Vector3 lBoneReference = lHitBone._Transform.position + (lHitBone._Transform.rotation * (lHitBone.BoneForward * lHitDistance));

                        PhysicsObject lBonePO = PhysicsObject.Allocate();
                        lBonePO.Mass = 4.53f; // 4.5359237 kg arm
                        lBonePO.Position = lBoneReference;
                        lBonePO.Velocity = Vector3.zero;

                        PhysicsObject lRayPO = PhysicsObject.Allocate();
                        lRayPO.Mass = rMass;
                        lRayPO.Position = rHitPoint;
                        lRayPO.Velocity = rVelocity;

                        PhysicsExt.SolveSphericalCollision(ref lBonePO, ref lRayPO, 0.5f);

                        mActiveBoneInfo[lHitBone].State = 1;
                        mActiveBoneInfo[lHitBone].Time = _ImpactTime;
                        mActiveBoneInfo[lHitBone].Change = lBonePO.Velocity * _Power;

                        if (lHitBone.Parent != null)
                        {
                            Vector3 lChange = mActiveBoneInfo[lHitBone].Change;
                            ApplyImpactToParent(lHitBone, lChange, _ParentSpread, _ParentDamping);
                        }

                        if (lHitBone.Children != null && lHitBone.Children.Count > 0)
                        {
                            Vector3 lChange = -mActiveBoneInfo[lHitBone].Change;
                            ApplyImpactToChildren(lHitBone, lChange, _ChildSpread, _ChildDamping);
                        }

                        // Release our objects
                        PhysicsObject.Release(lBonePO);
                        PhysicsObject.Release(lRayPO);
                    }
                }
            }

            // Report the bone that was hit
            return lHitBone;
        }

        /// <summary>
        /// Spreads the impact to parent bones
        /// </summary>
        /// <param name="rChild">Child who will start spreading</param>
        /// <param name="rChange">Change to apply to the parent</param>
        /// <param name="rDepthRemaining">Determines how far we spread the change</param>
        /// <param name="rDamping">Determines how much we dull the change on each iteration</param>
        private void ApplyImpactToParent(BoneControllerBone rChild, Vector3 rChange, int rDepthRemaining, float rDamping)
        {
            BoneControllerBone lParent = rChild.Parent;
            while (lParent != null && rDepthRemaining > 0)
            {
                rDepthRemaining--;
                rChange = rChange * rDamping;

                if (AddTemporaryBone(lParent))
                {
                    mActiveBoneInfo[lParent].State = 1;
                    mActiveBoneInfo[lParent].Time = _ImpactTime;
                    mActiveBoneInfo[lParent].Change = rChange;

                    lParent = lParent.Parent;
                }
                else { return; }
            }
        }

        /// <summary>
        /// Spreads the impact through to the children
        /// </summary>
        /// <param name="rParent">Parent whose children will be impacted</param>
        /// <param name="rChange">Change being applied</param>
        /// <param name="rDepthRemaining">Determines how deep we go</param>
        /// <param name="rDamping">Determines how much we dull the change on each iteration</param>
        private void ApplyImpactToChildren(BoneControllerBone rParent, Vector3 rChange, int rDepthRemaining, float rDamping)
        {
            rDepthRemaining--;
            rChange = rChange * rDamping;

            for (int i = 0; i < rParent.Children.Count; i++)
            {
                BoneControllerBone lChild = rParent.Children[i];
                if (AddTemporaryBone(lChild))
                {
                    // We're going to temper the child response based on the
                    // length compared to the original.
                    float lCompare = Mathf.Min(lChild.Length / rParent.Length, 1f);
                    rChange = rChange * lCompare;

                    mActiveBoneInfo[lChild].State = 1;
                    mActiveBoneInfo[lChild].Time = _ImpactTime;
                    mActiveBoneInfo[lChild].Change = rChange;

                    if (rDepthRemaining > 0)
                    {
                        ApplyImpactToChildren(lChild, rChange, rDepthRemaining, rDamping);
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

            EditorGUILayout.BeginHorizontal();

            // Power
            bool lNewUseAllBones = EditorGUILayout.Toggle(new GUIContent("Use All Bones", "Determines if we impact with all bones or a limited set."), _UseAllBones);
            if (lNewUseAllBones != _UseAllBones)
            {
                lIsDirty = true;
                _UseAllBones = lNewUseAllBones;
            }

            GUILayout.Space(20);
            EditorGUILayout.LabelField(new GUIContent("Min Length", "Minimum bone length that will be impacted."), GUILayout.Width(70));
            float lNewMinBoneLength = EditorGUILayout.FloatField(_MinBoneLength, GUILayout.Width(65));
            if (lNewMinBoneLength != _MinBoneLength)
            {
                lIsDirty = true;
                _MinBoneLength = lNewMinBoneLength;
            }

            GUILayout.FlexibleSpace();

            EditorGUILayout.EndHorizontal();

            // Power
            float lNewPower = EditorGUILayout.FloatField(new GUIContent("Power", "Impact is based on velocity and mass, but we can modify the results with this factor."), _Power);
            if (lNewPower != _Power)
            {
                lIsDirty = true;
                _Power = lNewPower;
            }

            // The time it takes to reach the full extent of the impact
            float lNewImpactTime = EditorGUILayout.FloatField(new GUIContent("Impact Time", "Time (in seconds) to reach the full extent of the impact."), _ImpactTime);
            if (lNewImpactTime != _ImpactTime)
            {
                lIsDirty = true;
                _ImpactTime = lNewImpactTime;
            }

            // The time it takes to recover from the extent of the impact
            float lNewRecoveryTime = EditorGUILayout.FloatField(new GUIContent("Recovery Time", "Time (in seconds) to recover from the impact."), _RecoveryTime);
            if (lNewRecoveryTime != _RecoveryTime)
            {
                lIsDirty = true;
                _RecoveryTime = lNewRecoveryTime;
            }

            // Determine how stiffness factor is applied
            AnimationCurve lNewRecoveryCurve = EditorGUILayout.CurveField(new GUIContent("Recovery Curve", "Determines how the recovery occurs so it doesn't have to be linear."), _RecoveryCurve);
            if (lNewRecoveryCurve != _RecoveryCurve)
            {
                lIsDirty = true;
                _RecoveryCurve = lNewRecoveryCurve;
            }

            EditorGUILayout.BeginHorizontal();

            int lNewParentSpread = EditorGUILayout.IntField(new GUIContent("Parent Spread", "Number of parents to spread the impact to."), _ParentSpread);
            if (lNewParentSpread != _ParentSpread)
            {
                lIsDirty = true;
                _ParentSpread = lNewParentSpread;
            }

            GUILayout.Space(5);
            EditorGUILayout.LabelField(new GUIContent("Damping", "Amount to change impact by on each parent."), GUILayout.Width(50));
            float lNewParentDamping = EditorGUILayout.FloatField(_ParentDamping, GUILayout.Width(65));
            if (lNewParentDamping != _ParentDamping)
            {
                lIsDirty = true;
                _ParentDamping = lNewParentDamping;
            }

            GUILayout.FlexibleSpace();

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();

            int lNewChildSpread = EditorGUILayout.IntField(new GUIContent("Child Spread", "Number of generations to spread the impact to."), _ChildSpread);
            if (lNewChildSpread != _ChildSpread)
            {
                lIsDirty = true;
                _ChildSpread = lNewChildSpread;
            }

            GUILayout.Space(5);
            EditorGUILayout.LabelField(new GUIContent("Damping", "Amount to change impact by on each generation."), GUILayout.Width(50));
            float lNewChildDamping = EditorGUILayout.FloatField(_ChildDamping, GUILayout.Width(65));
            if (lNewChildDamping != _ChildDamping)
            {
                lIsDirty = true;
                _ChildDamping = lNewChildDamping;
            }

            GUILayout.FlexibleSpace();

            EditorGUILayout.EndHorizontal();

            // Load bones if they are invalid
            if (mBones == null || mBones.Count == 0) { LoadBones(); }

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
                ImpactMotorBone lBoneInfo = new ImpactMotorBone();

                if (rBone != null && rBone._Transform != null)
                {
                    lBoneInfo.EndPosition = rBone._Transform.position + (rBone._Transform.rotation * (rBone.BoneForward * rBone.Length));
                }

                _BoneInfo.Insert(mBones.IndexOf(rBone), lBoneInfo);
            }

            // Set the bone weight
            float lOldBoneWeight = (rBone == null ? 0 : _BoneInfo[rIndex].Weight);
            float lNewBoneWeight = EditorGUILayout.FloatField(new GUIContent("Rotation Weight", "Normalized weight this bone will be responsible for."), lOldBoneWeight);
            if (lNewBoneWeight != lOldBoneWeight)
            {
                if (rBone != null)
                {
                    lIsDirty = true;
                    _BoneInfo[rIndex].Weight = lNewBoneWeight;
                }
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

            ImpactMotorBone lBoneInfo = new ImpactMotorBone();
            lBoneInfo.IsTemporary = false;

            if (rBone != null && rBone._Transform != null)
            {
                lBoneInfo.EndPosition = rBone._Transform.position + (rBone._Transform.rotation * (rBone.BoneForward * rBone.Length));
            }

            _BoneInfo.Insert(mBones.IndexOf(rBone), lBoneInfo);
        }

        /// <summary>
        /// Allows the motor to process bones temporarily and removes them once done
        /// </summary>
        /// <param name="rBone"></param>
        protected virtual bool AddTemporaryBone(BoneControllerBone rBone)
        {
            if (rBone == null || rBone._Transform == null) { return false; }
            if (rBone.Length < _MinBoneLength) { return false; }
            if (rBone == mSkeleton.Root) { return false; }

            if (mActiveBoneInfo.ContainsKey(rBone)) { return true; }
            
            ImpactMotorBone lBoneInfo = ImpactMotorBone.Allocate();

            lBoneInfo.IsTemporary = true;
            lBoneInfo.EndPosition = rBone._Transform.position + (rBone._Transform.rotation * (rBone.BoneForward * rBone.Length));

            mActiveBoneInfo.Add(rBone, lBoneInfo);

            return true;
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
        public class ImpactMotorBone
        {
            /// <summary>
            /// Determines if this bone is temorary and will be
            /// removed once the impact ends
            /// </summary>
            public bool IsTemporary = true;

            /// <summary>
            /// Determines what state we're in. 
            /// 0 = NONE
            /// 1 = IMPACT
            /// 2 = RECOVER
            /// 3 = DONE
            /// </summary>
            public int State = 0;

            /// <summary>
            /// Time the values were last set
            /// </summary>
            public float Time = 0f;

            /// <summary>
            /// Defines how much the bone should change
            /// </summary>
            public Vector3 Change = Vector3.zero;

            /// <summary>
            /// Position the bone's end should be this frame
            /// </summary>
            public Vector3 EndPosition = Vector3.zero;

            /// <summary>
            /// How much does this motor contribute
            /// </summary>
            public float Weight = 1f;

            /// <summary>
            /// Constructor
            /// </summary>
            public ImpactMotorBone()
            {
            }

            // ******************************** OBJECT POOL ********************************

            /// <summary>
            /// Allows us to reuse objects without having to reallocate them over and over
            /// </summary>
            private static ObjectPool<ImpactMotorBone> sPool = new ObjectPool<ImpactMotorBone>(20, 5);

            /// <summary>
            /// Pulls an object from the pool.
            /// </summary>
            /// <returns></returns>
            public static ImpactMotorBone Allocate()
            {
                // Grab the next available object
                ImpactMotorBone lInstance = sPool.Allocate();

                // Initialize
                lInstance.State = 0;
                lInstance.Time = 0f;
                lInstance.Change = Vector3.zero;
                lInstance.EndPosition = Vector3.zero;
                lInstance.Weight = 1f;

                return lInstance;
            }

            /// <summary>
            /// Returns an element back to the pool.
            /// </summary>
            /// <param name="rEdge"></param>
            public static void Release(ImpactMotorBone rInstance)
            {
                sPool.Release(rInstance);
            }
        }
    }
}
