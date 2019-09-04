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
    /// Based on the Fast and Easy Reach Cone Joint Limits paper:
    /// http://users.soe.ucsc.edu/~avg/Papers/jtl.pdf
    /// 
    /// This joint type allows the joint to "swing" along the
    /// up and right directions of the bone as well as "twist"
    /// along the forward direction. 
    /// 
    /// NOTE: Remember most bones have "up" vector being the "forward direction"
    /// 
    /// Using boundary points, we can control how far the swing will actually go.
    /// 
    /// Then, we can use simple angular limits to control how
    /// much the bone actually twists.
    /// 
    /// </summary>
    [Serializable]
    [IKBoneJointNameAttribute("Limited Swing and Twist")]
    public class LimitedSwingAndTwistJoint : BoneControllerJoint
    {
        /// <summary>
        /// Determines if we use the boundary points to limit the swing
        /// </summary>
        public bool _LimitSwing = true;
        public bool LimitSwing
        {
            get { return _LimitSwing; }
            set { _LimitSwing = value; }
        }

        /// <summary>
        /// Public facing points that are used to create the
        /// true swing limits of the bone. These points are normalized
        /// values that define the boundary relative to the bind pose.
        /// 
        /// points[0].point = new Vector3(0, 0, 1);     // down (z)
        /// points[1].point = new Vector3(1, 0, 0);     // back (x)
        /// points[2].point = new Vector3(0, 0, -1);    // up (-z)
        /// points[3].point = new Vector3(-1, 0, 0);    // forward (-x)
        /// </summary>
        public List<Vector3> BoundaryPoints = null;

        /// <summary>
        /// Once we process the BoundaryPoints, we have reach points
        /// that will be used to create the reach cones.
        /// </summary>
        private Vector3[] mReachPoints = null;

        /// <summary>
        /// Reach cones have edges on the "great-circle arcs" of the sphere
        /// that define the bone's limit for swing
        /// </summary>
        private ReachCone[] mReachCones = new ReachCone[0];

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
        /// Determines how close the bone is from reaching its twist limit. Values
        /// are from -1 (at min limit) to 0 (at bind angle) to 1 (at max limit).
        /// </summary>
        public override float GetTwistStress(Quaternion rLocalTwist)
        {
            if (!_AllowTwist) { return 0f; }
            if (!_LimitTwist) { return 0f; }

            // Grab the current twist angle
            float lTwistAngle = Vector3Ext.SignedAngle(Vector3.up, rLocalTwist * Vector3.up, Vector3.forward);

            // Based on the angle (and limits), determine the stress
            if (lTwistAngle == 0f)
            {
                return 0f;
            }
            else if (lTwistAngle < 0f)
            {
                if (lTwistAngle <= _MinTwistAngle) { return 1f; }
                return 1f - ((_MinTwistAngle - lTwistAngle) / _MinTwistAngle);
            }
            else
            {
                if (lTwistAngle >= _MaxTwistAngle) { return 1f; }
                return 1f - ((_MaxTwistAngle - lTwistAngle) / _MaxTwistAngle);
            }
        }

        /// <summary>
        /// Smoothing iterations for the boundary points
        /// </summary>
        public int _SmoothingIterations = 1;
        public int SmoothingIterations
        {
            get { return _SmoothingIterations; }
            set { _SmoothingIterations = Mathf.Clamp(value, 0, 3); }
        }

        /// <summary>
        /// Tracks the last radial slice the bone was oriented to. This is the
        /// place we'll start testing on the next iteration.
        /// </summary>
        //private int mLastReachSliceIndex = 0;

        /// <summary>
        /// Default constructor
        /// </summary>
        public LimitedSwingAndTwistJoint()
            : base()
        {
        }

        /// <summary>
        /// Bone constructor
        /// </summary>
        /// <param name="rBone">Bone the joint is tied to</param>
        public LimitedSwingAndTwistJoint(BoneControllerBone rBone)
            : base(rBone)
        {
        }

        /// <summary>
        /// Associates the bone and allows for any setup
        /// </summary>
        /// <param name="rBone">Bone the joint is tied to</param>
        public override void Initialize(BoneControllerBone rBone)
        {
            base.Initialize(rBone);

            //Log.ConsoleWrite("SwingPointAndTwistJoint.Initialize");

            // Use the default boundary points if we need to
            if (BoundaryPoints == null || BoundaryPoints.Count == 0)
            {
                ClearBoundaryPoints();
            }
            // Create the reach points with the existig boundary points
            else
            {
                BuildReachCones();
            }
        }

        /// <summary>
        /// Apply any rotational limits to the local rotation so it
        /// meets the constraints of this bone type
        /// </summary>
        /// <param name="rBone">Bone being processed</param>
        /// <param name="rRotation">Target local rotation of the bone to be modified</param>
        public override bool ApplyLimits(ref Quaternion rSwing, ref Quaternion rTwist)
        {
            //Vector3 lBoneForward = Vector3.forward; // mBone.BoneForward;

            // First thing we need to do is build our reach points based on the angles
            if (BoundaryPoints == null || BoundaryPoints.Count == 0) { ClearBoundaryPoints(); }

            // Track whether we're in bounds
            bool lIsInLimits = (!_LimitSwing || rSwing.IsIdentity()) && (!_LimitTwist || rTwist.IsIdentity());

            // Test if we should limit the swing
            if (_LimitSwing && !rSwing.IsIdentity())
            {
                // Create a longitudinal axis based on the initial bone
                // direction and the current rotation. This is the "local" direction
                // of the bone relative to the bind position.
                Vector3 lTargetBoneForward = rSwing * Vector3.forward;

                // Determine which reach cone our bone axis is in. We shouldn't
                // get a value less than 0. If so, the reach cones could be bad. 
                // For now, we'll simply move on.
                int lCurrentSliceIndex = GetReachConeIndex(lTargetBoneForward);
                if (lCurrentSliceIndex >= 0)
                {
                    // Test if we're actually valid inside the reach cone. If so,
                    // we can move on.
                    float lDot = Vector3.Dot(mReachCones[lCurrentSliceIndex].BoundaryPlane, lTargetBoneForward);
                    if (lDot >= 0)
                    {
                        lIsInLimits = true;
                        //mLastReachSliceIndex = lCurrentSliceIndex;

                        // If we are preserving the the automatic twisting, we can remove it
                        // by grabing the simple rotation from the start position to the new position
                        if (_PreventSwingTwisting) { rSwing = Quaternion.FromToRotation(Vector3.forward, lTargetBoneForward); }
                    }
                    // If we're not in the reach cone, we need to pull the swing
                    // back so that we are.
                    else
                    {
                        Quaternion lTwist = Quaternion.identity;
                        Quaternion lSwing = Quaternion.identity;

                        // We may need to store the twisting associated with the swing. So grab it first
                        if (!_PreventSwingTwisting) { rSwing.DecomposeSwingTwist(Vector3.forward, ref lSwing, ref lTwist); }

                        // Find the max swing rotation
                        Vector3 lCurrentBoneForward = Vector3.forward;

                        // Determine the closest intersection along the direction
                        // of old-L to new-L. This becomes the rotation we care about
                        Vector3 lNewTargetBoneForward = GetReachConeExit(lCurrentBoneForward, lTargetBoneForward);
                        rSwing = Quaternion.FromToRotation(lCurrentBoneForward, lNewTargetBoneForward);

                        // If we are aren't preventing the twist, put the twist back
                        if (!_PreventSwingTwisting) { rSwing = rSwing * lTwist; }
                    }
                }
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

            return lIsInLimits;
        }

        /// <summary>
        /// Clear the current boundary points and set them to 90-degrees on the
        /// local right and up directions.
        /// </summary>
        public void ClearBoundaryPoints()
        {
            if (BoundaryPoints == null) { BoundaryPoints = new List<Vector3>(); }

            BoundaryPoints.Clear();
            BoundaryPoints.Add(new Vector3(1f, 0f, 0f));
            BoundaryPoints.Add(new Vector3(0f, 1f, 0f));
            BoundaryPoints.Add(new Vector3(-1f, 0f, 0f));
            BoundaryPoints.Add(new Vector3(0f, -1f, 0f));            

            BuildReachCones();
        }

        /// <summary>
        /// Rebuild the reach codes when the limits change
        /// </summary>
        public void BuildReachCones()
        {
            _SmoothingIterations = Mathf.Clamp(_SmoothingIterations, 0, 3);

            // Use the original boundery points as a base and them smooth
            // by doubling the points (each iteration)
            mReachPoints = new Vector3[BoundaryPoints.Count];
            for (int i = 0; i < BoundaryPoints.Count; i++)
            {
                mReachPoints[i] = BoundaryPoints[i].normalized;
            }

            // Smooth for each iteration. The increase is quadradic,
            // so be careful about doing this too foten.
            for (int i = 0; i < _SmoothingIterations; i++)
            {
                mReachPoints = SmoothReachPoints();
            }

            // Use the newly smoothed points to create the condes
            mReachCones = new ReachCone[mReachPoints.Length];
            for (int i = 0; i < mReachCones.Length - 1; i++)
            {
                //mReachCones[i] = new ReachCone(Vector3.zero, mBone.BoneForward.normalized, mReachPoints[i], mReachPoints[i + 1]);
                mReachCones[i] = new ReachCone(Vector3.zero, Vector3.forward, mReachPoints[i], mReachPoints[i + 1]);
            }

            // This last cone closes the whole system
            //mReachCones[mReachPoints.Length - 1] = new ReachCone(Vector3.zero, mBone.BoneForward.normalized, mReachPoints[mReachPoints.Length - 1], mReachPoints[0]);
            mReachCones[mReachPoints.Length - 1] = new ReachCone(Vector3.zero, Vector3.forward, mReachPoints[mReachPoints.Length - 1], mReachPoints[0]);

            // Finally, pre-process the cones since some calculations only need to be done once
            for (int i = 0; i < mReachCones.Length; i++)
            {
                mReachCones[i].PreProcess();
            }
        }

        /// <summary>
        /// Smooth the reach points based on the "Smoothing the Reach Cone" section.
        /// The goal is to double the reach points (and reach cones) in order to smooth
        /// the limits without users having to enter tons of points.
        /// </summary>
        /// <returns></returns>
        private Vector3[] SmoothReachPoints()
        {
            int lReachPointLength = mReachPoints.Length;

            // Interpolation constants from (10)
            float lSmoothingConstant = GetSmoothingConstant(lReachPointLength);

            // We're subdividing the points, so double our array
            Vector3[] lNewReachPoints = new Vector3[lReachPointLength * 2];

            // Map existing points onto the tangent plane.
            for (int i = 0; i < lNewReachPoints.Length; i += 2)
            {
                //lNewReachPoints[i] = SpherePointToTangentPlane(mReachPoints[i / 2], mBone.BoneForward, 1);
                lNewReachPoints[i] = SpherePointToTangentPlane(mReachPoints[i / 2], Vector3.forward, 1);
            }

            // Interpolate the points. This folows formula (9) on page 8
            for (int i = 0; i < lNewReachPoints.Length; i += 2)
            {
                int iPlusHalf = i + 1;
                int iPlus1 = (i + 2) % lNewReachPoints.Length;
                int iPlus2 = (i + 4) % lNewReachPoints.Length;

                int iMinus1 = (i - 2) % lNewReachPoints.Length;
                if (iMinus1 < 0) { iMinus1 = lNewReachPoints.Length + iMinus1; }

                lNewReachPoints[iPlusHalf] = (0.5f * (lNewReachPoints[i] + lNewReachPoints[iPlus1])) + (lSmoothingConstant * (lNewReachPoints[iPlus1] - lNewReachPoints[iMinus1])) + (lSmoothingConstant * (lNewReachPoints[i] - lNewReachPoints[iPlus2]));
            }

            // Project back onto the sphere
            for (int i = 0; i < lNewReachPoints.Length; i++)
            {
                //lNewReachPoints[i] = TangentPointToSpherePoint(lNewReachPoints[i], mBone.BoneForward, 1);
                lNewReachPoints[i] = TangentPointToSpherePoint(lNewReachPoints[i], Vector3.forward, 1);
            }

            // Return the new set of points
            return lNewReachPoints;
        }

        /// <summary>
        /// Page 8
        /// Constant used to put a never vertex between ponts while smoothing.
        /// </summary>
        /// <param name="rBoundaryPointCount">Number of boundary points</param>
        /// <returns></returns>
        private float GetSmoothingConstant(int rBoundaryPointCount)
        {
            if (rBoundaryPointCount <= 3) return .1667f;
            if (rBoundaryPointCount == 4) return .1036f;
            if (rBoundaryPointCount == 5) return .0850f;
            if (rBoundaryPointCount == 6) return .0773f;
            if (rBoundaryPointCount == 7) return .0700f;
            return .0625f;
        }

        /// <summary>
        /// Page 7
        /// Maps the reach point from the spherical position to the tangent plane.
        /// </summary>
        /// <param name="rReachPoint">Point we're mapping</param>
        /// <param name="rVisiblePoint">Visible point on the sphere</param>
        /// <param name="rRadius">Radius of the sphere (should always be 1)</param>
        /// <returns>Position of the point on the tangent plane</returns>
        private Vector3 SpherePointToTangentPlane(Vector3 rReachPoint, Vector3 rVisiblePoint, float rRadius)
        {
            float lRadiusSqr = rRadius * rRadius;

            // From page 7, equations (7)
            float lDot = Vector3.Dot(rVisiblePoint, rReachPoint);
            float lU = (2 * lRadiusSqr) / (lRadiusSqr + lDot);
            return (lU * rReachPoint) + ((1 - lU) * -rVisiblePoint);
        }

        /// <summary>
        /// Page 8
        /// Maps a point on the tangent plane back to a spherical position
        /// </summary>
        /// <param name="rTangentPoint">Point we're mapping</param>
        /// <param name="rVisiblePoint">Visible point ont he sphere</param>
        /// <param name="rRadius">Radius of the sphere (should always be 1)</param>
        /// <returns>Position of the point on the sphere</returns>
        private Vector3 TangentPointToSpherePoint(Vector3 rTangentPoint, Vector3 rVisiblePoint, float rRadius)
        {
            float lRadiusSqr = rRadius * rRadius;

            // From page 8, equations (8)
            float lDot = Vector3.Dot(rTangentPoint - rVisiblePoint, rTangentPoint - rVisiblePoint);
            float lU = (4 * lRadiusSqr) / ((4 * lRadiusSqr) + lDot);
            return (lU * rTangentPoint) + ((1 - lU) * -rVisiblePoint);
        }

        /// <summary>
        /// Page 4:
        /// InReachCone(L)
        /// Determine the reach cone that this "local bone direction" crosses. There
        /// can be only one.
        /// </summary>
        /// <param name="rBoneAxis"></param>
        /// <returns></returns>
        private int GetReachConeIndex(Vector3 rBoneAxis)
        {
            float lDotPlus1 = Vector3.Dot(mReachCones[0].SlicePlane, rBoneAxis);

            // Search through all the cones. Remember we'll need to loop around 
            // to the first cone to complete the full test of all readial slices.
            for (int i = 0; i < mReachCones.Length; i++)
            {
                float lDot = lDotPlus1;

                int iPlus1 = (i + 1) % mReachCones.Length;
                lDotPlus1 = Vector3.Dot(mReachCones[iPlus1].SlicePlane, rBoneAxis);

                // Check if we're in the radial slice
                if (lDot >= 0 && lDotPlus1 < 0)
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// Determines if the axis is withing the reach cone
        /// </summary>
        /// <param name="rBoneAxis"></param>
        /// <returns></returns>
        private bool IsInReachCone(Vector3 rBoneAxis)
        {
            int lCurrentSliceIndex = GetReachConeIndex(rBoneAxis);
            if (lCurrentSliceIndex >= 0)
            {
                // Test if we're actually valid inside the reach cone. If so, we can move on.
                float lDot = Vector3.Dot(mReachCones[lCurrentSliceIndex].BoundaryPlane, rBoneAxis);
                if (lDot >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Alternate method for determining the reach cone exit point. This takes
        /// a little longer, but it can handle large angular differences and doesn't have
        /// as many quirks.
        /// </summary>
        private Vector3 GetReachConeExit(Vector3 rStart, Vector3 rEnd)
        {
            // Jump out early if the end is in the cone
            bool lIsEndInReachCone = IsInReachCone(rEnd);
            if (lIsEndInReachCone) { return rEnd; }

            // Grab stats and determine the half point
            Vector3 lDirection = rEnd - rStart;
            float lDistance = lDirection.magnitude * 0.5f;

            lDirection.Normalize();
            Vector3 lHalf = rStart + (lDirection * lDistance);

            int lLoop = 0;
            while (lLoop < 10)
            {
                lLoop++;

                // If the half point is in the cone, move the start
                // forward. This shrinks the distance to the end
                if (IsInReachCone(lHalf))
                {
                    rStart = lHalf;
                }

                // Determine the new half point
                lDistance = lDistance * 0.5f;
                if (lDistance < 0.0005f) { break; }

                lHalf = rStart + (lDirection * lDistance);
            }

            return lHalf;
        }

        /// <summary>
        /// Page 5:
        /// Given the current swing and target swing, check for the last point
        /// within the reach cone we can legally get to
        /// </summary>
        private Vector3 GetReachConeExit(Vector3 rStart, Vector3 rEnd, int rStartReachSliceIndex)
        {
            if (rStartReachSliceIndex < 0) { rStartReachSliceIndex = 0; }

            // If we have multiple candidates, we need to find the one with the one with the smallest t
            //float lOrientation = Vector3.Dot(Vector3.Cross(rStart, rEnd), mBone.BoneForward);
            float lOrientation = Vector3.Dot(Vector3.Cross(rStart, rEnd), Vector3.forward);
            int lDirection = (lOrientation >= 0 ? 1 : -1);

            for (int i = 0; i < mReachCones.Length; i++)
            {
                // Determine the unconstrained index based on the direction we flow
                int lIndex = rStartReachSliceIndex + (lDirection * i);
                if (lIndex < 0) { lIndex += mReachCones.Length; }

                // Ensure we're staying in our array limits even if we loop
                int lArrayIndex = lIndex % mReachCones.Length;

                // Test for when we have an exit point
                float lDistance = Vector3.Dot(-rStart, mReachCones[lArrayIndex].BoundaryPlane) / Vector3.Dot(rEnd - rStart, mReachCones[lArrayIndex].BoundaryPlane);

                // Determine if an intersection occured
                if (lDistance > 0f && lDistance < 1f) { return rStart + (lDistance * (rEnd - rStart)); }
            }

            return rStart;
        }

        // ************************************** EDITOR SUPPORT **************************************

        private bool mIsEditing = false;

        private int mSelectedPoint = 0;

#if UNITY_EDITOR

        private Quaternion mLastTwist = Quaternion.identity;

        private Quaternion mLastSwing = Quaternion.identity;

#endif

        /// <summary>
        /// Raised when the bone is selected in the editor
        /// </summary>
        public override void OnEnable()
        {
            mIsEditing = false;
            mSelectedPoint = -1;
        }

        /// <summary>
        /// Raised when the bone is deselected in the editor
        /// </summary>
        public override void OnDisable()
        {
            mIsEditing = false;
            mSelectedPoint = -1;
        }

        /// <summary>
        /// Allow the constraint to render it's own GUI
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
                mSelectedPoint = -1;
                _PreventSwingTwisting = lNewPreventSwingTwisting;
            }

            bool lNewLimitSwing = EditorGUILayout.Toggle(new GUIContent("Limit Swing", "Determines if we use the limits set below."), _LimitSwing);
            if (lNewLimitSwing != _LimitSwing)
            {
                lIsDirty = true;
                mSelectedPoint = -1;
                _LimitSwing = lNewLimitSwing;
            }

            if (_LimitSwing)
            {
                GUILayout.BeginVertical("Swing Limits", GUI.skin.window);

                EditorGUILayout.HelpBox("Boundary Point coordinates are relative to the bone's transform and are normalized.", MessageType.Info);

                for (int i = 0; i < BoundaryPoints.Count; i++)
                {
                    bool lIsSelected = mSelectedPoint == i;
                    GUIStyle lRowStyle = (lIsSelected ? BoneControllerJoint.SelectedRowStyle : BoneControllerJoint.RowStyle);
                    EditorGUILayout.BeginHorizontal(lRowStyle);

                    // Allow for the point to be selected
                    if (GUILayout.Button(new GUIContent(BoneControllerJoint.ItemSelector), GUI.skin.label, GUILayout.Width(16)))
                    {
                        mIsEditing = true;
                        mSelectedPoint = i;

                        SceneView.RepaintAll();
                    }

                    // If a value change happens, record it and select the row
                    Vector3 lNewPosition = EditorGUILayout.Vector3Field("Point " + (i + 1).ToString(), BoundaryPoints[i]);
                    if (lNewPosition != BoundaryPoints[i])
                    {
                        lIsDirty = true;

                        mIsEditing = true;
                        mSelectedPoint = i;

                        BoundaryPoints[i] = lNewPosition.normalized;
                    }

                    EditorGUILayout.EndHorizontal();
                }

                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();

                if (GUILayout.Button(new GUIContent("clear", "Clear all the boundary points and reset them."), EditorStyles.miniButton, GUILayout.Width(50)))
                {
                    lIsDirty = true;

                    mIsEditing = false;
                    mSelectedPoint = -1;

                    ClearBoundaryPoints();
                }

                if (GUILayout.Button(new GUIContent("+", "Adds a new boundary point to the swing limit."), EditorStyles.miniButtonLeft, GUILayout.Width(20)))
                {
                    lIsDirty = true;

                    mIsEditing = true;
                    mSelectedPoint = AddBoundaryPoint(mSelectedPoint);
                }

                if (GUILayout.Button(new GUIContent("-", "Removes the selected boundary point from the swing limit."), EditorStyles.miniButtonRight, GUILayout.Width(20)))
                {
                    lIsDirty = true;

                    mIsEditing = true;
                    mSelectedPoint = RemoveBoundaryPoint(mSelectedPoint);
                }

                GUILayout.EndHorizontal();

                GUILayout.Space(10);

                int lNewSmoothingIternations = EditorGUILayout.IntField(new GUIContent("Smooth Iterations", "Number of times (0 to 3) to smooth the boundary points."), _SmoothingIterations);
                if (lNewSmoothingIternations != _SmoothingIterations)
                {
                    lIsDirty = true;
                    mSelectedPoint = -1;

                    _SmoothingIterations = lNewSmoothingIternations;
                    BuildReachCones();
                }

                GUILayout.EndVertical();

                GUILayout.Space(10);
            }

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
                        mSelectedPoint = -1;
                        _MinTwistAngle = lNewMinTwist;
                    }

                    float lNewMaxTwist = Mathf.Clamp(EditorGUILayout.FloatField(new GUIContent("Max Twist Angle", "Maximum angle (0 to 180) that the twist can make."), _MaxTwistAngle), 0, 180);
                    if (lNewMaxTwist != _MaxTwistAngle)
                    {
                        lIsDirty = true;
                        mSelectedPoint = -1;
                        _MaxTwistAngle = lNewMaxTwist;
                    }

                    GUILayout.EndVertical();
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

            float lPositionScale = 0.08f;
            if (Bone.Skeleton.EditorAutoScaleHandles) { lPositionScale = HandleUtility.GetHandleSize(Bone.Transform.position) * HandlesHelper.HandleScale * 0.9f; }

            Color lLineColor = new Color(0.94118f, 0.39608f, 0.13333f, 1f);
            Color lFillColor = new Color(0.94118f, 0.39608f, 0.13333f, 0.2f);
            Color lHandleColor = Handles.color;

            Quaternion lWorldBind = Bone.WorldBindRotation;

            // Render out the swing limits
            if (_LimitSwing)
            {
                Handles.color = Color.white;

                // If we have an invalid reach cone, report it
                for (int i = 0; i < mReachCones.Length; i++)
                {
                    if (!mReachCones[i].IsVisiblePointValid)
                    {
                        lFillColor = new Color(1f, 0f, 0f, 1.0f);
                        lLineColor = new Color(1f, 0f, 0f, 1.0f);
                    }
                }

                for (int i = 0; i < mReachPoints.Length; i++)
                {
                    int iPlus1 = (i + 1) % mReachPoints.Length;

                    Vector3[] lVertices = new Vector3[4];
                    lVertices[0] = Bone.Transform.position;
                    lVertices[1] = Bone.Transform.position;
                    lVertices[2] = Bone.Transform.position + (lWorldBind * mReachPoints[i] * lPositionScale);
                    lVertices[3] = Bone.Transform.position + (lWorldBind * mReachPoints[iPlus1] * lPositionScale);

                    Handles.DrawSolidRectangleWithOutline(lVertices, lFillColor, lLineColor);

                    //Log.ConsoleWrite("Color: " + lFillColor.ToString());
                }
            }

            // Render out the twist limit
            if (_LimitTwist)
            {
                bool lIsLimitsDirty = HandlesHelper.JointTwistLimitsHandle(mBone, ref _MinTwistAngle, ref _MaxTwistAngle);
                if (lIsLimitsDirty)
                {
                    lIsDirty = true;
                }
            }

            // Only show the boundary points if we're in editor mode
            if (mIsEditing)
            {
                float lHandleScale = HandleUtility.GetHandleSize(Bone.Transform.position) * HandlesHelper.HandleScale * 0.1f;

                // Render out the code that is limiting the swing
                for (int i = 0; i < BoundaryPoints.Count; i++)
                {
                    GUI.color = new Color(0.72549f, 0.30588f, 0.10588f, 1f);
                    Handles.Label(Bone.Transform.position + (lWorldBind * (BoundaryPoints[i] * (lPositionScale + 0.01f))), (i + 1).ToString());

                    Vector3 lWorldPosition = Bone.Transform.position + (lWorldBind * (BoundaryPoints[i] * lPositionScale));

                    Handles.color = (i == mSelectedPoint ? Color.yellow : new Color(0.94118f, 0.39608f, 0.13333f, 1f));
#if UNITY_4 || UNITY_5_0 || UNITY_5_1 || UNITY_5_2 || UNITY_5_3 || UNITY_5_4 || UNITY_5_5
                    if (Handles.Button(lWorldPosition, Quaternion.identity, lHandleScale, lHandleScale, Handles.SphereCap))
#else
                    if (Handles.Button(lWorldPosition, Quaternion.identity, lHandleScale, lHandleScale, Handles.SphereHandleCap))
#endif
                    {
                        lIsDirty = true;
                        mSelectedPoint = i;
                    }

                    if (i == mSelectedPoint)
                    {
                        Vector3 lNewPosition = Handles.PositionHandle(lWorldPosition, Quaternion.identity);
                        if (lNewPosition != lWorldPosition)
                        {
                            lIsDirty = true;

                            lNewPosition = (lWorldBind.Conjugate() * (lNewPosition - Bone.Transform.position)) / lPositionScale;
                            BoundaryPoints[i] = lNewPosition.normalized;

                            BuildReachCones();
                        }
                    }
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
                //float lTwist = mBone.Twist.eulerAngles.y;
                float lTwist = Vector3Ext.SignedAngle(Vector3.up, rModifier.Twist * Vector3.up, Vector3.forward);
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
                    rModifier.Twist = (_AllowTwist ? Quaternion.identity : mBone._Twist);
                    rModifier.IsDirty = true;

                    mBone._Transform.localRotation = mBone._BindRotation;
                }

                if (rModifier.IsDirty)
                {
                    // Before we go to far, see if we are within the joint limits. If not,
                    // we need to go back to a good position.
                    bool lIsInLimits = ApplyLimits(ref rModifier.Swing, ref rModifier.Twist);
                    if (lIsInLimits || QuaternionExt.IsEqual(rModifier.Swing, Quaternion.identity))
                    {
                        mLastSwing = rModifier.Swing;
                        mLastTwist = rModifier.Twist;
                    }
                    else
                    {
                        rModifier.Swing = mLastSwing;
                        rModifier.Twist = mLastTwist;
                    }
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

            bool lIsTwistDirty = HandlesHelper.JointTwistHandle(mBone, rModifier);
            if (lIsTwistDirty)
            {
                //rModifier.Twist = lTwist;
                rModifier.IsDirty = true;
            }

            if (rModifier.IsDirty)
            {
                // Before we go to far, see if we are within the joint limits. If not,
                // we need to go back to a good position.
                bool lIsInLimits = ApplyLimits(ref rModifier.Swing, ref rModifier.Twist);
                if (lIsInLimits || QuaternionExt.IsEqual(rModifier.Swing, Quaternion.identity))
                {
                    mLastSwing = rModifier.Swing;
                    mLastTwist = rModifier.Twist;
                }
                else
                {
                    rModifier.Swing = mLastSwing;
                    rModifier.Twist = mLastTwist;
                }
            }

#endif

            return rModifier.IsDirty;
        }

        /// <summary>
        /// Adds a boundary point between the selected point and the next point
        /// </summary>
        /// <param name="rSelectedPoint"></param>
        /// <returns></returns>
        private int AddBoundaryPoint(int rSelectedPoint)
        {
            if (rSelectedPoint < 0 || mSelectedPoint > BoundaryPoints.Count) { rSelectedPoint = 0; }
            int lNextSelectedPoint = (rSelectedPoint + 1) % BoundaryPoints.Count;

            Vector3 lPoint = (BoundaryPoints[rSelectedPoint] + BoundaryPoints[lNextSelectedPoint]) / 2f;
            lPoint.Normalize();

            BoundaryPoints.Insert(lNextSelectedPoint, lPoint);
            BuildReachCones();

#if UNITY_EDITOR
            SceneView.RepaintAll();
#endif

            return lNextSelectedPoint;
        }

        /// <summary>
        /// Removes the specified boundary point
        /// </summary>
        /// <param name="rSelectedPoint"></param>
        /// <returns></returns>
        private int RemoveBoundaryPoint(int rSelectedPoint)
        {
            if (BoundaryPoints.Count <= 3) { return rSelectedPoint; }

            BoundaryPoints.RemoveAt(rSelectedPoint);
            BuildReachCones();

#if UNITY_EDITOR
            SceneView.RepaintAll();
#endif

            return (rSelectedPoint % BoundaryPoints.Count);
        }

        // ************************************** SUPPORT CLASSES **************************************

        /// <summary>
        /// Defines a set of directions that can actually be taken by the bone. These
        /// represent the area on the sphere the bone can swing to. They are 
        /// represented as tetrahedron.
        /// 
        /// A tetrahedron is a four plane triangular faces (ie a triangular pyramid).        
        /// For the reach cone, it's defined by an origin, visible point, and two boundary points
        /// </summary>
        [Serializable]
        public class ReachCone
        {
            /// <summary>
            /// Volume of the shape for determining if the visible point is
            /// is property positioned.
            /// </summary>
            public float Volume;

            /// <summary>
            /// Radial slice plane
            /// </summary>
            public Vector3 SlicePlane;

            /// <summary>
            /// Boundary plane
            /// </summary>
            public Vector3 BoundaryPlane;

            /// <summary>
            /// Origin (o)
            /// </summary>
            public Vector3 Origin;

            /// <summary>
            /// Visible point (a)
            /// </summary>
            public Vector3 VisiblePoint;

            /// <summary>
            /// Boundary point (b)
            /// </summary>
            public Vector3 BoundaryPoint1;

            /// <summary>
            /// Boundary point (c)
            /// </summary>
            public Vector3 BoundaryPoint2;

            /// <summary>
            /// Constructor
            /// </summary>
            public ReachCone(Vector3 rOrigin, Vector3 rVisiblePoint, Vector3 rBoundaryPoint1, Vector3 rBoundaryPoint2)
            {
                Origin = rOrigin;
                VisiblePoint = rVisiblePoint;
                BoundaryPoint1 = rBoundaryPoint1;
                BoundaryPoint2 = rBoundaryPoint2;

                Volume = 0;
                SlicePlane = Vector3.zero;
                BoundaryPlane = Vector3.zero;
            }

            /// <summary>
            /// The visible point is only valid if the volume is posative
            /// </summary>
            public bool IsVisiblePointValid
            {
                get { return Volume > 0; }
            }

            /// <summary>
            /// Page 4
            /// With the data set, process the values we know about so we don't
            /// have to do it over and over
            /// </summary>
            public void PreProcess()
            {
                Vector3 lCross = Vector3.Cross(VisiblePoint, BoundaryPoint1);

                Volume = Vector3.Dot(lCross, BoundaryPoint2) / 6.0f; // (1)

                SlicePlane = lCross.normalized; // (2)
                BoundaryPlane = Vector3.Cross(BoundaryPoint1, BoundaryPoint2).normalized; // (2)
            }
        }
    }
}
