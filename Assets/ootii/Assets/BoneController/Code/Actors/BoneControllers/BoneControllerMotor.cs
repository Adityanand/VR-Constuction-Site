using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
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
    /// An IK motor drives the IK skeleton and it's bones. In this
    /// way, we can create lots of motor and run them together as needed.
    /// For example, a LookAt motor, FootCollider motor, etc.
    /// 
    /// See BoneControllerSkeleton  for serialization notes
    /// </summary>
    [Serializable]
    public class BoneControllerMotor : IKMotor
    {
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
        /// Determines if the motor is available to work
        /// </summary>
        public override bool IsEnabled
        {
            get { return _IsEnabled; }

            set
            {
                _IsEnabled = value;
                if (!_IsEnabled) { ResetBoneRotations(false); }
            }
        }

        /// <summary>
        /// Determines if we'll run the motor in the editor
        /// </summary>
        public override bool IsEditorEnabled
        {
            get { return _IsEditorEnabled; }

            set
            {
                _IsEditorEnabled = value;
                if (!_IsEditorEnabled) { ResetBoneRotations(false); }
            }
        }

        /// <summary>
        /// Determines if we'll apply joint limits to the bone rotations (each frame)
        /// </summary>
        public bool _ApplyLimits = true;
        public virtual bool ApplyLimits
        {
            get { return _ApplyLimits; }
            set { _ApplyLimits = value; }
        }

        /// <summary>
        /// Actual bones that are used by the motor. We do this here to help manage
        /// bone serialization and deserialization.
        /// </summary>
        protected List<BoneControllerBone> mBones = new List<BoneControllerBone>();
        public List<BoneControllerBone> Bones
        {
            get { return mBones; }
            set { mBones = value; }
        }

        /// <summary>
        /// Determines if the bones are currently valid
        /// </summary>
        protected bool mIsValid = false;

        /// <summary>
        /// Allows us to do some pre-processing if needed
        /// </summary>
        protected bool mIsFirstUpdate = true;

        /// <summary>
        /// For physics calculations, the amount of time that has elapsed
        /// </summary>
        protected float mPhysicsElapsedTime = 0f;

        /// <summary>
        /// Due to Unity serialization depth and reference issues issues, we need to serialize IDs and not
        /// the actual objects. This will help us recreate our hierarchies
        /// 
        /// Note: We keep this field public so it is serialized. This is due to Unity not
        /// supporting GetFields(BindingFlags). If we use binding flags, we only get back an empty array :(
        /// 
        /// Note: This list is ONLY used as we serialize and deserialize. Do not expect it to be valid
        /// at run-time or edit-time.
        /// </summary>
        public List<int> SerializationBoneIndexes = new List<int>();

        /// <summary>
        /// Default constructor
        /// </summary>
        public BoneControllerMotor()
            : base()
        {
        }

        /// <summary>
        /// Skeleton constructor
        /// </summary>
        /// <param name="rSkeleton"></param>
        public BoneControllerMotor(BoneController rSkeleton)
        {
            mSkeleton = rSkeleton;
            mSkeleton.Motors.Add(this);
        }

        /// <summary>
        /// If any bones are saved, they should be invalidated and re-grabbed
        /// from the skeleton.
        /// </summary>
        public virtual void InvalidateBones()
        {
            mIsValid = false;
        }

        /// <summary>
        /// Clears all the bones from the list
        /// </summary>
        public virtual void ClearBones()
        {
            mBones.Clear();
        }

        /// <summary>
        /// Unity doesn't persist individual object instances in multiple places.
        /// So, the same bone stored here and in the skeleton will deserialize
        /// into two different objects. So, we need to have a way of loading
        /// bones from the source skeleton. This function does that.
        /// 
        /// We can't load the bones in InvalidateBones because that's called from
        /// as seperate thread sometimes. So, we'll load the actual bone
        /// references (if needed) in a seperate function.
        /// </summary>
        public virtual void LoadBones()
        {
            mBones.Clear();
            for (int i = 0; i < SerializationBoneIndexes.Count; i++)
            {
                if (SerializationBoneIndexes[i] >= 0)
                {
                    mBones.Add(mSkeleton.Bones[SerializationBoneIndexes[i]]);
                }
            }

            mIsValid = true;
            mIsFirstUpdate = true;
        }

        /// <summary>
        /// When the skeleton is reloaded, we'll have different references for 
        /// the same bone. So, we need to update our references to point to the skeleton
        /// </summary>
        public virtual void RefreshBones()
        {
            for (int i = mBones.Count - 1; i >= 0; i--)
            {
                bool lFound = false;
                string lBoneName = mBones[i].Name;
                string lBoneParentName = (mBones[i].Parent != null ? mBones[i].Parent.Name : "");

                BoneControllerBone lNewBone = mSkeleton.GetBone(lBoneName) as BoneControllerBone;
                if (lNewBone != null)
                {
                    string lNewBoneParentName = (lNewBone.Parent != null ? lNewBone.Parent.Name : "");
                    if (lNewBoneParentName == lBoneParentName)
                    {
                        lFound = true;
                        mBones[i] = lNewBone;
                    }
                }

                // If we didn't find a matching bone, we need to remove the
                // old one. Otherwise, the user may think everything is ok
                if (!lFound)
                {
                    RemoveBone(i, false);
                    Debug.LogWarning("BoneControllerMotor.RefreshBones() - Matching bone for " + lBoneName + " was not found. " + (Name.Length > 0 ? Name : this.GetType().Name) + " removing old bone.");
                }
            }
        }

        /// <summary>
        /// Retrieves the bone if it exists or null if it doesn't
        /// </summary>
        /// <param name="rBoneID"></param>
        /// <returns></returns>
        public BoneControllerBone GetBone(HumanBodyBones rBoneID)
        {
            if (mBones == null || mBones.Count == 0) { return null; }

            BoneControllerBone lBone = mSkeleton.GetBone(rBoneID) as BoneControllerBone;
            if (lBone != null) 
            {
                if (mBones.IndexOf(lBone) >= 0)
                {
                    return lBone;
                } 
            }

            return null;
        }

        /// <summary>
        /// Retrieves the bone index if it exists or -1 if it doesn't.
        /// </summary>
        /// <param name="rBoneID"></param>
        /// <returns></returns>
        public int GetBoneIndex(HumanBodyBones rBoneID)
        {
            if (mBones == null || mBones.Count == 0) { return -1; }

            BoneControllerBone lBone = mSkeleton.GetBone(rBoneID) as BoneControllerBone;
            if (lBone != null) { return mBones.IndexOf(lBone); }

            return -1;
        }

        /// <summary>
        /// Remove the ANY motor's influence on the bones this
        /// frame. Note that if the motor is active, the influence will
        /// re-apply next frame.
        /// </summary>
        public virtual void ResetBoneRotations(bool rAllBones)
        {
            if (rAllBones)
            {
                for (int i = 0; i < mSkeleton.Bones.Count; i++)
                {
                    mSkeleton.Bones[i].ClearRotation();
                    mSkeleton.Bones[i].SetLocalRotation(Quaternion.identity, Quaternion.identity, 1f);
                }
            }
            else
            {
                for (int i = 0; i < mBones.Count; i++)
                {
                    mBones[i].SetLocalRotation(Quaternion.identity, Quaternion.identity, 1f);
                }
            }
        }

        /// <summary>
        /// Called by the skeleton to update the motor each frame. This
        /// function provides preprocessing before the inherited motor actually
        /// runs.
        /// </summary>
        public void UpdateMotor()
        {
            // No need to continue if we aren't active
            if (!_IsEnabled) { return; }

            // If we're running in the editor, we may not need to run
            if (!(Application.isPlaying || _IsEditorEnabled)) { return; }

            // It's possible our bones have become invalidated. If so, we need to reload them from the skeleton
            if (!mIsValid || mBones == null || mBones.Count == 0) { LoadBones(); }

            // If we're not fixed, update as fast as possible
            if (!_IsFixedUpdateEnabled || _FixedUpdateFPS <= 0f)
            {
                Update(Time.deltaTime, true);
                mIsFirstUpdate = false;
            }
            // If we are fixed, update on the interval based on our FPS
            else
            {
                float lFixedDeltaTime = 1.0f / _FixedUpdateFPS;

                // We'll cheat a bit. If the delta time is withing 10% of our desired time,
                // We'll just go with the fixed physics time. It makes things smoother
                if (Mathf.Abs(lFixedDeltaTime - Time.deltaTime) < lFixedDeltaTime * 0.1f)
                {
                    Update(lFixedDeltaTime, true);
                    mIsFirstUpdate = false;
                }
                // Outside of the 10%, we need to adjust accordingly
                else
                {
                    // Track the number of fixed updates to process
                    int lFixedUpdates = 0;

                    // Build up our elapsed time
#if UNITY_EDITOR
                    if (Application.isPlaying)
                    {
                        mPhysicsElapsedTime += Time.deltaTime;
                    }
                    else
                    {
                        mPhysicsElapsedTime += mSkeleton.EditorDeltaTime;
                    }
#else
                    mPhysicsElapsedTime += Time.deltaTime;
#endif

                    // If the elapsed time exceeds our desired update schedule, it's
                    // time for us to do a physics update. In fact, if the system
                    // is running slow we may need to do multiple updates
                    while (mPhysicsElapsedTime >= lFixedDeltaTime)
                    {
                        lFixedUpdates++;
                        mPhysicsElapsedTime -= lFixedDeltaTime;

                        // Fail safe. We can have long delta times when debugging and such
                        if (lFixedUpdates >= 5)
                        {
                            mPhysicsElapsedTime = 0;
                            break;
                        }
                    }

                    // Do as many updates as we need to in order to simulate
                    // the desired frame rates
                    if (lFixedUpdates > 0)
                    {
                        for (int i = 0; i < lFixedUpdates; i++)
                        {
                            Update(lFixedDeltaTime, true);
                            mIsFirstUpdate = false;
                        }
                    }
                    // In this case, there shouldn't be an update. This typically
                    // happens when the true FPS is much faster than our desired FPS
                    else
                    {
                        Update(lFixedDeltaTime, false);
                    }
                }
            }
        }

        /// <summary>
        /// Process the motor each frame so that it can update the bone rotations.
        /// This is the function that should be overridden in each motor
        /// </summary>
        /// <param name="rDeltaTime">Delta time to use for the update</param>
        /// <param name="rUpdate">Determines if it is officially time to do the update</param>
        protected virtual void Update(float rDeltaTime, bool rUpdate)
        {
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
            if (mSkeleton == null) { return; }
            if (mSkeleton != rSkeleton) { return; }

            // Grab the indexes of the bones
            SerializationBoneIndexes.Clear();
            for (int i = 0; i < mBones.Count; i++)
            {
                SerializationBoneIndexes.Add(mSkeleton.Bones.IndexOf(mBones[i]));
            }
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
        /// 
        /// 
        /// </summary>
        /// <param name="rSkeleton"></param>
        public virtual void OnAfterSkeletonDeserialized(BoneController rSkeleton)
        {
            mSkeleton = rSkeleton;

            // Reload the bones based on the IDs that we serialized earlier.
            // We do this since the references aren't kept as expected. We can't
            // call the LoadBones() function because the test for mSkeleton == null
            // isn't allowed on the load thread.
            mBones.Clear();
            for (int i = 0; i < SerializationBoneIndexes.Count; i++)
            {
                if (SerializationBoneIndexes[i] >= 0)
                {
                    mBones.Add(mSkeleton.Bones[SerializationBoneIndexes[i]]);
                }
            }

            //Flag our bones as initialized and valid
            mIsValid = true;
        }

        // **************************************************************************************************
        // Following properties and function only valid while editing
        // **************************************************************************************************

        /// <summary>
        /// Create our own the task inspector GUI
        /// </summary>
        public virtual bool OnInspectorGUI(List<BoneControllerBone> rSelectedBones)
        {
            bool lIsDirty = false;

#if UNITY_EDITOR

            // List of general motion properties
            PropertyInfo[] mBaseProperties = typeof(BoneControllerMotor).GetProperties();

            // Render out the accessable properties using reflection
            PropertyInfo[] lProperties = GetType().GetProperties();
            foreach (PropertyInfo lProperty in lProperties)
            {
                if (!lProperty.CanWrite) { continue; }

                string lTooltip = "";
                object[] lAttributes = lProperty.GetCustomAttributes(typeof(IKTooltipAttribute), true);
                foreach (IKTooltipAttribute lAttribute in lAttributes)
                {
                    lTooltip = lAttribute.Tooltip;
                }

                // Unfortunately Binding flags don't seem to be working. So,
                // we need to ensure we don't include base properties
                bool lAdd = true;
                for (int i = 0; i < mBaseProperties.Length; i++)
                {
                    if (lProperty.Name == mBaseProperties[i].Name)
                    {
                        lAdd = false;
                        break;
                    }
                }

                if (!lAdd) { continue; }

                string lFriendlyName = StringHelper.FormatCamelCase(lProperty.Name);

                // Grab the current value
                object lOldValue = lProperty.GetValue(this, null);

                // Based on the type, show an edit field
                if (lProperty.PropertyType == typeof(string))
                {
                    string lNewValue = EditorGUILayout.TextField(new GUIContent(lFriendlyName, lTooltip), (string)lOldValue);
                    if (lNewValue != (string)lOldValue)
                    {
                        lIsDirty = true;
                        lProperty.SetValue(this, lNewValue, null);
                    }
                }
                else if (lProperty.PropertyType == typeof(int))
                {
                    int lNewValue = EditorGUILayout.IntField(new GUIContent(lFriendlyName, lTooltip), (int)lOldValue);
                    if (lNewValue != (int)lOldValue)
                    {
                        lIsDirty = true;
                        lProperty.SetValue(this, lNewValue, null);
                    }
                }
                else if (lProperty.PropertyType == typeof(float))
                {
                    float lNewValue = EditorGUILayout.FloatField(new GUIContent(lFriendlyName, lTooltip), (float)lOldValue);
                    if (lNewValue != (float)lOldValue)
                    {
                        lIsDirty = true;
                        lProperty.SetValue(this, lNewValue, null);
                    }
                }
                else if (lProperty.PropertyType == typeof(bool))
                {
                    bool lNewValue = EditorGUILayout.Toggle(new GUIContent(lFriendlyName, lTooltip), (bool)lOldValue);
                    if (lNewValue != (bool)lOldValue)
                    {
                        lIsDirty = true;
                        lProperty.SetValue(this, lNewValue, null);
                    }
                }
                else if (lProperty.PropertyType == typeof(Vector2))
                {
                    Vector2 lNewValue = EditorGUILayout.Vector2Field(new GUIContent(lFriendlyName, lTooltip), (Vector2)lOldValue);
                    if (lNewValue != (Vector2)lOldValue)
                    {
                        lIsDirty = true;
                        lProperty.SetValue(this, lNewValue, null);
                    }
                }
                else if (lProperty.PropertyType == typeof(Vector3))
                {
                    Vector3 lNewValue = EditorGUILayout.Vector3Field(new GUIContent(lFriendlyName, lTooltip), (Vector3)lOldValue);
                    if (lNewValue != (Vector3)lOldValue)
                    {
                        lIsDirty = true;
                        lProperty.SetValue(this, lNewValue, null);
                    }
                }
                else if (lProperty.PropertyType == typeof(Vector4))
                {
                    Vector4 lNewValue = EditorGUILayout.Vector4Field(lFriendlyName, (Vector4)lOldValue);
                    if (lNewValue != (Vector4)lOldValue)
                    {
                        lIsDirty = true;
                        lProperty.SetValue(this, lNewValue, null);
                    }
                }
            }

#endif

            return lIsDirty;
        }

        /// <summary>
        /// Allow the motor to control the scene GUI
        /// </summary>
        /// <returns></returns>
        public virtual bool OnSceneGUI(List<BoneControllerBone> rSelectedBones)
        {
            bool lIsDirty = false;

#if UNITY_EDITOR

#endif

            return lIsDirty;
        }

        /// <summary>
        /// Simpler version of Render Bone List that manages the selected bone list
        /// </summary>
        /// <param name="rBones">Bones to list</param>
        /// <param name="rSelectedBones">List of currently selected bones</param>
        /// <returns>Determines if the selected bone has changed</returns>
        protected bool RenderBoneList(List<BoneControllerBone> rBones, List<BoneControllerBone> rSelectedBones)
        {
            return RenderBoneList(rBones, rSelectedBones, false, 0);
        }

        /// <summary>
        /// Simpler version of Render Bone List that manages the selected bone list
        /// </summary>
        /// <param name="rBones">Bones to list</param>
        /// <param name="rSelectedBones">List of currently selected bones</param>
        /// <returns>Determines if the selected bone has changed</returns>
        protected bool RenderBoneList(List<BoneControllerBone> rBones, List<BoneControllerBone> rSelectedBones, bool rIncludeChildren)
        {
            return RenderBoneList(rBones, rSelectedBones, rIncludeChildren, 0);
        }

        /// <summary>
        /// Simpler version of Render Bone List that manages the selected bone list
        /// </summary>
        /// <param name="rBones">Bones to list</param>
        /// <param name="rSelectedBones">List of currently selected bones</param>
        /// <returns>Determines if the selected bone has changed</returns>
        protected bool RenderBoneList(List<BoneControllerBone> rBones, List<BoneControllerBone> rSelectedBones, int rMaxBones)
        {
            return RenderBoneList(rBones, rSelectedBones, false, rMaxBones);
        }

        /// <summary>
        /// Simpler version of Render Bone List that manages the selected bone list
        /// </summary>
        /// <param name="rBones">Bones to list</param>
        /// <param name="rSelectedBones">List of currently selected bones</param>
        /// <returns>Determines if the selected bone has changed</returns>
        protected bool RenderBoneList(List<BoneControllerBone> rBones, List<BoneControllerBone> rSelectedBones, bool rIncludeChildren, int rMaxBones)
        {
            bool lIsDirty = false;

            // Force the selected bone based on the input list
            BoneControllerBone lSelectedBone = null;
            int lSelectedBoneIndex = -1;
            if (rSelectedBones.Count > 0)
            {
                lSelectedBone = rSelectedBones[0];
                lSelectedBoneIndex = mBones.IndexOf(lSelectedBone);
            }

            BoneControllerBone lNewSelectedBone = lSelectedBone;
            int lNewSelectedBoneIndex = lSelectedBoneIndex;
            bool lIsListDirty = RenderBoneList(mBones, ref lNewSelectedBoneIndex, ref lNewSelectedBone, rIncludeChildren, rMaxBones);
            if (lIsListDirty)
            {
                lIsDirty = true;
            }

            if (lNewSelectedBoneIndex != lSelectedBoneIndex)
            {
                rSelectedBones.Clear();
                if (lNewSelectedBone != null)
                {
                    rSelectedBones.Add(lNewSelectedBone);
                }

                // Force a readraw
                BoneController.EditorForceRepaint = true;
            }

            return lIsDirty;
        }
        
        /// <summary>
        /// Used by motors and other inspectors/editors to render out a list of bones that
        /// the user can edit.
        /// </summary>
        /// <param name="rBones"></param>
        /// <param name="rSelectedBones">Bones that are selected in the scene</param>
        /// <param name="rSelectedBoneIndex">Bone index that is selected in the bone list</param>
        /// <returns></returns>
        protected bool RenderBoneList(List<BoneControllerBone> rBones, ref int rSelectedBoneIndex, ref BoneControllerBone rSceneSelectedBone, bool rIncludeChildren, int rMaxBones)
        {
            bool lIsDirty = false;

#if UNITY_EDITOR

            // Force the selected bone based on the input list
            BoneControllerBone lSelectedBone = (rSceneSelectedBone != null ? rSceneSelectedBone : (rSelectedBoneIndex < 0 ? null : rBones[rSelectedBoneIndex]));
            int lSelectedBoneIndex = (rSelectedBoneIndex >= 0 ? rSelectedBoneIndex : rBones.IndexOf(lSelectedBone));

            // List of bones that represent the chain (head->neck->etc)
            if (rBones.Count == 0)
            {
                EditorGUILayout.HelpBox("Select a bone in the scene view and press '+' to make it part of the motor", MessageType.Info);
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

                        BoneControllerBone lBone = mSkeleton.GetBone(lNewBoneName) as BoneControllerBone;
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

                        BoneControllerBone lBone = mSkeleton.GetBone(lNewBoneTransform) as BoneControllerBone;
                        if (lBone != null)
                        {
                            rBones[i] = lBone;
                            lSelectedBone = lBone;
                            lSelectedBoneIndex = i;
                        }
                    }

                    // Render out the motor specific bone details
                    bool lIsBoneDirty = RenderBone(i, rBones[i]);
                    if (lIsBoneDirty)
                    {
                        lSelectedBoneIndex = i;
                        lSelectedBone = rBones[i];

                        lIsDirty = true;
                    }

                    EditorGUILayout.EndVertical();
                }
            }

            // Control buttons
            EditorGUILayout.BeginHorizontal();

            GUILayout.FlexibleSpace();

            if (GUILayout.Button(new GUIContent("Clear", "Clears all the bones"), EditorStyles.miniButton, GUILayout.Width(50)))
            {
                if (EditorUtility.DisplayDialog("Bone Controller", "Delete all bones from the motor?", "Yes", "No"))
                {
                    lIsDirty = true;
                    ClearBones();
                }
            }
            
            if (rMaxBones <= 0 || mBones.Count < rMaxBones)
            {
                if (GUILayout.Button(new GUIContent("+", "Add Bone"), EditorStyles.miniButtonLeft, GUILayout.Width(20)))
                {
                    if (!rBones.Contains(lSelectedBone))
                    {
                        lIsDirty = true;
                        lSelectedBoneIndex = rBones.Count;

                        AddBone(lSelectedBone, rIncludeChildren);
                    }
                    else
                    {
                        lIsDirty = true;

                        lSelectedBone = null;
                        lSelectedBoneIndex = rBones.Count;

                        AddBone(lSelectedBone, rIncludeChildren);
                    }
                }
            }

            if (GUILayout.Button(new GUIContent("-", "Delete Bone"), EditorStyles.miniButtonRight, GUILayout.Width(20)))
            {
                if (lSelectedBoneIndex >= 0)
                {
                    if (EditorUtility.DisplayDialog("Bone Controller", "Delete this bone (and its children) from the motor?", "Yes", "No"))
                    {
                        lIsDirty = true;

                        RemoveBone(lSelectedBoneIndex, rIncludeChildren);

                        if (lSelectedBoneIndex >= rBones.Count) { lSelectedBoneIndex = rBones.Count - 1; }
                        lSelectedBone = (lSelectedBoneIndex < 0 ? null : rBones[lSelectedBoneIndex]);
                    }
                }
            }

            EditorGUILayout.EndHorizontal();

            // Send back the index of the currently selected bone
            rSceneSelectedBone = lSelectedBone;
            rSelectedBoneIndex = lSelectedBoneIndex;

#endif

            return lIsDirty;
        }

        /// <summary>
        /// Render out the motor specific bone details
        /// </summary>
        /// <param name="rBone"></param>
        /// <returns></returns>
        protected virtual bool RenderBone(int rIndex, BoneControllerBone rBone)
        {
            return false;
        }

        /// <summary>
        /// Allows the motor to process any specific bone logic after
        /// a bone has been added
        /// </summary>
        /// <param name="rIndex">Index position of the new bone</param>
        /// <param name="rBone">New bone that was added</param>
        public virtual void AddBone(BoneControllerBone rBone, bool rIncludeChildren)
        {
            if (rBone == null || !mBones.Contains(rBone))
            {
                mBones.Add(rBone);
            }

            if (rBone != null && rIncludeChildren)
            {
                for (int i = 0; i < rBone.Children.Count; i++)
                {
                    AddBone(rBone.Children[i], rIncludeChildren);
                }
            }
        }

        /// <summary>
        /// Allows the motor to process any specific bone logic after 
        /// a bone has been deleted
        /// </summary>
        /// <param name="rIndex">Index position the bone was at</param>
        /// <param name="rBone">Bone that was deleted</param>
        protected virtual void RemoveBone(int rBoneIndex, bool rIncludeChildren)
        {
            if (rBoneIndex < 0 || rBoneIndex >= mBones.Count) { return; }

            BoneControllerBone rBone = mBones[rBoneIndex];
            RemoveBone(rBone, rIncludeChildren);
        }

        /// <summary>
        /// Allows the motor to process any specific bone logic after 
        /// a bone has been deleted
        /// </summary>
        /// <param name="rIndex">Index position the bone was at</param>
        /// <param name="rBone">Bone that was deleted</param>
        protected virtual void RemoveBone(BoneControllerBone rBone, bool rIncludeChildren)
        {
            if (mBones.Contains(rBone)) 
            {
                mBones.Remove(rBone);
            }

            if (rBone != null && rIncludeChildren)
            {
                for (int i = 0; i < rBone.Children.Count; i++)
                {
                    RemoveBone(rBone.Children[i], rIncludeChildren);
                }
            }
        }
    }
}
