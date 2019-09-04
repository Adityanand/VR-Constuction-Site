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
    /// As a character stands or walks, we want to ensure foot
    /// placement meets the ground correctly. This does that.
    /// </summary>
    [Serializable]
    [IKName("Foot to Ground 2 Bone Motor")]
    [IKDescription("This motor will adjust foot placement and rotation in order to meet the ground under the character")]
    public class FootGround2BoneMotor : BoneControllerMotor
    {
        /// <summary>
        /// Minimum distance required for us to attempt to move the bones
        /// </summary>
        public const float MIN_TARGET_DISTANCE = 0.0001f;

        /// <summary>
        /// Collision support to keep us from reallocating
        /// </summary>
        private static RaycastHit sCollisionInfo1;
        private static RaycastHit sCollisionInfo2;

        /// <summary>
        /// Ground layers
        /// </summary>
        public int _GroundingLayers = 1;
        public int GroundingLayers
        {
            get { return _GroundingLayers; }
            set { _GroundingLayers = value; }
        }

        /// <summary>
        /// Determines if the limb reaches and twists based on it's current 
        /// rotation or the bind rotation
        /// </summary>
        public bool _UseBindRotation = false;
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
        /// Determines if the leg will extend to reach the ground position
        /// </summary>
        public bool _AllowLegExtension = false;
        public bool AllowLegExtension
        {
            get { return _AllowLegExtension; }
            set { _AllowLegExtension = value; }
        }

        /// <summary>
        /// TRT 02/04/2016 a - Added to editor
        /// 
        /// Given the delta between the knee-and-foot vs. knee-and-target,
        /// this is the max delta before we disable IK. This is used to support
        /// animations that curl the leg and lift the foot.
        /// </summary>
        public float _MaxDeltaDistance = 0.02f;
        public float MaxDeltaDistance
        {
            get { return _MaxDeltaDistance; }
            set { _MaxDeltaDistance = value; }
        }

        /// <summary>
        /// Determines if we'll rotate the foot to settle on the ground's normal
        /// </summary>
        public bool _RotateFootToGround = true;
        public bool RotateFootToGround
        {
            get { return _RotateFootToGround; }
            set { _RotateFootToGround = value; }
        }

        /// <summary>
        /// When rotating foot to ground, determines if we need both the
        /// heel and toe to collide
        /// </summary>
        public bool _RotateFootRequiresBoth = true;
        public bool RotateFootRequiresBoth
        {
            get { return _RotateFootRequiresBoth; }
            set { _RotateFootRequiresBoth = value; }
        }

        /// <summary>
        /// When the character is moving, we typically don't want to control
        /// the feet rotation. This keep us from getting floppy foot.
        /// </summary>
        public bool _RotateFootOnMovement = false;
        public bool RotateFootOnMovement
        {
            get { return _RotateFootOnMovement; }
            set { _RotateFootOnMovement = value; }
        }

        /// <summary>
        /// Minimum ground angle before we start rotating the foot
        /// </summary>
        public float _RotateFootToGroundMinAngle = 4f;
        public float RotateFootToGroundMinAngle
        {
            get { return _RotateFootToGroundMinAngle; }
            set { _RotateFootToGroundMinAngle = value; }
        }

        /// <summary>
        /// Vertical distance between the foot joint and the toe joint
        /// </summary>
        public float _FootToeDistance = 0f;
        public float FootToeDistance
        {
            get { return _FootToeDistance; }
            set { _FootToeDistance = value; }
        }

        /// <summary>
        /// Distance between the sole of the foot and the toe. This
        /// is used to ensure our ground test can actually get to the ground.
        /// </summary>
        public float _ToeSoleDistance = 0.03f;
        public float ToeSoleDistance
        {
            get { return _ToeSoleDistance; }
            set { _ToeSoleDistance = value; }
        }

        /// <summary>
        /// Vertical distance above the foot bone where we'll start the raycast
        /// </summary>
        public float _RaycastStartDistance = 0.9f;
        public float RaycastStartDistance
        {
            get { return _RaycastStartDistance; }
            set { _RaycastStartDistance = value; }
        }

        /// <summary>
        /// Vertical distance below the toe bone where we'll end the raycast
        /// </summary>
        public float _RaycastExtensionDistance = 0.02f;
        public float RaycastExtensionDistance
        {
            get { return _RaycastExtensionDistance; }
            set { _RaycastExtensionDistance = value; }
        }

        /// <summary>
        /// Extra information in order to help the management of the bones
        /// </summary>
        public List<FootPlacementMotorBone> _BoneInfo = new List<FootPlacementMotorBone>();

        /// <summary>
        /// Rotation that gets the foot from the "typical forward" to the bind position
        /// </summary>
        public Quaternion _FootForwardToBind = Quaternion.identity;

        /// <summary>
        /// Track the last player's position for movement
        /// </summary>
        protected Vector3 mLastPosition = Vector3.zero;

        /// <summary>
        /// Default constructor
        /// </summary>
        public FootGround2BoneMotor()
            : base()
        {
        }

        /// <summary>
        /// Skeleton constructor
        /// </summary>
        /// <param name="rSkeleton">Skeleton the motor is driving</param>
        public FootGround2BoneMotor(BoneController rSkeleton)
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
        /// Automatically loads bones for the developer. This is typically done by using things like
        /// the HumanBodyBones.
        /// </summary>
        /// <param name="rStyle">String that can be used to define how to load bones</param>
        public virtual void AutoLoadBones(string rStyle)
        {
            rStyle = rStyle.ToLower();

            mBones.Clear();
            _BoneInfo.Clear();

            if (rStyle == "humanoid left")
            {
                AddBone(Skeleton.GetBone(HumanBodyBones.LeftUpperLeg) as BoneControllerBone, false);
                AddBone(Skeleton.GetBone(HumanBodyBones.LeftLowerLeg) as BoneControllerBone, false);
                AddBone(Skeleton.GetBone(HumanBodyBones.LeftFoot) as BoneControllerBone, false);
                AddBone(Skeleton.GetBone(HumanBodyBones.LeftToes) as BoneControllerBone, false);
            }
            else if (rStyle == "humanoid right")
            {
                AddBone(Skeleton.GetBone(HumanBodyBones.RightUpperLeg) as BoneControllerBone, false);
                AddBone(Skeleton.GetBone(HumanBodyBones.RightLowerLeg) as BoneControllerBone, false);
                AddBone(Skeleton.GetBone(HumanBodyBones.RightFoot) as BoneControllerBone, false);
                AddBone(Skeleton.GetBone(HumanBodyBones.RightToes) as BoneControllerBone, false);
            }

            // Flip the axis on the upper leg
            if (_BoneInfo.Count > 0 && _BoneInfo[0] != null)
            {
                _BoneInfo[0].BendAxis = new Vector3(-1f, 0f, 0f);
            }

            // Determine the rotation to get us from the ground forward to the foot forward
            if (mBones.Count >= 3 && mBones[2] != null)
            {
                _BoneInfo[2].RotationLerp = 0.3f;

                Vector3 lBindGroundForward = Vector3.Cross(mSkeleton.transform.up, mBones[2].WorldBindRotation * -Vector3.right);
                Quaternion lBindForwardRotation = Quaternion.LookRotation(lBindGroundForward, mSkeleton.transform.up);
                _FootForwardToBind = Quaternion.Inverse(lBindForwardRotation) * mBones[2].WorldBindRotation;
            }

            // Ensure we have valid values for the toes
            if (mBones.Count >= 4 && mBones[3] != null)
            {
                _FootToeDistance = mBones[3]._Transform.position.y - mBones[2]._Transform.position.y;
            }
            else
            {
                _FootToeDistance = 0f;
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
            // TRT 02/04/2016 a - Protection for some errors I saw when going from debug to running
            if (mBones.Count < 3) { return; }
            if (mSkeleton == null) { return; }
            if (object.ReferenceEquals(mSkeleton, null)) { return; }
            if (object.ReferenceEquals(mSkeleton.gameObject, null)) { return; }

            // Shortcuts for easy access
            BoneControllerBone lUpperLeg = mBones[0];
            BoneControllerBone lLowerLeg = mBones[1];
            BoneControllerBone lFoot = mBones[2];
            BoneControllerBone lToes = (mBones.Count > 3 ? mBones[3] : null);

            // Get out if we don't have valid bones
            if (lUpperLeg == null || lLowerLeg == null || lFoot == null) { return; }

            // Ensure we have valid values
            if (lToes != null)
            {
                if (_FootToeDistance == 0f) { _FootToeDistance = mBones[3]._Transform.position.y - mBones[2]._Transform.position.y; }
            }

            if (_FootForwardToBind == Quaternion.identity)
            {
                Vector3 lBindGroundForward = Vector3.Cross(mSkeleton.transform.up, lFoot.WorldBindRotation * -Vector3.right);
                Quaternion lBindForwardRotation = Quaternion.LookRotation(lBindGroundForward, mSkeleton.transform.up);
                _FootForwardToBind = Quaternion.Inverse(lBindForwardRotation) * lFoot.WorldBindRotation;
            }

            // Ensure we have the correct amount of bone infos... we should
            while (_BoneInfo.Count < mBones.Count)
            {
                FootPlacementMotorBone lBoneInfo = new FootPlacementMotorBone();
                _BoneInfo.Add(lBoneInfo);
            }

            // If it's time to update, cast out to find the collision point and 
            // generate the new positions.
            if (rUpdate)
            {
                bool lUseCurrentRotation = true;
                Transform lOwnerTransform = mSkeleton.gameObject.transform;

                // Heel cast
                Vector3 lHeelStart = lFoot.Transform.position;
                lHeelStart = lHeelStart + (lOwnerTransform.up * _RaycastStartDistance);

                float lHeelRaycastDistance = _RaycastStartDistance + _FootToeDistance + _ToeSoleDistance + (_AllowLegExtension ? _RaycastExtensionDistance : 0f);
                Vector3 lHeelEnd = lHeelStart - (lOwnerTransform.up * lHeelRaycastDistance);

                bool lHeelCollision = RaycastExt.SafeRaycast(lHeelStart, -lOwnerTransform.up, out sCollisionInfo1, lHeelRaycastDistance, _GroundingLayers, mSkeleton._RootTransform, mSkeleton.BoneTransforms);

                // Toe cast
                bool lToeCollision = false;
                Vector3 lToeEnd = Vector3.zero;

                if (lToes != null)
                {
                    Vector3 lToeStart = lToes.Transform.position;
                    lToeStart = lToeStart + (lOwnerTransform.up * _RaycastStartDistance);

                    float lToeRaycastDistance = _RaycastStartDistance + _ToeSoleDistance + (_AllowLegExtension ? _RaycastExtensionDistance : 0f);
                    lToeEnd = lToeStart - (lOwnerTransform.up * lToeRaycastDistance);

                    lToeCollision = RaycastExt.SafeRaycast(lToeStart, -lOwnerTransform.up, out sCollisionInfo2, lToeRaycastDistance, _GroundingLayers, mSkeleton._RootTransform, mSkeleton.BoneTransforms);
                }

                // Prepare some variables in case we'll need to continue
                Vector3 lFootTarget = Vector3.zero;
                Vector3 lGroundNormal = Vector3.up;

                // We only need to process if there is a collision
                if (lHeelCollision || lToeCollision)
                {
                    lUseCurrentRotation = false;

                    // Test if we actually hit anything
                    bool lUseHeel = true;
                    if (!lHeelCollision || (lToeCollision && (sCollisionInfo2.point.y - lToeEnd.y > sCollisionInfo1.point.y - lHeelEnd.y)))
                    {
                        lUseHeel = false;
                    }

                    lGroundNormal = (lUseHeel ? sCollisionInfo1 : sCollisionInfo2).normal;

                    // Determine the actual foot bone target
                    if (lUseHeel)
                    {
                        lFootTarget = sCollisionInfo1.point + (lOwnerTransform.up * _FootToeDistance);
                    }
                    else
                    {
                        lFootTarget = sCollisionInfo2.point + ((lFoot.Transform.position - lToes.Transform.position).normalized * lFoot.Length);
                    }

                    // If we aren't allowed to extend the leg, but we need to... stop
                    if (!_AllowLegExtension)
                    {
                        // TRT 02/04/2016 a - When than animation curls the leg and pulls the foot up, we
                        // don't want to force the foot to the ground. So, we disable the IK. The problem is
                        // if there's a tiny shuffling animation, we could flicker the IK on and off.

                        float lLegFootAnimationDistance = (lFoot._Transform.position - lUpperLeg._Transform.position).sqrMagnitude;
                        float lLegFootTargetDistance = (lFootTarget - lUpperLeg._Transform.position).sqrMagnitude;

                        //if (lLegFootNew >= lLegFootOld)
                        float lLegDelta = lLegFootTargetDistance - lLegFootAnimationDistance;
                        if (lLegDelta > _MaxDeltaDistance)
                        {
                            lUseCurrentRotation = true;
                        }
                    }
                }

                // If we're using the current rotations, we need to remove the targets that
                // may have been set. We do this so we can smoothly blend to the current rotation
                // as set by animations.
                if (lUseCurrentRotation)
                {
                    if (lUpperLeg != null)
                    {
                        BoneControllerBone lBone = lUpperLeg;
                        FootPlacementMotorBone lBoneInfo = _BoneInfo[mBones.IndexOf(lBone)];

                        lBoneInfo.RotationTarget = lBone.Transform.rotation * lBone.ToBoneForward;
                        lBoneInfo.Rotation = Quaternion.Lerp(lBoneInfo.Rotation, lBoneInfo.RotationTarget, (_IsFixedUpdateEnabled && !mIsFirstUpdate ? lBoneInfo.RotationLerp : 1f));
                        lBone.SetWorldRotation(lBoneInfo.Rotation, _BoneWeight);

                        if (lBone.ApplyLimitsInFrame) { lBone.ApplyLimitsInFrame = _ApplyLimits; }
                    }

                    if (lLowerLeg != null)
                    {
                        BoneControllerBone lBone = lLowerLeg;
                        FootPlacementMotorBone lBoneInfo = _BoneInfo[mBones.IndexOf(lBone)];

                        lBoneInfo.RotationTarget = lBone.Transform.rotation * lBone.ToBoneForward;
                        lBoneInfo.Rotation = Quaternion.Lerp(lBoneInfo.Rotation, lBoneInfo.RotationTarget, (_IsFixedUpdateEnabled && !mIsFirstUpdate ? lBoneInfo.RotationLerp : 1f));
                        lBone.SetWorldRotation(lBoneInfo.Rotation, _BoneWeight);

                        if (lBone.ApplyLimitsInFrame) { lBone.ApplyLimitsInFrame = _ApplyLimits; }
                    }

                    if (lFoot != null && _RotateFootToGround)
                    {
                        RotateFoot(lOwnerTransform, lLowerLeg, lFoot, lToes, lFootTarget, lGroundNormal, lHeelCollision, lToeCollision);
                    }
                }
                // If we get there, we need to bend the legs because there is a collision
                // or an extension
                else
                {
                    // Only perform the solve if there enough movement involved. Otherwise,
                    // we're wasting resources.
                    //float lTargetDistance = Vector3.Distance(lFoot.Transform.position, lFootTarget);

                    // TRT 02/04/2016 a - With minor movements in animation, we can see the solver popping on and off.
                    // So, we'll force the solver to run each frame no matter what. It's not that big of a hit.

                    //if (lTargetDistance > FootGround2BoneMotor.MIN_TARGET_DISTANCE)
                    {
                        //HingeSwingAndTwistJoint lLowerJoint = lLowerLeg.Joint as HingeSwingAndTwistJoint;

                        // Since we have a target, solve
                        IKSolverState lState = IKSolverState.Allocate();
                        lState.TargetPosition = lFootTarget;
                        lState.UseBindRotation = _UseBindRotation;
                        lState.UsePlaneNormal = _UsePlaneNormal;
                        lState.IsDebugEnabled = _IsDebugEnabled;

                        lState.Bones.Add(lUpperLeg);
                        lState.Bones.Add(lLowerLeg);

                        lState.BoneBendAxes.Add(_BoneInfo[0].BendAxis);
                        lState.BoneBendAxes.Add(_BoneInfo[1].BendAxis);

                        CosineSolver.SolveIK(ref lState);

                        // Process the results of the solve. We use the enumerator to
                        // avoid garbage from the ForEach
                        Dictionary<BoneControllerBone, Quaternion>.Enumerator lEnumerator = lState.Rotations.GetEnumerator();
                        while (lEnumerator.MoveNext())
                        {
                            BoneControllerBone lBone = lEnumerator.Current.Key;

                            FootPlacementMotorBone lBoneInfo = _BoneInfo[mBones.IndexOf(lBone)];

                            // The current rotation we will lerp from. We remove the trailing rotation offset because we'll add it later
                            Quaternion lCurrentRotation = lBone.Transform.rotation * lBone.ToBoneForward;

                            // Rotation as determined by the target
                            Quaternion lTargetRotation = lState.Rotations[lBone] * Quaternion.Euler(0f, 0f, lBoneInfo.Twist);

                            // Determine the final rotation based on weight
                            lBoneInfo.RotationTarget = Quaternion.Lerp(lCurrentRotation, lTargetRotation, _Weight * lBoneInfo.Weight);

                            // Slowly move towards the rotation we determined
                            lBoneInfo.Rotation = Quaternion.Lerp(lBoneInfo.Rotation, lBoneInfo.RotationTarget, (_IsFixedUpdateEnabled && !mIsFirstUpdate ? lBoneInfo.RotationLerp : 1f));

                            // Set the world rotation
                            lBone.SetWorldRotation(lBoneInfo.Rotation, _BoneWeight);
                            if (lBone.ApplyLimitsInFrame) { lBone.ApplyLimitsInFrame = _ApplyLimits; }
                        }

                        //// Process the results of the solve
                        //foreach (BoneControllerBone lBone in lState.Rotations.Keys)
                        //{
                        //    FootPlacementMotorBone lBoneInfo = _BoneInfo[mBones.IndexOf(lBone)];

                        //    // The current rotation we will lerp from. We remove the trailing rotation offset because we'll add it later
                        //    Quaternion lCurrentRotation = lBone.Transform.rotation * lBone.ToBoneForward;

                        //    // Rotation as determined by the target
                        //    Quaternion lTargetRotation = lState.Rotations[lBone] * Quaternion.Euler(0f, 0f, lBoneInfo.Twist);

                        //    // Determine the final rotation based on weight
                        //    lBoneInfo.RotationTarget = Quaternion.Lerp(lCurrentRotation, lTargetRotation, _Weight * lBoneInfo.Weight);

                        //    // Slowly move towards the rotation we determined
                        //    lBoneInfo.Rotation = Quaternion.Lerp(lBoneInfo.Rotation, lBoneInfo.RotationTarget, (_IsFixedUpdateEnabled && !mIsFirstUpdate ? lBoneInfo.RotationLerp : 1f));

                        //    // Set the world rotation
                        //    lBone.SetWorldRotation(lBoneInfo.Rotation, _BoneWeight);
                        //    if (lBone.ApplyLimitsInFrame) { lBone.ApplyLimitsInFrame = _ApplyLimits; }
                        //}

                        //DebugDraw.DrawLineOverlay(lUpperLeg.Transform.position, lUpperLeg.Transform.position + (lUpperLeg.Transform.rotation * lUpperLeg.ToBoneForward * (Vector3.forward * 0.5f)), 0.02f, Color.blue, 0.5f);
                        //DebugDraw.DrawLineOverlay(lUpperLeg.Transform.position, lUpperLeg.Transform.position + (lUpperLeg.Transform.rotation * lUpperLeg.ToBoneForward * (Vector3.up * 0.5f)), 0.02f, Color.green, 0.5f);
                        //DebugDraw.DrawLineOverlay(lUpperLeg.Transform.position, lUpperLeg.Transform.position + (lUpperLeg.Transform.rotation * lUpperLeg.ToBoneForward * (Vector3.right * 0.5f)), 0.02f, Color.red, 0.5f);

                        // Set the foot rotations. This may change based on the collisions
                        if (lFoot != null && _RotateFootToGround)
                        {
                            RotateFoot(lOwnerTransform, lLowerLeg, lFoot, lToes, lFootTarget, lGroundNormal, lHeelCollision, lToeCollision);
                        }

                        // Clean up
                        IKSolverState.Release(lState);
                    }
                }

                // Keep track of the last position so we can test for movement
                mLastPosition = mSkeleton.transform.position;
            }
            // If it's not on a consistant update, we just want to reset the
            // last rotations that we found.
            else
            {
                for (int i = 0; i < mBones.Count; i++)
                {
                    BoneControllerBone lBone = mBones[i];
                    if (lBone == null || lBone == lToes) { continue; }

                    if (lBone == lFoot)
                    {
                        Vector3 lMovement = mSkeleton.transform.position - mLastPosition;
                        if (!_RotateFootOnMovement && (Mathf.Abs(lMovement.x) > 0.001f || Mathf.Abs(lMovement.z) > 0.001f))
                        {
                            continue;
                        }
                    }

                    lBone.SetWorldRotation(_BoneInfo[i].Rotation, _BoneWeight);
                }
            }
        }

        /// <summary>
        /// Rotates the foot to match the ground. This function will use the
        /// collision values as best as it can
        /// </summary>
        /// <param name="rOwnerTransform"></param>
        /// <param name="rFoot"></param>
        /// <param name="rToes"></param>
        /// <param name="rFootTarget"></param>
        /// <param name="rGroundNormal"></param>
        /// <param name="rHeelCollision"></param>
        /// <param name="rToeCollision"></param>
        private void RotateFoot(Transform rOwnerTransform, BoneControllerBone rLowerLeg, BoneControllerBone rFoot, BoneControllerBone rToes, Vector3 rFootTarget, Vector3 rGroundNormal, bool rHeelCollision, bool rToeCollision)
        {
            BoneControllerBone lBone = rFoot;
            FootPlacementMotorBone lBoneInfo = _BoneInfo[mBones.IndexOf(lBone)];
            
            // The current rotation we will lerp from. We remove the trailing rotation offset because we'll add it later
            Quaternion lCurrentRotation = lBone.Transform.rotation * lBone.ToBoneForward;

            // Rotation based on the target position
            Quaternion lTargetRotation = lCurrentRotation;

            // Usually we don't want to rotate if we're moving. This way we don't cause floppy foot
            Vector3 lMovement = mSkeleton.transform.position - mLastPosition;
            if (_RotateFootOnMovement || (Mathf.Abs(lMovement.x) <= 0.001f && Mathf.Abs(lMovement.z) <= 0.001f))
            {
                // If we're meant to rotate the feet, do it
                float lGroundAngle = Vector3.Angle(rGroundNormal, mSkeleton.transform.up);
                if ((_RotateFootToGroundMinAngle == 0f || Mathf.Abs(lGroundAngle) > _RotateFootToGroundMinAngle) && Mathf.Abs(lGroundAngle) < 60f)
                {
                    if (rHeelCollision && rToeCollision)
                    {
                        Vector3 lGroundForward = Vector3.Cross(rGroundNormal, lCurrentRotation * -Vector3.right);

                        //float lDot = Vector3.Dot(mSkeleton.transform.forward, lGroundForward);
                        //if (lDot < 0f) { lGroundForward = -lGroundForward; }

                        lTargetRotation = Quaternion.LookRotation(lGroundForward, rGroundNormal) * _FootForwardToBind;
                    }
                    else if (rHeelCollision || rToeCollision)
                    {
                        if (rToes == null || !_RotateFootRequiresBoth)
                        {
                            if (rHeelCollision)
                            {
                                //Quaternion lHeelRotation = Quaternion.AngleAxis(90f, rFoot.WorldBindRotation * Vector3.right);
                                //Vector3 lToeForward = sCollisionInfo1.point + ((lHeelRotation * rGroundNormal).normalized * rFoot._Length);
                                //lTargetRotation = Quaternion.LookRotation(lToeForward - rFootTarget, rGroundNormal);

                                Vector3 lGroundForward = Vector3.Cross(rGroundNormal, lCurrentRotation * -Vector3.right);

                                //float lDot = Vector3.Dot(mSkeleton.transform.forward, lGroundForward);
                                //if (lDot < 0f) { lGroundForward = -lGroundForward; }

                                lTargetRotation = Quaternion.LookRotation(lGroundForward, rGroundNormal) * _FootForwardToBind;
                            }
                            else
                            {
                                //lTargetRotation = Quaternion.LookRotation(sCollisionInfo2.point - rFootTarget, rGroundNormal);

                                Vector3 lGroundForward = Vector3.Cross(rGroundNormal, lCurrentRotation * -Vector3.right);

                                //float lDot = Vector3.Dot(mSkeleton.transform.forward, lGroundForward);
                                //if (lDot < 0f) { lGroundForward = -lGroundForward; }

                                lTargetRotation = Quaternion.LookRotation(lGroundForward, rGroundNormal) * _FootForwardToBind;
                            }
                        }
                    }
                }
            }

            // Rotation as determined by the target
            lBoneInfo.RotationTarget = Quaternion.Lerp(lCurrentRotation, lTargetRotation, _Weight * lBoneInfo.Weight);

            // Slowly move towards the rotation we determined
            lBoneInfo.Rotation = Quaternion.Lerp(lBoneInfo.Rotation, lBoneInfo.RotationTarget, (!mIsFirstUpdate ? lBoneInfo.RotationLerp : 1f));

            // Set the world rotation
            lBone.SetWorldRotation(lBoneInfo.Rotation, _BoneWeight);
            if (lBone.ApplyLimitsInFrame) { lBone.ApplyLimitsInFrame = _ApplyLimits; }
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

            // Ground layers
            int lNewGroundingLayers = EditorHelper.LayerMaskField(new GUIContent("Ground Layers", "Layers that we'll use for collision tests"), GroundingLayers);
            if (lNewGroundingLayers != GroundingLayers)
            {
                lIsDirty = true;
                GroundingLayers = lNewGroundingLayers;
            }

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

            EditorGUILayout.BeginHorizontal();

            bool lNewAllowLegExtension = EditorGUILayout.Toggle(new GUIContent("Allow Extension", "Determines we'll have the leg reach for the target if the target is lower."), _AllowLegExtension);
            if (lNewAllowLegExtension != _AllowLegExtension)
            {
                lIsDirty = true;
                _AllowLegExtension = lNewAllowLegExtension;
            }

            GUILayout.Space(20);
            EditorGUILayout.LabelField(new GUIContent("Max Delta Distance", "Max delta between animation's leg-to-foot distance and let-to-foot-target distance before we disable IK."), GUILayout.Width(30));
            float lNewMaxDeltaDistance = Mathf.Abs(EditorGUILayout.FloatField(MaxDeltaDistance, GUILayout.Width(65)));
            if (lNewMaxDeltaDistance != MaxDeltaDistance)
            {
                lIsDirty = true;
                MaxDeltaDistance = lNewMaxDeltaDistance;
            }

            GUILayout.FlexibleSpace();

            EditorGUILayout.EndHorizontal();

            float lNewRaycastExtensionDistance = Mathf.Abs(EditorGUILayout.FloatField(new GUIContent("Ray Extension Dist", "Distance below the toe bone to end the raycast."), _RaycastExtensionDistance));
            if (lNewRaycastExtensionDistance != _RaycastExtensionDistance)
            {
                lIsDirty = true;
                _RaycastExtensionDistance = lNewRaycastExtensionDistance;
            }

            GUILayout.Space(5);

            EditorGUILayout.BeginHorizontal();

            bool lNewRotateFootToGround = EditorGUILayout.Toggle(new GUIContent("Rotate Foot to Ground", "Determines if we rotate the foot to settle on the ground's normal."), _RotateFootToGround);
            if (lNewRotateFootToGround != _RotateFootToGround)
            {
                lIsDirty = true;
                _RotateFootToGround = lNewRotateFootToGround;
            }

            GUILayout.Space(20);
            EditorGUILayout.LabelField(new GUIContent("Min Angle", "Minimum ground angle for the foot to rotate."), GUILayout.Width(30));
            float lNewRotateFootToGroundMinAngle = EditorGUILayout.FloatField(_RotateFootToGroundMinAngle, GUILayout.Width(65));
            if (lNewRotateFootToGroundMinAngle != _RotateFootToGroundMinAngle)
            {
                lIsDirty = true;
                _RotateFootToGroundMinAngle = lNewRotateFootToGroundMinAngle;
            }

            GUILayout.FlexibleSpace();

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();

            bool lNewRotateFootOnMovement = EditorGUILayout.Toggle(new GUIContent("Rotate Foot On Movement", "Determines if we rotate while the character is moving."), _RotateFootOnMovement);
            if (lNewRotateFootOnMovement != _RotateFootOnMovement)
            {
                lIsDirty = true;
                _RotateFootOnMovement = lNewRotateFootOnMovement;
            }

            // Determines if we'll run in the scene editor
            GUILayout.Space(20);
            EditorGUILayout.LabelField(new GUIContent("Req Both", "Determines if rotating the foot requires both the heel and toe be on the ground."), GUILayout.Width(80));
            bool lNewRotateFootRequiresBoth = EditorGUILayout.Toggle(_RotateFootRequiresBoth, GUILayout.Width(16));
            if (lNewRotateFootRequiresBoth != _RotateFootRequiresBoth)
            {
                lIsDirty = true;
                _RotateFootRequiresBoth = lNewRotateFootRequiresBoth;
            }

            GUILayout.FlexibleSpace();

            EditorGUILayout.EndHorizontal();

            GUILayout.Space(5);

            float lNewFootToeDistance = Mathf.Abs(EditorGUILayout.FloatField(new GUIContent("Foot Toe Distance", "Vertical distance between the foot bone and toe bone. Set to 0 to enable auto detect."), _FootToeDistance));
            if (lNewFootToeDistance != _FootToeDistance)
            {
                lIsDirty = true;
                _FootToeDistance = lNewFootToeDistance;
            }

            float lNewToeSoleDistance = Mathf.Abs(EditorGUILayout.FloatField(new GUIContent("Toe Sole Distance", "Vertical distance between the bottom of the sole and toe bone."), _ToeSoleDistance));
            if (lNewToeSoleDistance != _ToeSoleDistance)
            {
                lIsDirty = true;
                _ToeSoleDistance = lNewToeSoleDistance;
            }

            float lNewRaycastStartDistance = Mathf.Abs(EditorGUILayout.FloatField(new GUIContent("Start Distance", "Distance above the foot bone to start the raycast."), _RaycastStartDistance));
            if (lNewRaycastStartDistance != _RaycastStartDistance)
            {
                lIsDirty = true;
                _RaycastStartDistance = lNewRaycastStartDistance;
            }

            Vector3 lNewFootForwardToBind = EditorGUILayout.Vector3Field(new GUIContent("Ground to Foot Rotation", "Euler angles that gets us from the horizontal ground to the foot's bind pose"), _FootForwardToBind.eulerAngles);
            if (lNewFootForwardToBind != _FootForwardToBind.eulerAngles)
            {
                lIsDirty = true;
                _FootForwardToBind = Quaternion.Euler(lNewFootForwardToBind);
            }

            GUILayout.Space(5);

            EditorGUILayout.LabelField("Bone List:");

            EditorGUILayout.BeginVertical(GUI.skin.box);

            EditorGUILayout.LabelField("Auto-Generate Humanoid");

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button(new GUIContent("Left Leg", "Auto setup for a left humanoid leg."), EditorStyles.miniButton))
            {
                if (EditorUtility.DisplayDialog("Bone Controller", "Update settings?", "Yes", "No"))
                {
                    AutoLoadBones("Humanoid Left");
                }
            }

            if (GUILayout.Button(new GUIContent("Right Leg", "Auto setup for a right humanoid leg."), EditorStyles.miniButton))
            {
                if (EditorUtility.DisplayDialog("Bone Controller", "Update settings?", "Yes", "No"))
                {
                    AutoLoadBones("Humanoid Right");
                }
            }

            EditorGUILayout.EndHorizontal();

            GUILayout.Space(3);

            EditorGUILayout.EndVertical();

            // Force the selected bone based on the input list
            bool lIsListDirty = RenderBoneList(mBones, rSelectedBones, 4);
            if (lIsListDirty) 
            { 
                lIsDirty = true;

                if (mBones.Count >= 3 && mBones[2] != null)
                {
                    Vector3 lBindGroundForward = Vector3.Cross(mSkeleton.transform.up, mBones[2].WorldBindRotation * -Vector3.right);
                    Quaternion lBindForwardRotation = Quaternion.LookRotation(lBindGroundForward, mSkeleton.transform.up);
                    _FootForwardToBind = Quaternion.Inverse(lBindForwardRotation) * mBones[2].WorldBindRotation;
                }

                // Ensure we have valid values for the toes
                if (mBones.Count >= 4 && mBones[3] != null)
                {
                    _FootToeDistance = mBones[3]._Transform.position.y - mBones[2]._Transform.position.y;
                }
                else
                {
                    _FootToeDistance = 0f;
                }
            }

#endif

            return lIsDirty;
        }

        /// <summary>
        /// Render scene objects that are used to control or debug the motor
        /// </summary>
        /// <param name="rSelectedBones"></param>
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
                FootPlacementMotorBone lBoneInfo = new FootPlacementMotorBone();
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

            while (_BoneInfo.Count < mBones.Count)
            {
                FootPlacementMotorBone lBoneInfo = new FootPlacementMotorBone();
                _BoneInfo.Insert(mBones.IndexOf(rBone), lBoneInfo);
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
        public class FootPlacementMotorBone
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
            public float RotationLerp = 0.7f;

            /// <summary>
            /// Determines how much the motor overrides the natural rotation
            /// </summary>
            public float Weight = 1f;

            /// <summary>
            /// Constructor
            /// </summary>
            public FootPlacementMotorBone()
            {
            }
        }
    }
}
