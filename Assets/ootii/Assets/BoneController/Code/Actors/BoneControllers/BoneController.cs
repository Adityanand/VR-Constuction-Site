//#define ENABLE_PROFILING 

/// <summary> 
/// SERIALIZATION NOTES
/// 
/// Unity has some quirks when it comes to serialization. For example, it will not
/// keep references after deserialization. So, if a bone is store in the skeleton,
/// and in a motor, when everything is deserialized, there were be two instances instead
/// of one instance with two references.
/// 
/// So, the BoneControllerBone and BoneControllerMotor objects serialize indexes into the BoneControllerSkeleton 's bone list.
/// 
/// OnBeforeSkeletonSerialized (BoneControllerBone and BoneControllerMotor)
/// This is called before the skeleton is serialized so that the indexes of the current
/// bone list are stored.
/// 
/// OnAfterSkeletonDeserialized (BoneControllerBone and BoneControllerMotor)
/// This is called after the skeleton is deserialized so that the bone references can
/// be grabbed based on the stored indexes.
/// 
/// </summary>
using System;
using System.Collections.Generic;
using UnityEngine;
using com.ootii.Base;
using com.ootii.Data.Serializers;
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
    /// The IK skeleton is used to keep track of all the bones in the
    /// system and define constraints. Then, we can use the skeleton
    /// to manage the bones
    /// </summary>
    [Serializable]
    [ExecuteInEditMode]
    [AddComponentMenu("ootii/Bone Controller")]
    public class BoneController  : BaseBoneController, ISerializationCallbackReceiver
    {
        /// <summary>
        /// This is a bit odd to put here, but since the skeleton is also used
        /// to help render the inspector and scene GUI, we'll use this flag to
        /// help determine if it's time to update those views. We do this so
        /// the scene isn't flagged as dirty when we just need to redraw
        /// </summary>
        [NonSerialized]
        public static bool EditorForceRepaint = false;

        /// <summary>
        /// Maximum number of iterations the IK solver will use when finding
        /// an IK solution.
        /// </summary>
        public static int MaxIterations = 10;

        /// <summary>
        /// Transform the skeleton will be based on
        /// </summary>
        public Transform _RootTransform = null;
        public Transform RootTransform
        {
            get { return _RootTransform; }

            set
            {
                _RootTransform = value;
                if (_RootTransform == null)
                {
                    mRoot = null;

                    // Clear out the bone list
                    for (int i = 0; i < Bones.Count; i++)
                    {
                        Bones[i].Clear();
                    }

                    Bones.Clear();
                }
                else
                {
                    InitializeRoot(_RootTransform);
                }
            }
        }

        /// <summary>
        /// Defines the root bone of the skeleton
        /// </summary>
        protected BoneControllerBone mRoot = null;
        public BoneControllerBone Root
        {
            get { return mRoot; }
        }

        /// <summary>
        /// List of bones the skeleton manages. This is primarily used for
        /// serializations because of Unity's serialization depth issue. However,
        /// it can be used to access the bones too.
        /// </summary>
        public List<BoneControllerBone> Bones = new List<BoneControllerBone>();

        /// <summary>
        /// Motors used to drive the skeleton and it's bones
        /// </summary>
        [NonSerialized]
        public List<BoneControllerMotor> Motors = new List<BoneControllerMotor>();

        /// <summary>
        /// Since we can't deserialize derived clases (without them being a SerializableObject
        /// which won't go into a prefab), we need to store the definitions instead.
        /// </summary>
        [SerializeField]
        public List<string> MotorDefinitions = new List<string>();

        /// <summary>
        /// Used when processing bones so we don't load up extra stuff
        /// </summary>
        public string EditorBoneFilters = "IK_|FK_";

        /// <summary>
        /// Determines if we render the skeleton bones when the skeleton isn't
        /// selected in the editior.
        /// </summary>
        public bool EditorShowBones = true;

        /// <summary>
        /// Keeps us from reparsing the bone filters
        /// </summary>
        protected string[] mBoneFilters = null;

        /// <summary>
        /// Holds the flag for showing bone limits. This allows us to remember the
        /// setting while we're editing.
        /// </summary>
        public bool EditorShowBoneLimits = true;

        /// <summary>
        /// Determines if we'll render the bone colliders
        /// </summary>
        public bool EditorShowBoneColliders = false;

        /// <summary>
        /// Determines if we auto scale the bone and joint handles in the editor
        /// </summary>
        public bool EditorAutoScaleHandles = true;

        /// <summary>
        /// Determines if we're showing all bones or filtering to the ones that are selected
        /// </summary>
        public bool EditorShowSelectedBones = false;

        /// <summary>
        /// Forces us to use true colliders instead of our psuedo bone colliders
        /// </summary>
        public bool EditorForceTrueColliders = false;

        /// <summary>
        /// Due to capsule colliders not working correctly, it's better to force box colliders
        /// </summary>
        public bool EditorForceTrueBoxColliders = true;

        /// <summary>
        /// Track the last time the editor updated
        /// </summary>
        public float EditorLastTime = 0f;

        /// <summary>
        /// Allows us to track time in the editor
        /// </summary>
        public float EditorDeltaTime = 0f;

        /// <summary>
        /// Flag for us to synch the bones after a deserialization
        /// </summary>
        private bool mRaiseOnAfterDeserialize = false;

        /// <summary>
        /// Runs when the skeleton is loaded
        /// </summary>
        void Awake()
        {
            //Log.FileWrite("BoneControllerSkeleton.Awake");
        
            // If we need to initialize the bones after a deserialization, we
            // do it now. This way we don't try to do it when a bone hasn't
            // finished deserializing.
            if (mRaiseOnAfterDeserialize)
            {
                OnAfterDeserializeCore();
                mRaiseOnAfterDeserialize = false;
            }
        }

        /// <summary>
        /// Run before any update is run
        /// </summary>
        void Start()
        {
            //Log.FileWrite("BoneControllerSkeleton.Start");
        }

        /// <summary>
        /// Loads the skeleton based on the bones that are found
        /// in the object's hierarchy.
        /// </summary>
        /// <param name="rRoot">Root bone that we'll derive the bones from</param>
        /// <returns>True if bones are found or false if not</returns>
        public void InitializeRoot(Transform rRoot)
        {
            // Clear the bone filters
            mBoneFilters = EditorBoneFilters.Split('|');

            // If the root bone is null, we're going to find it
            if (rRoot == null)
            {
                // First, find the skinned mesh renderedr we're referencing
                SkinnedMeshRenderer lRenderer = GetComponent<SkinnedMeshRenderer>();
                if (lRenderer == null)
                {
                    lRenderer = gameObject.GetComponentInChildren<SkinnedMeshRenderer>();
                    if (lRenderer == null) { return; }
                }

                // From that, grab the root bone
                rRoot = lRenderer.rootBone;
            }

            // Clear out the bone list
            for (int i = 0; i < Bones.Count; i++)
            {
                Bones[i].Clear();
            }

            Bones.Clear();

            // Refresh the root
            if (rRoot != null)
            {
                mRoot = new BoneControllerBone(this);
                mRoot.Transform = rRoot;
            }

            // With the bones being reinitialized, we need to invalidate the motors
            // so they can regrab bones if needed
            for (int i = 0; i < Motors.Count; i++)
            {
                if (Motors[i].Bones.Count == 0)
                {
                    Motors[i].InvalidateBones();
                }
                else
                {
                    Motors[i].RefreshBones();
                }
            }
        }

        /// <summary>
        /// Support function for recursively loading bones
        /// </summary>
        /// <param name="rParent"></param>
        public void InitializeBone(BoneControllerBone rBone)
        {
            if (rBone == null) { return; }

            // Ensure our transform list exists
            if (BoneTransforms == null)
            {
                BoneTransforms = new List<Transform>();
            }

            // Add the bone to our list. This is primarily for serialization
            if (!Bones.Contains(rBone))
            {
                Bones.Add(rBone);
                BoneTransforms.Add(rBone._Transform);
            }

            // Process each child
            if (rBone.Transform != null)
            {
                for (int i = 0; i < rBone.Transform.childCount; i++)
                {
                    Transform lChildTransform = rBone.Transform.GetChild(i);
                    string lChildName = lChildTransform.name;

                    if (!TestBoneNameFilter(lChildName))
                    {
                        continue;
                    }

                    // We'll only get here if we have a valid name
                    BoneControllerBone lChild = rBone.AddChild(lChildTransform);

                    // This length isn't perfect since it will be overwritten if there are multiple
                    // children. However, it's a good default.
                    //rBone.Length = Vector3.Distance(lChild.Transform.position, rBone.Transform.position);

                    // Find children for this child
                    InitializeBone(lChild);
                }
            }
        }

        /// <summary>
        /// Recursively searches for a bone given the name and returns it if found
        /// </summary>
        /// <param name="rParent">Parent to search through</param>
        /// <param name="rBoneName">Bone to find</param>
        /// <returns>Transform of the bone or null</returns>
        public override IKBone GetBone(string rBoneName)
        {
            return GetChildBone(mRoot, rBoneName);
        }

        /// <summary>
        /// Recursively searches for a bone given the name and returns it if found
        /// </summary>
        /// <param name="rParent">Parent to search through</param>
        /// <param name="rBone">Bone transform to find</param>
        /// <returns>Transform of the bone or null</returns>
        public override IKBone GetBone(Transform rBone)
        {
            return GetChildBone(mRoot, rBone);
        }

        /// <summary>
        /// Recursively searches for a bone given the name and returns it if found
        /// </summary>
        /// <param name="rParent">Parent to search through</param>
        /// <param name="rBoneName">Bone to find</param>
        /// <returns>Transform of the bone or null</returns>
        public override IKBone GetBone(HumanBodyBones rBone)
        {
            if (gameObject == null) { return null; }

            Animator lAnimator = gameObject.GetComponent<Animator>();
            if (lAnimator == null) { return null; }

            Transform lBoneTransform = lAnimator.GetBoneTransform(rBone);
            return GetChildBone(mRoot, lBoneTransform);
        }

        /// <summary>
        /// Recursively search for a bone that matches the specifie name
        /// </summary>
        /// <param name="rParent">Parent to search through</param>
        /// <param name="rBoneName">Bone to find</param>
        /// <returns></returns>
        private BoneControllerBone GetChildBone(BoneControllerBone rParent, string rBoneName)
        {
            if (rParent == null) { return null; }

            // We found it. Get out fast
            if (rParent.Name == rBoneName) { return rParent; }

            // Handle the case where the bone name is nested in a namespace
            int lIndex = rParent.Name.IndexOf(':');
            if (lIndex >= 0)
            {
                string lParentName = rParent.Name.Substring(lIndex + 1);
                if (lParentName == rBoneName) { return rParent; }
            }

            // Since we didn't find it, check the children
            for (int i = 0; i < rParent.Children.Count; i++)
            {
                BoneControllerBone lBone = GetChildBone(rParent.Children[i], rBoneName);
                if (lBone != null) { return lBone; }
            }

            // Return nothing
            return null;
        }

        /// <summary>
        /// Recursively search for a bone that matches the specifie name
        /// </summary>
        /// <param name="rParent">Parent to search through</param>
        /// <param name="rBoneName">Bone to find</param>
        /// <returns></returns>
        private BoneControllerBone GetChildBone(BoneControllerBone rParent, Transform rBoneTransform)
        {
            if (rParent == null) { return null; }

            // We found it. Get out fast
            if (rParent.Transform == rBoneTransform) { return rParent; }

            // Since we didn't find it, check the children
            for (int i = 0; i < rParent.Children.Count; i++)
            {
                BoneControllerBone lBone = GetChildBone(rParent.Children[i], rBoneTransform);
                if (lBone != null) { return lBone; }
            }

            // Return nothing
            return null;
        }

        /// <summary>
        /// Adds a bone to the parent
        /// </summary>
        /// <param name="rChild"></param>
        public BoneControllerBone AddBone(BoneControllerBone rParent)
        {
            if (rParent == null) { return null; }

            // Determine where we'll add the child bone
            //int lIndex = Bones.IndexOf(rParent) + 1;
            //if (rParent.Children.Count > 0) { lIndex = Bones.IndexOf(rParent.Children[0]); }

            // Create the child bone and adds it to the skeleton
            BoneControllerBone lChild = rParent.InsertChild();

            return lChild;
        }

        /// <summary>
        /// Removes the child bone
        /// </summary>
        /// <param name="rChild"></param>
        public void RemoveBone(BoneControllerBone rBone)
        {
            BoneControllerBone lParent = rBone.Parent;
            if (lParent != null)
            {
                lParent.RemoveChild(rBone);
            }

            // Remove all the bones from the skeleton
            RemoveBoneChildren(rBone);
        }

        /// <summary>
        /// Recursively remove all child bones (and this bone) from the skeleton
        /// </summary>
        /// <param name="rParent"></param>
        private void RemoveBoneChildren(BoneControllerBone rParent)
        {
            for (int i = rParent.Children.Count - 1; i >= 0; i--)
            {
                RemoveBoneChildren(rParent.Children[i]);
            }

            Bones.Remove(rParent);
            BoneTransforms.Remove(rParent._Transform);
        }

        /// <summary>
        /// Tests if the point is contained by any of the bones' collider
        /// </summary>
        /// <param name="rPoint"></param>
        /// <returns></returns>
        public override IKBone TestPointCollision(Vector3 rPoint)
        {
            for (int i = 0; i < Bones.Count; i++)
            {
                if (Bones[i].TestPointCollision(rPoint))
                {
                    return Bones[i];
                }
            }

            return null;
        }

        /// <summary>
        /// Tests if the ray collides with the any of the bones' collider and if so, it returns the bone and collision point
        /// </summary>
        /// <param name="rStart"></param>
        /// <param name="rDirection"></param>
        /// <param name="rRange"></param>
        /// <param name="rHitPoint"></param>
        /// <returns></returns>
        public override bool TestRayCollision(Vector3 rStart, Vector3 rDirection, float rRange, out IKBone rHitBone, out Vector3 rHitPoint)
        {
            rHitBone = null;
            rHitPoint = Vector3.zero;

            if (rRange <= 0f) { return false; }
            if (rDirection.sqrMagnitude == 0f) { return false; }

            // If our starting point is in the bone, this is easy
            rHitBone = TestPointCollision(rStart);
            if (rHitBone != null)
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

                rHitBone = TestPointCollision(lEnd);
                if (rHitBone != null)
                {
                    // The second phase: Determine the closest point by cutting the distance in half
                    do
                    {
                        lDistance = lDistance * 0.5f;
                        rHitPoint = lStart + (rDirection * lDistance);
                        if (!rHitBone.TestPointCollision(rHitPoint))
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
        /// Determine if the specified bone name passes the filter test
        /// </summary>
        /// <param name="rName"></param>
        /// <returns></returns>
        public bool TestBoneNameFilter(string rName)
        {
            if (mBoneFilters == null && EditorBoneFilters.Length > 0)
            {
                mBoneFilters = EditorBoneFilters.Split('|');
            }

            if (mBoneFilters != null)
            {
                for (int i = 0; i < mBoneFilters.Length; i++)
                {
                    if (mBoneFilters[i].Trim().Length == 0) { continue; }

                    if (rName.IndexOf(mBoneFilters[i]) >= 0)
                    {
                        return false;
                    }
                }
            }

            return true;
        }
        
        /// <summary>
        /// Clears the rotations on all the bones, basically putting it
        /// back to the bind pose.
        /// </summary>
        public override void ResetBindPose()
        {
            for (int i = 0; i < Bones.Count; i++)
            {
                Bones[i].ClearRotation();
                Bones[i].Transform.localRotation = Bones[i].BindRotation;
                Bones[i].Transform.localPosition = Bones[i].BindPosition;
            }
        }

        /// <summary>
        /// Retrieves the motor based on the motor's name. It will return the first
        /// motor matching the specified name.
        /// </summary>
        /// <param name="rName"></param>
        /// <returns></returns>
        public override IKMotor GetMotor(string rName)
        {
            for (int i = 0; i < Motors.Count; i++)
            {
                if (Motors[i].Name == rName)
                {
                    return Motors[i];
                }
            }

            return null;
        }

        /// <summary>
        /// Retrieves the motor based on the motor's time. It will return the first
        /// motor of the specified type.
        /// </summary>
        /// <param name="rType"></param>
        /// <returns></returns>
        public override IKMotor GetMotor(Type rType)
        {
            for (int i = 0; i < Motors.Count; i++)
            {
                if (Motors[i].GetType() == rType)
                {
                    return Motors[i];
                }
            }

            return null;
        }

        /// <summary>
        /// Retrieves all the motors based on the motor's type. 
        /// </summary>
        /// <param name="rType">Type of motor we're looking for.</param>
        /// <returns>List of motors based on the specified type or null if none exist.</returns>
        public virtual List<IKMotor> GetMotors(Type rType)
        {
            List<IKMotor> lMotors = null;
            for (int i = 0; i < Motors.Count; i++)
            {
                if (Motors[i].GetType() == rType)
                {
                    if (lMotors == null) { lMotors = new List<IKMotor>(); }
                    lMotors.Add(Motors[i]);
                }
            }

            return lMotors;
        }

        /// <summary>
        /// Retrieves the motor based on the motor's time. It will return the first
        /// motor of the specified type.
        /// </summary>
        /// <param name="rType"></param>
        /// <returns></returns>
        public override T GetMotor<T>()
        {
            Type lType = typeof(T);
            for (int i = 0; i < Motors.Count; i++)
            {
                if (Motors[i].GetType() == lType)
                {
                    return Motors[i] as T;
                }
            }

            return default(T);
        }

        /// <summary>
        /// Retrieves all the motors based on the motor's type. 
        /// </summary>
        /// <returns>List of motors based on the specified type or null if none exist.</returns>
        public virtual List<T> GetMotors<T>() where T : BoneControllerMotor
        {
            List<T> lMotors = null;

            Type lType = typeof(T);
            for (int i = 0; i < Motors.Count; i++)
            {
                if (Motors[i].GetType() == lType)
                {
                    if (lMotors == null) { lMotors = new List<T>(); }
                    lMotors.Add(Motors[i] as T);
                }
            }

            return lMotors;
        }

        /// <summary>
        /// Retrieves the motor based on the motor's name. It will return the first
        /// motor of the specified type and name.
        /// </summary>
        /// <param name="rType"></param>
        /// <returns></returns>
        public override T GetMotor<T>(string rName)
        {
            Type lType = typeof(T);
            for (int i = 0; i < Motors.Count; i++)
            {
                if (Motors[i].GetType() == lType && Motors[i].Name == rName)
                {
                    return Motors[i] as T;
                }
            }

            return default(T);
        }

        /// <summary>
        /// Retrieves all the motors based on the motor's type. 
        /// </summary>
        /// <returns>List of motors based on the specified type or null if none exist.</returns>
        public virtual List<T> GetMotors<T>(string rName) where T : BoneControllerMotor
        {
            List<T> lMotors = null;

            Type lType = typeof(T);
            for (int i = 0; i < Motors.Count; i++)
            {
                if (Motors[i].GetType() == lType && Motors[i].Name == rName)
                {
                    if (lMotors == null) { lMotors = new List<T>(); }
                    lMotors.Add(Motors[i] as T);
                }
            }

            return lMotors;
        }

        /// <summary>
        /// Enables and disables motors of the specified type
        /// </summary>
        /// <param name="rType"></param>
        /// <returns></returns>
        public override void EnableMotors<T>(bool rEnable)
        {
            Type lType = typeof(T);
            for (int i = 0; i < Motors.Count; i++)
            {
                if (Motors[i].GetType() == lType)
                {
                    Motors[i].IsEnabled = rEnable;
                }
            }
        }

        /// <summary>
        /// Enables and disables motors of the specified type
        /// </summary>
        /// <param name="rType"></param>
        /// <returns></returns>
        public void EnableMotors(Type rType, bool rEnable)
        {
            for (int i = 0; i < Motors.Count; i++)
            {
                if (Motors[i].GetType() == rType)
                {
                    Motors[i].IsEnabled = rEnable;
                }
            }
        }

        /// <summary>
        /// Runs once per frame
        /// </summary>
        public void Update()
        {

#if UNITY_EDITOR

            // Time.deltaTime isn't valid when editing. So, we need to create
            // our own here.
            float lCurrentTime = (float)EditorApplication.timeSinceStartup;
            EditorDeltaTime = Mathf.Min(lCurrentTime - EditorLastTime, 0.01666f);
            EditorLastTime = lCurrentTime;

#endif
        }

        /// <summary>
        /// Update the bones themselves so they can override any animations
        /// </summary>
        public void LateUpdate()
        {
#if ENABLE_PROFILING
            ootii.Utilities.Profiler.Start(gameObject.name + " BoneControllerSkeleton  Update");
#endif

            // If we need to initialize the bones after a deserialization, we
            // do it now. This way we don't try to do it when a bone hasn't
            // finished deserializing.
            if (mRaiseOnAfterDeserialize)
            {
                OnAfterDeserializeCore();
                mRaiseOnAfterDeserialize = false;
            }

            // As long as we have a root, we can continue
            if (mRoot == null) { return; }

            // For each motor, we may need to call a physics update
            for (int lMotorIndex = 0; lMotorIndex < Motors.Count; lMotorIndex++)
            {
                if (Motors[lMotorIndex].IsEnabled)
                {
                    if (Application.isPlaying || Motors[lMotorIndex].IsEditorEnabled)
                    {
                        Motors[lMotorIndex].UpdateMotor();
                    }
                }
            }

            // Update the bones
            mRoot.Update();

#if ENABLE_PROFILING
            ootii.Utilities.Profiler.Stop(gameObject.name + " BoneControllerSkeleton  Update");
#endif
        }

        /// <summary>
        /// Function that will automatically set the bone colliders if
        /// they don't exist on the bones. It will not override colliders that exist.
        /// </summary>
        /// <param name="rType">Allows the overrider to define solutions other than 'human'</param>
        /// <param name="rDetailLevel">Future expansion to control which bones get colliders</param>
        public virtual void SetBoneColliders(string rType, int rDetailLevel)
        {
            if (rType.ToLower() == "human" || rType.ToLower() == "humanoid")
            {
                SetHumanoidBoneColliders(rDetailLevel);
            }
            else
            {
                SetBoneColliders(rDetailLevel);
            }
        }

        /// <summary>
        /// Set generic bone colliders as best we can
        /// </summary>
        /// <param name="rDetail"></param>
        public virtual void SetBoneColliders(int rDetailLevel)
        {
            if (rDetailLevel == EnumIKSkeletonDetailLevel.LOW)
            {
                for (int i = 0; i < Bones.Count; i++)
                {
                    BoneControllerBone lBone = Bones[i];
                    if (lBone.Length > 0.2f)
                    {
                        SetBoneCapsuleCollider(lBone, 1f, true);
                    }
                    else if (lBone.Length > 0.08f)
                    {
                        SetBoneSphereCollider(lBone, 1f, true, true);
                    }
                }
            }
            else if (rDetailLevel >= EnumIKSkeletonDetailLevel.MEDIUM)
            {
                for (int i = 0; i < Bones.Count; i++)
                {
                    BoneControllerBone lBone = Bones[i];
                    if (lBone.Length > 0.02f)
                    {
                        SetBoneCapsuleCollider(lBone, 1f, true);
                    }
                    else if (lBone.Length > 0.01f)
                    {
                        SetBoneSphereCollider(lBone, 1f, true, true);
                    }
                }
            }
        }

        /// <summary>
        /// Function that will automatically set the bone colliders if
        /// they don't exist on the bones. It will not override colliders that exist.
        /// </summary>
        /// <param name="rType">Allows the overrider to define solutions other than 'human'</param>
        /// <param name="rDetailLevel">Future expansion to control which bones get colliders</param>
        public virtual void SetHumanoidBoneColliders(int rDetailLevel)
        {
            if (rDetailLevel >= EnumIKSkeletonDetailLevel.LOW)
            {
                SetBoneSphereCollider(HumanBodyBones.Hips, 3f, false, true);

                if (EditorForceTrueColliders)
                {
                    SetBoneSphereCollider(HumanBodyBones.Spine, 1.5f, true, true);
                    SetBoneSphereCollider(HumanBodyBones.Chest, 1.5f, true, true);
                    SetBoneSphereCollider(HumanBodyBones.Head, 1.5f, true, true);
                }
                else
                {
                    SetBoneCapsuleCollider(HumanBodyBones.Spine, 5.5f, true);
                    SetBoneCapsuleCollider(HumanBodyBones.Chest, 5.0f, true);
                    SetBoneCapsuleCollider(HumanBodyBones.Head, 7.5f, true);
                }

                SetBoneCapsuleCollider(HumanBodyBones.RightUpperLeg, 1.5f, true);
                SetBoneCapsuleCollider(HumanBodyBones.RightLowerLeg, true);
                SetBoneCapsuleCollider(HumanBodyBones.RightUpperArm, 1.5f, true);
                SetBoneCapsuleCollider(HumanBodyBones.RightLowerArm, true);

                SetBoneCapsuleCollider(HumanBodyBones.LeftUpperLeg, 1.5f, true);
                SetBoneCapsuleCollider(HumanBodyBones.LeftLowerLeg, true);
                SetBoneCapsuleCollider(HumanBodyBones.LeftUpperArm, 1.5f, true);
                SetBoneCapsuleCollider(HumanBodyBones.LeftLowerArm, true);
            }

            if (rDetailLevel >= EnumIKSkeletonDetailLevel.MEDIUM)
            {
                if (EditorForceTrueColliders)
                {
                    SetBoneSphereCollider(HumanBodyBones.Neck, true);
                    SetBoneSphereCollider(HumanBodyBones.RightFoot, 0.75f, true, true);
                    SetBoneSphereCollider(HumanBodyBones.RightShoulder, 1.5f, true, true);
                    SetBoneSphereCollider(HumanBodyBones.RightHand, 1.0f, true, true);
                    SetBoneSphereCollider(HumanBodyBones.LeftFoot, 0.75f, true, true);
                    SetBoneSphereCollider(HumanBodyBones.LeftShoulder, 1.5f, true, true);
                    SetBoneSphereCollider(HumanBodyBones.LeftHand, 1.0f, true, true);
                }
                else
                {
                    SetBoneCapsuleCollider(HumanBodyBones.Neck, 5.0f, true);
                    SetBoneCapsuleCollider(HumanBodyBones.RightFoot, 2.5f, true);
                    SetBoneCapsuleCollider(HumanBodyBones.RightShoulder, 2.5f, true);
                    SetBoneCapsuleCollider(HumanBodyBones.RightHand, 5.0f, true);
                    SetBoneCapsuleCollider(HumanBodyBones.LeftFoot, 2.5f, true);
                    SetBoneCapsuleCollider(HumanBodyBones.LeftShoulder, 2.5f, true);
                    SetBoneCapsuleCollider(HumanBodyBones.LeftHand, 5.0f, true);
                }
            }

            // Only if we're high will we cycle through all the bones and add ones we missed
            if (rDetailLevel >= EnumIKSkeletonDetailLevel.HIGH)
            {
                for (int i = 0; i < Bones.Count; i++)
                {
                    BoneControllerBone lBone = Bones[i];
                    if (!EditorForceTrueColliders || lBone.Length > 0.02f)
                    {
                        SetBoneCapsuleCollider(lBone, 2f, true);
                    }
                    else if (lBone.Length > 0.01f)
                    {
                        SetBoneSphereCollider(lBone, 1f, true, true);
                    }
                }
            }
        }

        /// <summary>
        /// Sets a collider on the bone
        /// </summary>
        /// <param name="rBone"></param>
        protected void SetBoneBoxCollider(HumanBodyBones rBoneID, bool rTestForExisting)
        {
            BoneControllerBone lBone = GetBone(rBoneID) as BoneControllerBone;
            SetBoneBoxCollider(lBone, rTestForExisting);
        }

        /// <summary>
        /// Sets a collider on the bone
        /// </summary>
        /// <param name="rBone"></param>
        protected void SetBoneBoxCollider(BoneControllerBone rBone, bool rTestForExisting)
        {
            if (rTestForExisting)
            {
                if (EditorForceTrueColliders && (rBone == null || rBone._Transform == null || rBone._Transform.gameObject == null || rBone._Transform.gameObject.GetComponent<Collider>() != null))
                {
                    return;
                }

                if (rBone._ColliderSize.sqrMagnitude != 0f)
                {
                    return;
                }
            }

            Vector3 lSize = new Vector3(rBone.Length / 3f, rBone.Length / 3f, rBone.Length);
            Vector3 lCenter = rBone.BoneForward * (rBone.Length / 2f);

            rBone._ColliderType = EnumIKBoneColliderType.BOX;
            rBone._ColliderSize = lSize;

            // If we're using true colliders, set the box collider
            if (EditorForceTrueColliders)
            {
                BoxCollider lCollider = rBone._Transform.gameObject.AddComponent<BoxCollider>();
                lCollider.size = rBone.ToBoneForwardInv * lSize;
                lCollider.center = lCenter;
                lCollider.isTrigger = true;

                // Grab the collider on the bone (if there is one)
                Collider lBaseCollider = gameObject.GetComponent<Collider>();

                // If we have a capsule collider as part of a character controller, attept to ignore it
                if (lBaseCollider != null)
                {
                    UnityEngine.Physics.IgnoreCollision(lBaseCollider, lCollider, true);
                }
            }
        }

        /// <summary>
        /// Sets a collider on the bone
        /// </summary>
        /// <param name="rBone"></param>
        protected void SetBoneCapsuleCollider(HumanBodyBones rBoneID, bool rTestForExisting)
        {
            BoneControllerBone lBone = GetBone(rBoneID) as BoneControllerBone;
            SetBoneCapsuleCollider(lBone, 1f, rTestForExisting);
        }

        /// <summary>
        /// Sets a collider on the bone
        /// </summary>
        /// <param name="rBone"></param>
        protected void SetBoneCapsuleCollider(HumanBodyBones rBoneID, float rWidthMultiplier, bool rTestForExisting)
        {
            BoneControllerBone lBone = GetBone(rBoneID) as BoneControllerBone;
            SetBoneCapsuleCollider(lBone, rWidthMultiplier, rTestForExisting);
        }

        /// <summary>
        /// Sets a collider on the bone
        /// 
        /// Capsule direction
        /// 0 -> Capsule height is along the x-axis. 
        /// 1 -> Capsule height is along the y-axis. 
        /// 2 -> Capsule height is along the z-axis.
        /// 
        /// </summary>
        /// <param name="rBone"></param>
        protected void SetBoneCapsuleCollider(BoneControllerBone rBone, float rWidthMultiplier, bool rTestForExisting)
        {
            if (rBone == null) { return; }

            // If we're forcing box colliders, build there and leave
            if (EditorForceTrueColliders && EditorForceTrueBoxColliders)
            {
                SetBoneBoxCollider(rBone, rTestForExisting);
                return;
            }

            // If colliders exist, stop
            if (rTestForExisting)
            {
                if (EditorForceTrueColliders && (rBone == null || rBone._Transform == null || rBone._Transform.gameObject == null || rBone._Transform.gameObject.GetComponent<Collider>() != null))
                {
                    return;
                }

                if (rBone._ColliderSize.sqrMagnitude != 0f)
                {
                    return;
                }
            }

            int lDirection = 0;
            Vector3 lForward = rBone.BoneForward;
            Vector3 lCenter = lForward * (rBone.Length / 2f);

            rBone._ColliderType = EnumIKBoneColliderType.BOX;
            rBone._ColliderSize = new Vector3((rBone.Length / 5f) * rWidthMultiplier, (rBone.Length / 5f) * rWidthMultiplier, rBone.Length);

            // If we're using true colliders, set the box collider
            if (EditorForceTrueColliders)
            {
                // If the bone is aligned with an axis, we can use a capsule
                if (Mathf.Abs(lForward.x) > 0.98f)
                {
                    lDirection = 0;
                }
                else if (Mathf.Abs(lForward.y) > 0.98f)
                {
                    lDirection = 1;
                }
                else if (Mathf.Abs(lForward.z) > 0.98f)
                {
                    lDirection = 2;
                }
                // Capsules don't rotate nicely. So, if it isn't aligned
                // along an axis, we'll use a sphere collider instead.
                else
                {
                    SetBoneSphereCollider(rBone, 1f, true, rTestForExisting);
                    return;
                }

                CapsuleCollider lCollider = rBone._Transform.gameObject.AddComponent<CapsuleCollider>();
                lCollider.direction = lDirection;
                lCollider.height = rBone.Length;
                lCollider.radius = (rBone.Length / 5f) * rWidthMultiplier;
                lCollider.center = lCenter;
                lCollider.isTrigger = true;

                // Grab the collider on the bone (if there is one)
                Collider lBaseCollider = gameObject.GetComponent<Collider>();

                // If we have a capsule collider as part of a character controller, attept to ignore it
                if (lBaseCollider != null)
                {
                    UnityEngine.Physics.IgnoreCollision(lBaseCollider, lCollider, true);
                }
            }
        }

        /// <summary>
        /// Sets a collider on the bone
        /// </summary>
        /// <param name="rBone"></param>
        protected void SetBoneSphereCollider(HumanBodyBones rBoneID, bool rTestForExisting)
        {
            BoneControllerBone lBone = GetBone(rBoneID) as BoneControllerBone;
            SetBoneSphereCollider(lBone, 1f, true, rTestForExisting);
        }

        /// <summary>
        /// Sets a collider on the bone
        /// </summary>
        /// <param name="rBone"></param>
        protected void SetBoneSphereCollider(HumanBodyBones rBoneID, float rRadiusMultiplier, bool rCenter, bool rTestForExisting)
        {
            BoneControllerBone lBone = GetBone(rBoneID) as BoneControllerBone;
            SetBoneSphereCollider(lBone, rRadiusMultiplier, rCenter, rTestForExisting);
        }

        /// <summary>
        /// Sets a collider on the bone
        /// </summary>
        /// <param name="rBone"></param>
        protected void SetBoneSphereCollider(BoneControllerBone rBone, float rRadiusMultiplier, bool rCenter, bool rTestForExisting)
        {
            if (rBone == null) { return; }

            // Ensure colliders don't already exit
            if (rTestForExisting)
            {
                if (EditorForceTrueColliders && (rBone == null || rBone._Transform == null || rBone._Transform.gameObject == null || rBone._Transform.gameObject.GetComponent<Collider>() != null))
                {
                    return;
                }

                if (float.IsNaN(rBone._ColliderSize.x) || rBone._ColliderSize.sqrMagnitude != 0f)
                {
                    return;
                }
            }

            float lRadius = (rBone.Length / 2f) * rRadiusMultiplier;
            Vector3 lCenter = (rCenter ? rBone.BoneForward * (rBone.Length / 2f) : Vector3.zero);

            rBone._ColliderType = EnumIKBoneColliderType.SPHERE;
            rBone._ColliderSize = new Vector3(lRadius, lRadius, rBone.Length);

            // If we're using true colliders, set the box collider
            if (EditorForceTrueColliders)
            {
                SphereCollider lCollider = rBone._Transform.gameObject.AddComponent<SphereCollider>();
                lCollider.radius = lRadius;
                lCollider.center = lCenter;
                lCollider.isTrigger = true;

                // Grab the collider on the bone (if there is one)
                Collider lBaseCollider = gameObject.GetComponent<Collider>();

                // If we have a capsule collider as part of a character controller, attept to ignore it
                if (lBaseCollider != null)
                {
                    UnityEngine.Physics.IgnoreCollision(lBaseCollider, lCollider, true);
                }
            }
        }

        /// <summary>
        /// By default, removes all the bone colliders
        /// </summary>
        /// <param name="rDetailLevel"></param>
        public virtual void RemoveBoneColliders(int rDetailLevel)
        {
            for (int i = 0; i < Bones.Count; i++)
            {
                Bones[i]._ColliderType = EnumIKBoneColliderType.BOX;
                Bones[i]._ColliderSize = Vector3.zero;

                if (EditorForceTrueColliders)
                {
                    Transform lTransform = Bones[i]._Transform;
                    if (lTransform != null && lTransform.gameObject != null)
                    {
                        Collider lCollider = lTransform.gameObject.GetComponent<Collider>();
                        if (lCollider != null)
                        {
                            // Grab the collider on the bone (if there is one)
                            Collider lBaseCollider = gameObject.GetComponent<Collider>();

                            // Remove any ignore we added
                            if (lBaseCollider != null)
                            {
                                UnityEngine.Physics.IgnoreCollision(lBaseCollider, lCollider, false);
                            }

                            // Destroy the collider
                            DestroyImmediate(lCollider);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Function that will automatically set the bone colliders if
        /// they don't exist on the bones. It will not override colliders that exist.
        /// </summary>
        /// <param name="rType">Allows the overrider to define solutions other than 'human'</param>
        /// <param name="rDetailLevel">Future expansion to control which bones get colliders</param>
        public virtual void SetBoneJoints(string rType, int rDetailLevel)
        {
            if (rType.ToLower() == "human" || rType.ToLower() == "humanoid")
            {
                SetHumanoidBoneJoints(rDetailLevel);
            }
            else
            {
                SetBoneJoints(rDetailLevel);
            }
        }

        /// <summary>
        /// Set generic bone colliders as best we can
        /// </summary>
        /// <param name="rDetail"></param>
        public virtual void SetBoneJoints(int rDetailLevel)
        {            
        }

        /// <summary>
        /// Function that will automatically set the bone colliders if
        /// they don't exist on the bones. It will not override colliders that exist.
        /// </summary>
        /// <param name="rType">Allows the overrider to define solutions other than 'human'</param>
        /// <param name="rDetailLevel">Future expansion to control which bones get colliders</param>
        public virtual void SetHumanoidBoneJoints(int rDetailLevel)
        {
            BoneControllerBone lBone = null;

            if (rDetailLevel >= EnumIKSkeletonDetailLevel.LOW)
            {
                lBone = GetBone(HumanBodyBones.Head) as BoneControllerBone;
                if (lBone != null && lBone.Joint == null)
                {
                    LimitedSwingAndTwistJoint lJoint = new LimitedSwingAndTwistJoint(lBone);
                    lJoint.LimitTwist = true;
                    lJoint.MinTwistAngle = -20f;
                    lJoint.MaxTwistAngle = 20;

                    lJoint.LimitSwing = true;
                    lJoint.BoundaryPoints[0] = new Vector3( 0.56f,  0.62f, 0.55f);
                    lJoint.BoundaryPoints[1] = new Vector3(    0f,  0.94f, 0.34f);
                    lJoint.BoundaryPoints[2] = new Vector3(-0.56f,  0.46f, 0.55f);
                    lJoint.BoundaryPoints[3] = new Vector3(    0f, -0.83f, 0.55f);
                    lJoint.SmoothingIterations = 2;
                    lJoint.BuildReachCones();

                    lBone.Joint = lJoint;
                }

                lBone = GetBone(HumanBodyBones.Neck) as BoneControllerBone;
                if (lBone != null && lBone.Joint == null)
                {
                    LimitedSwingAndTwistJoint lJoint = new LimitedSwingAndTwistJoint(lBone);
                    lJoint.LimitTwist = true;
                    lJoint.MinTwistAngle = -60f;
                    lJoint.MaxTwistAngle = 60;

                    lJoint.LimitSwing = true;
                    lJoint.BoundaryPoints[0] = new Vector3(0.56f, 0.62f, 0.55f);
                    lJoint.BoundaryPoints[1] = new Vector3(0f, 0.94f, 0.34f);
                    lJoint.BoundaryPoints[2] = new Vector3(-0.56f, 0.46f, 0.55f);
                    lJoint.BoundaryPoints[3] = new Vector3(0f, -0.83f, 0.55f);
                    lJoint.SmoothingIterations = 2;
                    lJoint.BuildReachCones();

                    lBone.Joint = lJoint;
                }

                lBone = GetBone(HumanBodyBones.Chest) as BoneControllerBone;
                if (lBone != null && lBone.Joint == null)
                {
                    LimitedSwingAndTwistJoint lJoint = new LimitedSwingAndTwistJoint(lBone);
                    lJoint.LimitTwist = true;
                    lJoint.MinTwistAngle = -20f;
                    lJoint.MaxTwistAngle = 20;

                    lJoint.LimitSwing = true;
                    lJoint.BoundaryPoints[0] = new Vector3(0.39f, 0.068f, 0.92f);
                    lJoint.BoundaryPoints[1] = new Vector3(0f, 0.76f, 0.65f);
                    lJoint.BoundaryPoints[2] = new Vector3(-0.39f, 0.21f, 0.92f);
                    lJoint.BoundaryPoints[3] = new Vector3(0f, -0.10f, 0.99f);
                    lJoint.SmoothingIterations = 2;
                    lJoint.BuildReachCones();

                    lBone.Joint = lJoint;
                }

                lBone = GetBone(HumanBodyBones.Spine) as BoneControllerBone;
                if (lBone != null && lBone.Joint == null)
                {
                    LimitedSwingAndTwistJoint lJoint = new LimitedSwingAndTwistJoint(lBone);
                    lJoint.LimitTwist = true;
                    lJoint.MinTwistAngle = -20f;
                    lJoint.MaxTwistAngle = 20;

                    lJoint.LimitSwing = true;
                    lJoint.BoundaryPoints[0] = new Vector3(0.39f, 0.068f, 0.92f);
                    lJoint.BoundaryPoints[1] = new Vector3(0f, 0.76f, 0.65f);
                    lJoint.BoundaryPoints[2] = new Vector3(-0.39f, 0.21f, 0.92f);
                    lJoint.BoundaryPoints[3] = new Vector3(0f, -0.10f, 0.99f);
                    lJoint.SmoothingIterations = 2;
                    lJoint.BuildReachCones();

                    lBone.Joint = lJoint;
                }

                lBone = GetBone(HumanBodyBones.RightUpperArm) as BoneControllerBone;
                if (lBone != null && lBone.Joint == null)
                {
                    LimitedSwingAndTwistJoint lJoint = new LimitedSwingAndTwistJoint(lBone);
                    lJoint.LimitTwist = true;
                    lJoint.MinTwistAngle = -30f;
                    lJoint.MaxTwistAngle = 30;

                    lJoint.LimitSwing = true;
                    lJoint.BoundaryPoints[0] = new Vector3( 1f,  0f, 0f);
                    lJoint.BoundaryPoints[1] = new Vector3( 0f,  1f, 0f);
                    lJoint.BoundaryPoints[2] = new Vector3(-0.75f,  0f, -0.66f);
                    lJoint.BoundaryPoints[3] = new Vector3( 0f, -1f, 0f);
                    lJoint.SmoothingIterations = 2;
                    lJoint.BuildReachCones();

                    lBone.Joint = lJoint;
                }

                lBone = GetBone(HumanBodyBones.RightLowerArm) as BoneControllerBone;
                if (lBone != null && lBone.Joint == null)
                {
                    HingeSwingAndTwistJoint lJoint = new HingeSwingAndTwistJoint(lBone);
                    lJoint.AllowTwist = true;
                    lJoint.LimitTwist = true;
                    lJoint.MinTwistAngle = -45f;
                    lJoint.MaxTwistAngle = 45f;

                    lJoint.SwingAxis = Vector3.up;
                    lJoint.MinSwingAngle = -120f;
                    lJoint.MaxSwingAngle = 0f;

                    lBone.Joint = lJoint;
                }

                lBone = GetBone(HumanBodyBones.RightUpperLeg) as BoneControllerBone;
                if (lBone != null && lBone.Joint == null)
                {
                    LimitedSwingAndTwistJoint lJoint = new LimitedSwingAndTwistJoint(lBone);
                    lJoint.LimitTwist = true;
                    lJoint.MinTwistAngle = -50f;
                    lJoint.MaxTwistAngle = 50;

                    lJoint.LimitSwing = true;
                    lJoint.BoundaryPoints[0] = new Vector3(0.67f, -0.01f, 0.74f);
                    lJoint.BoundaryPoints[1] = new Vector3(0.02f, 0.95f, -0.31f);
                    lJoint.BoundaryPoints[2] = new Vector3(-0.65f, -0.01f, 0.76f);
                    lJoint.BoundaryPoints[3] = new Vector3(-0.025f, -0.86f, 0.5f);
                    lJoint.SmoothingIterations = 2;
                    lJoint.BuildReachCones();

                    lBone.Joint = lJoint;
                }

                lBone = GetBone(HumanBodyBones.RightLowerLeg) as BoneControllerBone;
                if (lBone != null && lBone.Joint == null)
                {
                    HingeSwingAndTwistJoint lJoint = new HingeSwingAndTwistJoint(lBone);
                    lJoint.AllowTwist = false;

                    lJoint.SwingAxis = Vector3.right;
                    lJoint.MinSwingAngle = 0f;
                    lJoint.MaxSwingAngle = 120f;

                    lBone.Joint = lJoint;
                }

                lBone = GetBone(HumanBodyBones.LeftUpperArm) as BoneControllerBone;
                if (lBone != null && lBone.Joint == null)
                {
                    LimitedSwingAndTwistJoint lJoint = new LimitedSwingAndTwistJoint(lBone);
                    lJoint.LimitTwist = true;
                    lJoint.MinTwistAngle = -30f;
                    lJoint.MaxTwistAngle = 30;

                    lJoint.LimitSwing = true;
                    lJoint.BoundaryPoints[0] = new Vector3(0.76f, 0f, -0.65f);
                    lJoint.BoundaryPoints[1] = new Vector3(0f, 1f, 0f);
                    lJoint.BoundaryPoints[2] = new Vector3(-1f, 0f, 0f);
                    lJoint.BoundaryPoints[3] = new Vector3(0f, -1f, 0f);
                    lJoint.SmoothingIterations = 2;
                    lJoint.BuildReachCones();

                    lBone.Joint = lJoint;
                }

                lBone = GetBone(HumanBodyBones.LeftLowerArm) as BoneControllerBone;
                if (lBone != null && lBone.Joint == null)
                {
                    HingeSwingAndTwistJoint lJoint = new HingeSwingAndTwistJoint(lBone);
                    lJoint.AllowTwist = true;
                    lJoint.LimitTwist = true;
                    lJoint.MinTwistAngle = -45f;
                    lJoint.MaxTwistAngle = 45f;

                    lJoint.SwingAxis = Vector3.up;
                    lJoint.MinSwingAngle = 0f;
                    lJoint.MaxSwingAngle = 120f;

                    lBone.Joint = lJoint;
                }

                lBone = GetBone(HumanBodyBones.LeftUpperLeg) as BoneControllerBone;
                if (lBone != null && lBone.Joint == null)
                {
                    LimitedSwingAndTwistJoint lJoint = new LimitedSwingAndTwistJoint(lBone);
                    lJoint.LimitTwist = true;
                    lJoint.MinTwistAngle = -50f;
                    lJoint.MaxTwistAngle = 50;

                    lJoint.LimitSwing = true;
                    lJoint.BoundaryPoints[0] = new Vector3(0.54f, -0.01f, 0.84f);
                    lJoint.BoundaryPoints[1] = new Vector3(-0.01f, 0.95f, -0.31f);
                    lJoint.BoundaryPoints[2] = new Vector3(-0.58f, -0.01f, 0.81f);
                    lJoint.BoundaryPoints[3] = new Vector3(0.03f, -0.82f, 0.58f);
                    lJoint.SmoothingIterations = 2;
                    lJoint.BuildReachCones();

                    lBone.Joint = lJoint;
                }

                lBone = GetBone(HumanBodyBones.LeftLowerLeg) as BoneControllerBone;
                if (lBone != null && lBone.Joint == null)
                {
                    HingeSwingAndTwistJoint lJoint = new HingeSwingAndTwistJoint(lBone);
                    lJoint.AllowTwist = false;

                    lJoint.SwingAxis = Vector3.right;
                    lJoint.MinSwingAngle = 0f;
                    lJoint.MaxSwingAngle = 120f;

                    lBone.Joint = lJoint;
                }
            }
        }

        /// <summary>
        /// By default, removes all the bone colliders
        /// </summary>
        /// <param name="rDetailLevel"></param>
        public virtual void RemoveBoneJoints(int rDetailLevel)
        {
            for (int i = 0; i < Bones.Count; i++)
            {
                BoneControllerJoint lJoint = Bones[i].Joint;
                if (lJoint != null) { lJoint.Bone = null; }

                Bones[i].Joint = null;
            }
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
            //Log.ConsoleWrite("BoneControllerSkeleton .OnBeforeSerialize()" + GetInstanceID().ToString() + " roac:" + mRaiseOnAfterDeserialize + " mc:" + Motors.Count.ToString(), false);

            // Don't allow a serialization if we haven't completed a deserialization. We don't
            // want to clear our definitions before we have a chance to instanciate them.
            if (mRaiseOnAfterDeserialize) { return; }

            // Rebuild the motor defintions
            MotorDefinitions.Clear();
            for (int i = 0; i < Motors.Count; i++)
            {
                if (Motors[i] != null)
                {
                    Motors[i].OnBeforeSkeletonSerialized(this);
                    MotorDefinitions.Add(JSONSerializer.Serialize(Motors[i], false));
                }
            }

            // Prep the bones
            for (int i = 0; i < Bones.Count; i++)
            {
                if (Bones[i] != null)
                {
                    Bones[i].OnBeforeSkeletonSerialized(this);
                }
            }
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
            //Log.ConsoleWrite("BoneControllerSkeleton .OnAfterDeserialized() " + GetInstanceID().ToString() + " roac:" + mRaiseOnAfterDeserialize + " mc:" + Motors.Count.ToString(), false);
            //Log.FileWrite("BoneControllerSkeleton.OnAfterDeserialize", false);

            // We may postpone the actual bone processing until the Update() functions
            // run. This way we don't try to process a bone before it's actually
            // been deserialized.
            mRaiseOnAfterDeserialize = true;

            // Materials need to be re-created after a deserialize (scene load). This
            // will force that to happen.
            DebugDraw.Invalidate();
        }

        /// <summary>
        /// Due to Unity's serialization limit on nested objects (7 levels),
        /// we have to store the bones in a flat list and then reconstruct
        /// our hierarchy after deserialization.
        /// 
        /// This function is called AFTER the skeleton has been deserialized. We
        /// put it in this seperate function so it can be called in the update phase.
        /// Otherwise, we may try to process bones that haven't finished deserializing.
        /// </summary>
        private void OnAfterDeserializeCore()
        {
            if (object.ReferenceEquals(gameObject, null)) { return; }

            //Log.ConsoleWrite("BoneControllerSkeleton .OnAfterDeserializedCore()" + GetInstanceID().ToString() + " roac:" + mRaiseOnAfterDeserialize + " mc:" + Motors.Count.ToString(), false);
            //Log.FileWrite("BoneControllerSkeleton.OnAfterDeserializeCore");

            // Reinitialize our transforms
            BoneTransforms.Clear();

            // Clean up the bones and allow them to post process
            if (Bones != null)
            {
                if (Bones.Count > 0) { mRoot = Bones[0]; }

                for (int i = Bones.Count - 1; i >= 0; i--)
                {
                    try
                    {
                        Bones[i].OnAfterSkeletonDeserialized(this);
                    }
                    catch
                    {
                        Bones.RemoveAt(i);
                    }
                }

                // Rebuild our transform lists to help raycasting
                for (int i = 0; i < Bones.Count; i++)
                {
                    BoneTransforms.Add(Bones[i]._Transform);
                }
            }

            // The last transform we add is for the skeleton's owner
            BoneTransforms.Add(gameObject.transform);

            // Since we can't deserialize derived clases (without them being a SerializableObject
            // which won't go into a prefab), we need to store the definitions instead.
            Motors.Clear();
            for (int i = 0; i < MotorDefinitions.Count; i++)
            {
                string lDefinition = MotorDefinitions[i];
                lDefinition = lDefinition.Replace(".Objects.", ".Actors.");

                BoneControllerMotor lMotor = JSONSerializer.Deserialize<BoneControllerMotor>(lDefinition);
                lMotor.OnAfterSkeletonDeserialized(this);

                Motors.Add(lMotor);
            }
        }

        // **************************************************************************************************
        // Following properties and function only valid while editing
        // **************************************************************************************************

        /// <summary>
        /// Allow the motor to control the scene GUI
        /// </summary>
        /// <returns></returns>
        public bool OnSceneGUI()
        {
            bool lIsDirty = false;

#if UNITY_EDITOR

#endif

            return lIsDirty;
        }

        /// <summary>
        /// Grabs the transform from the original mesh. This is primarily used to
        /// find the bind pose of a bone.
        /// </summary>
        /// <param name="rBoneName">Name of the bone to find</param>
        /// <returns>Transform respresenting the bone or null</returns>
        public Transform GetPrefabTransform(string rBoneName)
        {

#if UNITY_EDITOR

#if (UNITY_5 || UNITY_2017 || UNITY_2018_1)
            GameObject lRoot = UnityEditor.PrefabUtility.GetPrefabParent(gameObject) as GameObject;
#else
            GameObject lRoot = PrefabUtility.GetCorrespondingObjectFromSource(gameObject) as GameObject;
#endif

            if (lRoot != null)
            {
                Transform lBone = BoneController.FindTransform(lRoot.transform, rBoneName);
                if (lBone != null)
                {
                    return lBone;
                }
            }

#endif

            return null;
        }

        /// <summary>
        /// Used by motors and other inspectors/editors to render out a list of bones that
        /// the user can edit.
        /// </summary>
        /// <param name="rBones"></param>
        /// <param name="rSelectedBones">Bones that are selected in the scene</param>
        /// <param name="rSelectedBoneIndex">Bone index that is selected in the bone list</param>
        /// <returns></returns>
        public bool RenderBoneList(List<BoneControllerBone> rBones, ref int rSelectedBoneIndex, ref BoneControllerBone rSceneSelectedBone)
        {
            bool lIsDirty = false;

#if UNITY_EDITOR

            // Force the selected bone based on the input list
            BoneControllerBone lSelectedBone = (rSceneSelectedBone != null ? rSceneSelectedBone : (rSelectedBoneIndex < 0 ? null : rBones[rSelectedBoneIndex]));
            int lSelectedBoneIndex = (rSelectedBoneIndex >= 0 ? rSelectedBoneIndex : rBones.IndexOf(lSelectedBone));

            // List of bones that represent the chain (head->neck->etc)
            if (rBones.Count == 0)
            {
                EditorGUILayout.HelpBox("Add bones to make them part of the look chain", MessageType.Info);
            }
            else
            {
                for (int i = 0; i < rBones.Count; i++)
                {
                    EditorGUILayout.BeginVertical(GUI.skin.box);

                    // If this bone is selected, we'll highlight the title
                    bool lIsSelected = (i == lSelectedBoneIndex);

                    GUIStyle lRowStyle = (lIsSelected ? BoneController.SelectedRowStyle : BoneController.TitleRowStyle);

                    // Display the title
                    EditorGUILayout.BeginHorizontal(lRowStyle);

                    if (GUILayout.Button(new GUIContent(BoneController.ItemSelector), GUI.skin.label, GUILayout.Width(16)))
                    {
                        lSelectedBoneIndex = i;
                        lSelectedBone = rBones[i];
                    }

                    string lCleanName = "Select a bone";
                    if (rBones[i] != null) { lCleanName = rBones[i].CleanName + (rBones[i].Parent == null ? "" : " (from " + rBones[i].Parent.CleanName + ")"); }
                    EditorGUILayout.LabelField(lCleanName);

                    EditorGUILayout.EndHorizontal();

                    // Search based on the bone name
                    string lOldBoneName = (rBones[i] == null ? "" : rBones[i].Name);
                    string lNewBoneName = EditorGUILayout.TextField(new GUIContent("Bone Name", "Name of the bone to search for and pose."), lOldBoneName);
                    if (lNewBoneName != lOldBoneName)
                    {
                        lIsDirty = true;

                        BoneControllerBone lBone = GetBone(lNewBoneName) as BoneControllerBone;
                        if (lBone != null)
                        {
                            rBones[i] = lBone;
                            lSelectedBone = lBone;
                            lSelectedBoneIndex = i;
                        }
                    }

                    // Select based on the transform
                    Transform lOldBoneTransform = (rBones[i] == null ? null : rBones[i].Transform);
                    Transform lNewBoneTransform = EditorGUILayout.ObjectField(new GUIContent("Bone Transform", "Bone transform we are posing."), lOldBoneTransform, typeof(Transform), true) as Transform;
                    if (lNewBoneTransform != lOldBoneTransform)
                    {
                        lIsDirty = true;

                        BoneControllerBone lBone = GetBone(lNewBoneTransform) as BoneControllerBone;
                        if (lBone != null)
                        {
                            rBones[i] = lBone;
                            lSelectedBone = lBone;
                            lSelectedBoneIndex = i;
                        }
                    }

                    // Set the bone weight
                    float lOldBoneWeight = (rBones[i] == null ? 0 : rBones[i].Weight);
                    float lNewBoneWeight = EditorGUILayout.FloatField(new GUIContent("Rotation Weight", "Normalized weight this bone will be responsible for."), lOldBoneWeight);
                    if (lNewBoneWeight != lOldBoneWeight)
                    {
                        if (rBones[i] != null)
                        {
                            lIsDirty = true;
                            rBones[i].Weight = lNewBoneWeight;
                        }
                    }

                    EditorGUILayout.EndVertical();
                }
            }

            // Control buttons
            EditorGUILayout.BeginHorizontal();

            GUILayout.FlexibleSpace();

            if (GUILayout.Button(new GUIContent("+", "Add Bone"), EditorStyles.miniButtonLeft, GUILayout.Width(20)))
            {
                if (!rBones.Contains(lSelectedBone))
                {
                    lIsDirty = true;
                    rBones.Add(lSelectedBone);

                    lSelectedBoneIndex = rBones.Count - 1;
                }
                else
                {
                    lIsDirty = true;
                    rBones.Add(null);

                    lSelectedBone = null;
                    lSelectedBoneIndex = rBones.Count - 1;
                }
            }

            if (GUILayout.Button(new GUIContent("-", "Delete Bone"), EditorStyles.miniButtonRight, GUILayout.Width(20)))
            {
                if (lSelectedBoneIndex >= 0)
                {
                    lIsDirty = true;
                    rBones.RemoveAt(lSelectedBoneIndex);

                    if (lSelectedBoneIndex >= rBones.Count) { lSelectedBoneIndex = rBones.Count - 1; }
                    lSelectedBone = (lSelectedBoneIndex < 0 ? null : rBones[lSelectedBoneIndex]);
                }
            }

            EditorGUILayout.EndHorizontal();

            // Send back the index of the currently selected bone
            rSceneSelectedBone = lSelectedBone;
            rSelectedBoneIndex = lSelectedBoneIndex;

#endif

            return lIsDirty;
        }

        // **************************************************************************************************
        // Static functions
        // **************************************************************************************************

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
        private static GUIStyle sTitleRowStyle = null;
        public static GUIStyle TitleRowStyle
        {
            get
            {
                if (sTitleRowStyle == null)
                {
                    sTitleRowStyle = new GUIStyle();
                    sTitleRowStyle.normal.background = (Texture2D)Resources.Load<Texture>("IKTitle"); ;
                    sTitleRowStyle.border = new RectOffset(1, 1, 1, 1);
                    sTitleRowStyle.margin = new RectOffset(0, 0, 0, 0);
                    sTitleRowStyle.padding = new RectOffset(0, 0, 0, 0);
                }

                return sTitleRowStyle;
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

        /// <summary>
        /// Recursively searches for a bone given the name and returns it if found
        /// </summary>
        /// <param name="rParent">Parent to search through</param>
        /// <param name="rBoneName">Bone to find</param>
        /// <returns>Transform of the bone or null</returns>
        public static Transform FindTransform(Transform rParent, string rBoneName)
        {
            Transform lBone = null;
            for (int i = 0; i < rParent.transform.childCount; i++)
            {
                lBone = FindChildTransform(rParent.transform.GetChild(i), rBoneName);
                if (lBone != null) { return lBone; }
            }

            return lBone;
        }

        /// <summary>
        /// Recursively search for a bone that matches the specifie name
        /// </summary>
        /// <param name="rParent">Parent to search through</param>
        /// <param name="rBoneName">Bone to find</param>
        /// <returns></returns>
        private static Transform FindChildTransform(Transform rParent, string rBoneName)
        {
            // We found it. Get out fast
            if (rParent.name == rBoneName) { return rParent; }

            // Handle the case where the bone name is nested in a namespace
            int lIndex = rParent.name.IndexOf(':');
            if (lIndex >= 0)
            {
                string lParentName = rParent.name.Substring(lIndex + 1);
                if (lParentName == rBoneName) { return rParent; }
            }

            // Since we didn't find it, check the children
            for (int i = 0; i < rParent.transform.childCount; i++)
            {
                Transform lBone = FindChildTransform(rParent.transform.GetChild(i), rBoneName);
                if (lBone != null) { return lBone; }
            }

            // Return nothing
            return null;
        }
    }
}
