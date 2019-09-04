using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using com.ootii.Geometry;
using com.ootii.Helpers;
using com.ootii.Actors.BoneControllers;
using com.ootii.Utilities;
using com.ootii.Utilities.Debug;

[CanEditMultipleObjects]
[CustomEditor(typeof(BoneController))]
public class BoneControllerEditor : Editor
{
    private static Color SkeletonColor = new Color(0f, 1f, 1f, 0.1f);

    private static Color SkeletonColliderColor = new Color(0f, 1f, 1f, 0.8f);

    private static Color SkeletonSelectedColor = new Color(1f, 1f, 0f, 0.6f);

    // Helps us keep track of when the list needs to be saved. This
    // is important since some changes happen in scene.
    private bool mIsDirty;

    // Row styles
    private Texture mBackground;
    private Texture mItemSelector;

    private GUIStyle mRowStyle;
    private GUIStyle mTitleRowStyle;
    private GUIStyle mSelectedRowStyle;
    private GUIStyle mRedXStyle;
    private GUIStyle mGreenPlusStyle;
    private GUIStyle mBluePlusStyle;
    private GUIStyle mBlueGearStyle;

    // The actual class we're storing
    private BoneController mSkeleton;
    private SerializedObject mSkeletonSO;

    // Currently selected bone
    private BoneControllerBone mSelectedBone;

    // List of bones that are flagged as selected and should be drawn a different way
    private List<BoneControllerBone> mSelectedBones = new List<BoneControllerBone>();

    // Currently selected motor
    private int mSelectedMotorIndex = 0;

    // Drop down values
    private int mSelectedMotorTypeIndex = 0;
    private List<Type> mMotorTypes = new List<Type>();
    private List<String> mMotorNames = new List<string>();

    // Determines if we show the seconds
    private bool mShowBones = false;
    private bool mShowMotors = false;
    private bool mShowSettings = false;

    // Help text
    private string mMotorHelp = "Add a motor in order to control the skeleton and its bones.";
    private string mPropertyHelp = "Select a motor to modify its properties. ";

    // String used to limit the bone results
    private string mBoneSearchString = "";

    // provides a list of style names
    private int mJointStyleNameIndex = 0;
    private int mColliderStyleNameIndex = 0;
    protected string[] mStyleNames = new string[] { "Humanoid", "Other" };

    // provides a list for our detail levels
    private int mJointDetailLevelIndex = 0;
    private int mColliderDetailLevelIndex = 0;
    protected string[] mDetailLevels = new string[] { "Low", "Medium", "High" };

    // Track the mouse down position so we know if it moved
    private Vector2 mMouseDownPosition = Vector2.zero;

    /// <summary>
    /// Called when the script object is loaded
    /// </summary>
    private void OnEnable()
    {
        // Grab the serialized objects
        mSkeleton = (BoneController)target;
        mSkeletonSO = new SerializedObject(target);

        // Load the textures
        if (mBackground == null) { mBackground = Resources.Load<Texture>("IKBackground"); }
        if (mItemSelector == null) { mItemSelector = Resources.Load<Texture>("IKDot"); }

        // Styles for selected rows
        if (mTitleRowStyle == null)
        {
            mTitleRowStyle = new GUIStyle();
            mTitleRowStyle.normal.background = (Texture2D)Resources.Load<Texture>("IKTitle");
            mTitleRowStyle.border = new RectOffset(1, 1, 1, 1);
            mTitleRowStyle.margin = new RectOffset(0, 0, 0, 0);
            mTitleRowStyle.padding = new RectOffset(0, 0, 0, 0);
        }

        if (mRowStyle == null)
        {
            mRowStyle = new GUIStyle();
            mRowStyle.border = new RectOffset(1, 1, 1, 1);
            mRowStyle.margin = new RectOffset(0, 0, 0, 0);
            mRowStyle.padding = new RectOffset(0, 0, 0, 0);
        }

        if (mSelectedRowStyle == null)
        {
            mSelectedRowStyle = new GUIStyle();
            mSelectedRowStyle.normal.background = (Texture2D)Resources.Load<Texture>("IKBorder");
            mSelectedRowStyle.border = new RectOffset(1, 1, 1, 1);
            mSelectedRowStyle.margin = new RectOffset(0, 0, 0, 0);
            mSelectedRowStyle.padding = new RectOffset(0, 0, 0, 0);
        }

        if (mRedXStyle == null)
        {
            mRedXStyle = new GUIStyle();
            mRedXStyle.normal.background = Resources.Load<Texture2D>(EditorGUIUtility.isProSkin ? "RedSquareX" : "RedSquareX_Light");
            mRedXStyle.margin = new RectOffset(0, 0, 2, 0);
        }

        if (mGreenPlusStyle == null)
        {
            mGreenPlusStyle = new GUIStyle();
            mGreenPlusStyle.normal.background = Resources.Load<Texture2D>("BlueSquareMinus");
            mGreenPlusStyle.margin = new RectOffset(0, 0, 2, 0);
        }

        if (mBluePlusStyle == null)
        {
            mBluePlusStyle = new GUIStyle();
            mBluePlusStyle.normal.background = Resources.Load<Texture2D>(EditorGUIUtility.isProSkin ? "BlueSquarePlus" : "BlueSquarePlus_Light");
            mBluePlusStyle.margin = new RectOffset(0, 0, 2, 0);
        }

        if (mBlueGearStyle == null)
        {
            mBlueGearStyle = new GUIStyle();
            mBlueGearStyle.normal.background = Resources.Load<Texture2D>(EditorGUIUtility.isProSkin ? "BlueSquareGear" : "BlueSquareGear_Light");
            mBlueGearStyle.margin = new RectOffset(0, 0, 2, 0);
        }

        // Refresh the layers in case they were updated
        EditorHelper.RefreshLayers();

        // Grab the list of motion types
        mMotorTypes.Clear();
        mMotorNames.Clear();

        // CDL 07/03/2018 - this only scans the the assembly containing BoneController
        //// Generate the list of motions to display
        //Assembly lAssembly = Assembly.GetAssembly(typeof(BoneController));
        //foreach (Type lType in lAssembly.GetTypes())
        //{
        //    if (!lType.IsAbstract && lType != typeof(BoneControllerMotor) && typeof(BoneControllerMotor).IsAssignableFrom(lType))
        //    {
        //        mMotorTypes.Add(lType);

        //        string lTypeName = lType.Name;
        //        object[] lAttributes = lType.GetCustomAttributes(typeof(IKNameAttribute), true);
        //        foreach (IKNameAttribute lAttribute in lAttributes) { lTypeName = lAttribute.Name; }

        //        mMotorNames.Add(lTypeName);
        //    }
        //}

        // CDL 07/03/2018 - look in all assemblies for bone motors
        // Generate the list of bone motors to display
        List<Type> lFoundTypes = AssemblyHelper.FoundTypes;
        foreach (Type lType in lFoundTypes)
        {
            if (!lType.IsAbstract && lType != typeof(BoneControllerMotor) && typeof(BoneControllerMotor).IsAssignableFrom(lType))
            {
                mMotorTypes.Add(lType);

                string lTypeName = lType.Name;
                object[] lAttributes = lType.GetCustomAttributes(typeof(IKNameAttribute), true);
                foreach (IKNameAttribute lAttribute in lAttributes) { lTypeName = lAttribute.Name; }

                mMotorNames.Add(lTypeName);
            }
        }

            // If the skeleton editor is active, we'll allow the motors to 
            // run (when in edit mode)
            //for (int i = 0; i < mSkeleton.Motors.Count; i++)
            //{
            //    mSkeleton.Motors[i].IsEditorEnabled = true;
            //}

            mSkeleton.LateUpdate();

        // Hide the skinned mesh. It just gets in the way visually.
        SkinnedMeshRenderer lRenderer = mSkeleton.gameObject.GetComponent<SkinnedMeshRenderer>();
        if (lRenderer == null) { lRenderer = mSkeleton.gameObject.GetComponentInChildren<SkinnedMeshRenderer>(); }

#if (UNITY_4 || UNITY_5_0 || UNITY_5_1 || UNITY_5_2 || UNITY_5_3 || UNITY_5_4)
        EditorUtility.SetSelectedWireframeHidden(lRenderer, true);
#else
        EditorUtility.SetSelectedRenderState(lRenderer, EditorSelectedRenderState.Highlight);
#endif
    }

    /// <summary>
    /// This function is called when the scriptable object goes out of scope.
    /// </summary>
    private void OnDisable()
    {
        // If the skeleton editor is no longer enabled, we don't want
        // the skeleton's motors to run (when in edit mode)
        for (int i = 0; i < mSkeleton.Motors.Count; i++)
        {
            //mSkeleton.Motors[i].IsEditorEnabled = false;
        }

        // Clear the current bone rotations so we go back to the
        // bind pose. 
        //
        // NOTE: This causes the scene to be flagged as dirty
        //mSkeleton.ClearBoneRotations();

        // Force an update
        mSkeleton.LateUpdate();

        // Re-enable the skinned mesh we hid. This test first ensures that the object
        // wasn't deleted. If it wasn't we don't need to do anything.
        if (target != null)
        {
            SkinnedMeshRenderer lRenderer = mSkeleton.gameObject.GetComponent<SkinnedMeshRenderer>();
            if (lRenderer == null) { lRenderer = mSkeleton.gameObject.GetComponentInChildren<SkinnedMeshRenderer>(); }

#if (UNITY_4 || UNITY_5_0 || UNITY_5_1 || UNITY_5_2 || UNITY_5_3 || UNITY_5_4)
            EditorUtility.SetSelectedWireframeHidden(lRenderer, false);
#else
            EditorUtility.SetSelectedRenderState(lRenderer, EditorSelectedRenderState.Hidden);
#endif
        }

        // Clear any selected bones
        SelectBone(null);

        // Clear the hot control so we can select other things
        GUIUtility.hotControl = 0;
    }

    /// <summary>
    /// Called when the inspector needs to draw
    /// </summary>
    public override void OnInspectorGUI()
    {
        // Pulls variables from runtime so we have the latest values.
        mSkeletonSO.Update();

        // Force the inspector and scene to repain if needed
        if (BoneController.EditorForceRepaint)
        {
            Repaint();
            SceneView.RepaintAll();

            BoneController.EditorForceRepaint = false;
        }

        // Grab the position of the editor. Theis is a goofy hack, but works.
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
        //Rect lArea = GUILayoutUtility.GetLastRect();

        // Store the positions
        //float lEditorY = lArea.y;
        //float lEditorWidth = Screen.width - 20f;

        //// We want the BG aligned: Top Center. We'll cut off any piece that is too arge
        //Rect lBGCrop = new Rect(0, 0, mBackground.width, mBackground.height - 110);
        //Vector2 lBGPosition = new Vector2(lEditorWidth - (mBackground.width * 0.8f), lEditorY + 5f);

        //GUILayout.BeginArea(new Rect(0, 0, Screen.width, Screen.height));
        //GUI.BeginGroup(new Rect(lBGPosition.x, lBGPosition.y, lBGCrop.width, lBGCrop.height));
        //GUI.DrawTexture(new Rect(-lBGCrop.x, -lBGCrop.y, mBackground.width, mBackground.height), mBackground);
        //GUI.EndGroup();
        //GUILayout.EndArea();

        // Start putting in the properites
        GUILayout.Space(10);

        GUILayout.BeginHorizontal();

        Transform lNewRootTransform = EditorGUILayout.ObjectField(new GUIContent("Root Transform", "Determines if  this avatar will respond to input from the user."), mSkeleton.RootTransform, typeof(Transform), true) as Transform;
        if (mSkeleton.RootTransform != lNewRootTransform)
        {
            mIsDirty = true;
            mSkeleton.RootTransform = lNewRootTransform;

            // Force an update since we changed the bones
            mSkeleton.LateUpdate();
        }

        if (GUILayout.Button(new GUIContent(), mBlueGearStyle, GUILayout.Width(16), GUILayout.Height(16)))
        {
            mShowSettings = !mShowSettings;
        }

        GUILayout.EndHorizontal();

        if (mShowSettings)
        {
            EditorGUILayout.BeginVertical(GUI.skin.box);

            //bool lNewUsedStandardizedBoneForward = EditorGUILayout.Toggle(new GUIContent("Use standard bone forward", "Determines if we can used standard bone forwards"), mSkeleton.UseStandardizedBoneForward);
            //if (lNewUsedStandardizedBoneForward != mSkeleton.UseStandardizedBoneForward)
            //{
            //    mIsDirty = true;
            //    mSkeleton.UseStandardizedBoneForward = lNewUsedStandardizedBoneForward;
            //}

            //Vector3 lNewBoneForward = EditorGUILayout.Vector3Field(new GUIContent("Bone Forward", "Forward direction bones were created in. Typically, this is (0, 1, 0)."), mSkeleton.BoneForward);
            //if (lNewBoneForward != mSkeleton.BoneForward)
            //{
            //    mIsDirty = true;
            //    mSkeleton.BoneForward = lNewBoneForward;
            //}

            //Vector3 lNewBoneUp = EditorGUILayout.Vector3Field(new GUIContent("Bone Up", "Up direction bones were created in. Typically, this is (0, 0, -1)."), mSkeleton.BoneUp);
            //if (lNewBoneUp != mSkeleton.BoneUp)
            //{
            //    mIsDirty = true;
            //    mSkeleton.BoneUp = lNewBoneUp;
            //}

            //Vector3 lToBoneForward = mSkeleton.ToBoneForward.eulerAngles;
            //Vector3 lNewToBoneForward = EditorGUILayout.Vector3Field(new GUIContent("To Bone Forward", "Rotation that is applied that takes a 'forward' facing quaterion and makes it face along the default bone forward."), lToBoneForward);
            //if (lNewToBoneForward != lToBoneForward)
            //{
            //    mIsDirty = true;
            //    mSkeleton.ToBoneForward.eulerAngles = lNewToBoneForward;
            //}

            //GUILayout.Space(5);

            // Determine if we're to show the joint limits
            bool lNewShowSkeleton = EditorGUILayout.Toggle(new GUIContent("Draw bones", "Show or hide the skeleton."), mSkeleton.EditorShowBones);
            if (lNewShowSkeleton != mSkeleton.EditorShowBones)
            {
                mIsDirty = true;

                BoneController.EditorForceRepaint = true;
                mSkeleton.EditorShowBones = lNewShowSkeleton;
            }

            // Determine if we're to show the joint limits
            bool lNewShowBoneLimits = EditorGUILayout.Toggle(new GUIContent("Draw bone limits", "Show or hide the joint limits for the selected bone."), mSkeleton.EditorShowBoneLimits);
            if (lNewShowBoneLimits != mSkeleton.EditorShowBoneLimits)
            {
                mIsDirty = true;

                BoneController.EditorForceRepaint = true;
                mSkeleton.EditorShowBoneLimits = lNewShowBoneLimits;
            }

            // Determine if we're to show the joint limits
            bool lNewShowBoneColliders = EditorGUILayout.Toggle(new GUIContent("Draw bone colliders", "Show or hide the collider for the selected bone."), mSkeleton.EditorShowBoneColliders);
            if (lNewShowBoneColliders != mSkeleton.EditorShowBoneColliders)
            {
                mIsDirty = true;

                BoneController.EditorForceRepaint = true;
                mSkeleton.EditorShowBoneColliders = lNewShowBoneColliders;
            }

            // Determines if the GUI elements scale with the user position
            bool lNewAutoScaleHandles = EditorGUILayout.Toggle(new GUIContent("Auto scale handles", "Determines if we auto scale the joint handles."), mSkeleton.EditorAutoScaleHandles);
            if (lNewAutoScaleHandles != mSkeleton.EditorAutoScaleHandles)
            {
                mIsDirty = true;
                mSkeleton.EditorAutoScaleHandles = lNewAutoScaleHandles;
            }

            GUILayout.Space(10);
            GUILayout.BeginVertical("Bones", GUI.skin.window);
            EditorGUILayout.HelpBox("Matching bind pose puts the character back to it's original pose." + Environment.NewLine + Environment.NewLine +
                "Reloading bones removes the existing bones and loads the skeleton over. Reloading bones could effect motors that use the bones." + Environment.NewLine + Environment.NewLine +
                "Use Bone Import Filters to ignore bones as we load them. Seperate multiple string values with a pipe (|).", MessageType.None);

            EditorGUILayout.LabelField("Bone Import Filter");
            string lNewEditorBoneFilters = GUILayout.TextArea(mSkeleton.EditorBoneFilters, GUILayout.Height(40));
            if (lNewEditorBoneFilters != mSkeleton.EditorBoneFilters)
            {
                mIsDirty = true;
                mSkeleton.EditorBoneFilters = lNewEditorBoneFilters;
            }

            GUILayout.Space(5);

            if (GUILayout.Button(new GUIContent("Match Bind Pose", "Resets all the bone positions and rotations back to the bind pose.")))
            {
                mSkeleton.ResetBindPose();
            }

            if (GUILayout.Button(new GUIContent("Reload Bones", "Reload all of the bones and thier children.")))
            {
                if (mSkeleton.RootTransform != null)
                {
                    if (EditorUtility.DisplayDialog("Bone Controller", "Reloading the skeleton may require you to update bones and motors. Continue?", "Yes", "No"))
                    {
                        mSkeleton.RootTransform = mSkeleton.RootTransform;
                    }
                }
            }

            GUILayout.EndVertical();

            GUILayout.Space(10);
            GUILayout.BeginVertical("Joints", GUI.skin.window);
            EditorGUILayout.HelpBox("Automatically create joints using the specific style and level of detail. This will not change joints that already exist." + Environment.NewLine + Environment.NewLine + 
                "Removing all joints clears all bones of all joint limits and types. This cannot be undone, but joints can be re-added.", MessageType.None);

            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.LabelField(new GUIContent("Style", "Style of character to build joints for."), GUILayout.Width(40));
            //mJointStyleName = EditorGUILayout.TextField(mJointStyleName, GUILayout.MinWidth(50));
            mJointStyleNameIndex = EditorGUILayout.Popup("", mJointStyleNameIndex, mStyleNames, GUILayout.MinWidth(50));
            EditorGUILayout.LabelField(new GUIContent("Detail", "Level of detail to build joints for."), GUILayout.Width(40));
            mJointDetailLevelIndex = EditorGUILayout.Popup("", mJointDetailLevelIndex, mDetailLevels, GUILayout.Width(60));

            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button(new GUIContent("Set Joints", "Automatically create joints and limits for your character.")))
            {
                mSkeleton.SetBoneJoints(mStyleNames[mJointStyleNameIndex], mJointDetailLevelIndex);
                mIsDirty = true;
            }

            if (GUILayout.Button(new GUIContent("Remove Joints", "Remove all existing joints and limits.")))
            {
                if (EditorUtility.DisplayDialog("Bone Controller", "Remove all joints from the skeleton?", "Yes", "No"))
                {
                    mSkeleton.RemoveBoneJoints(mJointDetailLevelIndex);
                    mIsDirty = true;
                }
            }

            GUILayout.EndVertical();

            GUILayout.Space(10);
            GUILayout.BeginVertical("Colliders", GUI.skin.window);
            EditorGUILayout.HelpBox("Automatically create colliders using the specific style and level of detail. This will not change colliders that already exist." + Environment.NewLine + Environment.NewLine +
                "Removing all colliders clears all bones of the primary collider. This cannot be undone, but colliders can be re-added." + Environment.NewLine + Environment.NewLine +
                "By default we use 'psuedo-colliders' that have a performance improvement and limited interaction with other colliders." + Environment.NewLine + Environment.NewLine +
                "When using true colliders, capsule colliders have odd collision behavior when rotated. Forcing box colliders is safer.", MessageType.None);

            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.LabelField(new GUIContent("Style", "Style of character to build colliders for."), GUILayout.Width(40));
            //mColliderStyleName = EditorGUILayout.TextField(mColliderStyleName, GUILayout.MinWidth(50));
            mColliderStyleNameIndex = EditorGUILayout.Popup("", mColliderStyleNameIndex, mStyleNames, GUILayout.MinWidth(50));
            
            EditorGUILayout.LabelField(new GUIContent("Detail", "Level of detail to build colliders for."), GUILayout.Width(40));
            mColliderDetailLevelIndex = EditorGUILayout.Popup("", mColliderDetailLevelIndex, mDetailLevels, GUILayout.Width(60));

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.LabelField(new GUIContent("Force True Colliders", "Forces the use of true colliders over pseudo colliders."), GUILayout.Width(120));
            mSkeleton.EditorForceTrueColliders = EditorGUILayout.Toggle(mSkeleton.EditorForceTrueColliders, GUILayout.Width(15));

            GUILayout.Space(20);

            EditorGUILayout.LabelField(new GUIContent("Force Box", "Forces the use of true box colliders over true capsule colliders."), GUILayout.Width(60));
            mSkeleton.EditorForceTrueBoxColliders = EditorGUILayout.Toggle(mSkeleton.EditorForceTrueBoxColliders, GUILayout.Width(15));

            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button(new GUIContent("Set Colliders", "Automatically create joints and limits for your character.")))
            {
                mSkeleton.SetBoneColliders(mStyleNames[mColliderStyleNameIndex], mColliderDetailLevelIndex);
                mIsDirty = true;
            }

            if (GUILayout.Button(new GUIContent("Remove Colliders", "Remove all existing colliders.")))
            {
                if (EditorUtility.DisplayDialog("Bone Controller", "Remove all primary colliders from the skeleton?", "Yes", "No"))
                {
                    mSkeleton.RemoveBoneColliders(mColliderDetailLevelIndex);
                    mIsDirty = true;
                }
            }

            GUILayout.EndVertical(); 
            
            EditorGUILayout.EndVertical();
        }

        // Show the Bones
        GUILayout.Space(10);

        EditorGUI.indentLevel++;

        bool lNewShowBones = EditorGUILayout.Foldout(mShowBones, new GUIContent("Show bones and limits"));
        {
            BoneController.EditorForceRepaint = true;
            mShowBones = lNewShowBones;
        }

        EditorGUI.indentLevel--;

        GUILayout.BeginVertical("IK Bones (" + mSkeleton.Bones.Count + ")", GUI.skin.window, GUILayout.Height(10));

        if (mShowBones)
        {
            // Filter by name
            GUILayout.BeginVertical();

            GUILayout.BeginHorizontal(GUI.skin.FindStyle("Toolbar"));
            mBoneSearchString = GUILayout.TextField(mBoneSearchString, GUI.skin.FindStyle("ToolbarSeachTextField"), GUILayout.ExpandWidth(true));
            if (GUILayout.Button("", GUI.skin.FindStyle("ToolbarSeachCancelButton")))
            {
                mBoneSearchString = "";
                GUI.FocusControl(null);
            }
            GUILayout.EndHorizontal();

            // Filter by selected
            bool lNewShowSelectedBones = EditorGUILayout.Toggle(new GUIContent("Only show selected bones", "Show only selected bones in the list."), mSkeleton.EditorShowSelectedBones);
            if (lNewShowSelectedBones != mSkeleton.EditorShowSelectedBones)
            {
                mIsDirty = true;
                mSkeleton.EditorShowSelectedBones = lNewShowSelectedBones;
            }
            
            GUILayout.EndVertical();

            // Render the list
            bool lIsBoneListDirty = RenderBoneList(mBoneSearchString, mSkeleton.EditorShowSelectedBones);
            mIsDirty = mIsDirty || lIsBoneListDirty;
        }
        else
        {
            EditorGUILayout.LabelField("Click arrow above to show bone list...");
        }

        GUILayout.EndVertical();

        // Show the Motors
        GUILayout.Space(10);

        EditorGUI.indentLevel++;

        bool lNewShowMotors = EditorGUILayout.Foldout(mShowMotors, new GUIContent("Show bone motors"));
        if (lNewShowMotors != mShowMotors)
        {
            BoneController.EditorForceRepaint = true;
            mShowMotors = lNewShowMotors;
        }

        EditorGUI.indentLevel--;

        GUILayout.BeginVertical("IK Motors (" + mSkeleton.Motors.Count + ")", GUI.skin.window, GUILayout.Height(10));

        if (mShowMotors)
        {
            bool lIsMotorListDirty = RenderMotorList();
            mIsDirty = mIsDirty || lIsMotorListDirty;

            GUILayout.FlexibleSpace();

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            mSelectedMotorTypeIndex = EditorGUILayout.Popup(mSelectedMotorTypeIndex, mMotorNames.ToArray(), GUILayout.Width(150));

            if (GUILayout.Button(new GUIContent("+", "Add Motor"), EditorStyles.miniButtonLeft, GUILayout.Width(20)))
            {
                mIsDirty = true;
                AddMotor(mMotorTypes[mSelectedMotorTypeIndex]);
            }

            if (GUILayout.Button(new GUIContent("-", "Delete Motor"), EditorStyles.miniButtonRight, GUILayout.Width(20)))
            {
                mIsDirty = true;
                RemoveMotor(mSelectedMotorIndex);

                // Clear any rotations and update. This will clear out any influence
                // from a motor we deleted
                mSkeleton.ResetBindPose();

                // Force an update
                mSkeleton.LateUpdate();
            }

            EditorGUILayout.EndHorizontal();

            GUILayout.EndVertical();

            // Show the layer motions
            GUILayout.Space(10);
            GUILayout.BeginVertical("IK Motor Properties", GUI.skin.window, GUILayout.Height(100));

            bool lIsMotorDirty = RenderMotorProperties(mSelectedMotorIndex);
            mIsDirty = mIsDirty || lIsMotorDirty;
        }
        else
        {
            EditorGUILayout.LabelField("Click arrow above to show motor list...");
        }

        GUILayout.EndVertical();

        // Add some space at the bottom
        GUILayout.Space(10);

        // If there is a change... update.
        if (mIsDirty)
        {
            // Flag the object as needing to be saved
            EditorUtility.SetDirty(mSkeleton);

#if UNITY_4 || UNITY_5_0 || UNITY_5_1 || UNITY_5_2
            EditorApplication.MarkSceneDirty();
#else
            if (!EditorApplication.isPlaying)
            {
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
            }
#endif

            // Pushes the values back to the runtime so it has the changes
            mSkeletonSO.ApplyModifiedProperties();

            // Clear out the dirty flag
            mIsDirty = false;
        }
    }

    /// <summary>
    /// Allows us to render objects in the scene itself. This
    /// is only called when the scene window has focus
    /// </summary>
    private void OnSceneGUI()
    {
        bool lIsDirty = false;

        int lControlID = GUIUtility.GetControlID(FocusType.Passive);

        Event lEvent = Event.current;
        EventType lEventType = Event.current.GetTypeForControl(lControlID);

        // Track the mouse down position so we can test it when the
        // mouse is released
        if (lEventType == EventType.MouseDown && lEvent.button == 0)
        {
            mMouseDownPosition = lEvent.mousePosition;
        }
        // Test if a valid mouse up event has occurred
        else if (lEventType == EventType.MouseUp && lEvent.button == 0)
        {
            // Only process if the mouse didn't move
            if (mMouseDownPosition == lEvent.mousePosition)
            {
                if (GUIUtility.hotControl == lControlID)
                {
                    GUIUtility.hotControl = 0;
                    lEvent.Use();
                }

                // Test if we hit a bone
                int lHitIndex = RaycastForBone(lEvent.mousePosition, true);
                if (lHitIndex >= 0)
                {
                    BoneControllerBone lBone = mSkeleton.Bones[lHitIndex];
                    SelectBone(lBone);

                    // The only way to really get the SceneView to repaint is to
                    // force the dirty flag on the skeleton. It sucks, but...
                    BoneController.EditorForceRepaint = true;
                }
            }
        }

        // Render out the skeleton as handles
        if (lEventType == EventType.Repaint && mSkeleton.EditorShowBones)
        {
            HandlesHelper.DrawSkeleton(mSkeleton, BoneControllerEditor.SkeletonColor, BoneControllerEditor.SkeletonColliderColor);
        }

        // Allow the joints to render to the scene
        if ((mSkeleton.EditorShowBoneLimits || mShowBones) && mSelectedBones.Count > 0)
        {
            for (int i = 0; i < mSelectedBones.Count; i++)
            {
                bool lIsBoneDirty = mSelectedBones[i].OnSceneGUI(true);
                if (lIsBoneDirty) { lIsDirty = true; }
            }
        }

        // Allow the motors to render to the scene or edit scene objects
        if (mShowMotors && mSelectedMotorIndex >= 0 && mSelectedMotorIndex < mSkeleton.Motors.Count)
        {
            bool lIsMotorDirty = mSkeleton.Motors[mSelectedMotorIndex].OnSceneGUI(mSelectedBones);
            if (lIsMotorDirty) { lIsDirty = true; }
        }

        // Render out the selected bones
        for (int i = 0; i < mSelectedBones.Count; i++)
        {
            HandlesHelper.DrawTransform(mSelectedBones[i].Transform.position, mSelectedBones[i].Transform.rotation * mSelectedBones[i].ToBoneForward, 1f, true, 0.8f);
            HandlesHelper.DrawTransform(mSelectedBones[i].Transform.position, mSelectedBones[i].Transform.rotation, 0.25f, true, 1.2f);
            HandlesHelper.DrawBone(mSelectedBones[i], BoneControllerEditor.SkeletonSelectedColor);
        }

        // If we had a major change, repaint the inspector and then
        // force the SceneView to redraw. Unfortunatley, the only way to consistantly
        // do this is to set the dirty flag of the main object.
        if (lIsDirty)
        {
            mIsDirty = true;
        }
    }

    /// <summary>
    /// Renders out a list of bones that belong to the skeleton
    /// </summary>
    private bool RenderBoneList(string rNameFilter, bool rSelectedFilter)
    {
        // Cycle through the motions
        BoneControllerBone lBone = mSkeleton.Root;

        if (lBone == null)
        {
            EditorGUILayout.HelpBox("Select a root transform above that will represent the root of your skeleton. Typically this is the 'hips'.", MessageType.Info);
            return false;
        }
        else
        {
            return RenderBone(lBone, 0, rNameFilter, rSelectedFilter);
        }
    }

    /// <summary>
    /// Renders out a single bone that belongs to the skeleton
    /// </summary>
    /// <param name="rBone"></param>
    private bool RenderBone(BoneControllerBone rBone, int rSpacing, string rNameFilter, bool rSelectedFilter)
    {
        bool lIsDirty = false;

        rNameFilter = rNameFilter.ToLower();
        string lBoneName = rBone.Name.ToLower();
        if (rNameFilter.Length == 0 || 
            lBoneName.Length == 0 || 
            lBoneName.IndexOf(rNameFilter) > -1 || 
            lBoneName.IndexOf(rNameFilter.Replace(" ", "")) > -1)
        {
            bool lIsSelected = mSelectedBone == rBone;
            if (lIsSelected || !rSelectedFilter)
            {
                EditorGUILayout.BeginVertical(GUI.skin.box);

                // If this bone is selected, we'll highlight the title
                GUIStyle lRowStyle = (lIsSelected ? mSelectedRowStyle : mTitleRowStyle);
                EditorGUILayout.BeginHorizontal(lRowStyle, GUILayout.Height(22));

                if (GUILayout.Button(new GUIContent(mItemSelector), GUI.skin.label, GUILayout.Width(16)))
                {
                    SelectBone(rBone);
                }

                string lParentName = (rBone.Parent == null ? " (root)" : " (from " + rBone.Parent.CleanName + ")");
                EditorGUILayout.LabelField(rBone.CleanName + lParentName);

                if (GUILayout.Button(new GUIContent("", "Add a child bone"), mBluePlusStyle, GUILayout.Width(16), GUILayout.Height(16)))
                {
                    AddBone(rBone);
                }

                if (mSkeleton.Root != rBone)
                {
                    GUILayout.Space(2);

                    if (GUILayout.Button(new GUIContent("", "Remove this bone"), mRedXStyle, GUILayout.Width(16), GUILayout.Height(16)))
                    {
                        if (EditorUtility.DisplayDialog("Bone Controller", "Remove this bone (and its children) from the motor?", "Yes", "No"))
                        {
                            RemoveBone(rBone);
                        }
                    }
                }

                GUILayout.Space(2);

                EditorGUILayout.EndHorizontal();

                // Allow the bone to render itself
                bool lIsBoneInspectorDirty = rBone.OnInspectorGUI(lIsSelected);
                lIsDirty = lIsDirty || lIsBoneInspectorDirty;

                GUILayout.Space(2);

                EditorGUILayout.EndVertical();

                GUILayout.Space(7);
            }
        }

        // Render the children
        for (int i = 0; i < rBone.Children.Count; i++)
        {
            bool lIsBoneDirty = RenderBone(rBone.Children[i], rSpacing + 5, rNameFilter, rSelectedFilter);
            lIsDirty = lIsDirty || lIsBoneDirty;
        }

        return lIsDirty;
    }

    /// <summary>
    /// Renders the motions for the specified list's index
    /// </summary>
    /// <param name="rName"></param>
    /// <param name="rLayerIndex"></param>
    private bool RenderMotorList()
    {
        bool lIsDirty = false;

        // If we don't have items in the list, display some help
        if (mSkeleton.Motors.Count == 0)
        {
            EditorGUILayout.HelpBox(mMotorHelp, MessageType.Info, true);
            return false;
        }

        // Add a row for titles
        EditorGUILayout.BeginHorizontal();
        GUILayout.Space(24);
        EditorGUILayout.LabelField("Type", GUILayout.MinWidth(50));
        EditorGUILayout.LabelField("Name", GUILayout.MinWidth(50));
        EditorGUILayout.LabelField("Ena", GUILayout.Width(24));
        EditorGUILayout.LabelField("", GUILayout.Width(2));
        EditorGUILayout.EndHorizontal();

        // Cycle through the motions
        for (int i = 0; i < mSkeleton.Motors.Count; i++)
        {
            BoneControllerMotor lMotor = mSkeleton.Motors[i];
            if (lMotor == null) { continue; }

            GUIStyle lRowStyle = (mSelectedMotorIndex == i ? mSelectedRowStyle : mRowStyle);
            EditorGUILayout.BeginHorizontal(lRowStyle);

            if (GUILayout.Button(new GUIContent(mItemSelector), GUI.skin.label, GUILayout.Width(16)))
            {
                mSelectedMotorIndex = i;
            }

            Type lMotorType = lMotor.GetType();

            string lMotorTypeName = lMotorType.Name;
            object[] lAttributes = lMotorType.GetCustomAttributes(typeof(IKNameAttribute), true);
            foreach (IKNameAttribute lAttribute in lAttributes) { lMotorTypeName = lAttribute.Name; }

            EditorGUILayout.LabelField(lMotorTypeName, GUILayout.MinWidth(50));

            string lMotorName = EditorGUILayout.TextField(lMotor.Name, GUILayout.MinWidth(50));
            if (lMotorName != lMotor.Name)
            {
                lMotor.Name = lMotorName;
                lIsDirty = true;
            }

            bool lMotorIsEnabled = EditorGUILayout.Toggle(lMotor.IsEnabled, GUILayout.Width(20));
            if (lMotorIsEnabled != lMotor.IsEnabled)
            {
                lMotor.IsEnabled = lMotorIsEnabled;
                lIsDirty = true;
            }

            EditorGUILayout.EndHorizontal();
        }

        return lIsDirty;
    }

    /// <summary>
    /// Renders the properties of the motion so they can be changed here
    /// </summary>
    /// <param name="rLayerIndex">Layer the motion belongs to</param>
    /// <param name="rMotorIndex">Motors whose properites are to be listed</param>
    private bool RenderMotorProperties(int rMotorIndex)
    {
        bool lExit = false;
        if (!lExit && rMotorIndex < 0) { lExit = true; }
        if (!lExit && rMotorIndex >= mSkeleton.Motors.Count) { lExit = true; }

        // If we don't have items in the list, display some help
        if (lExit)
        {
            EditorGUILayout.HelpBox(mPropertyHelp, MessageType.Info, true);
            return false;
        }

        // Tracks if we change the motion values
        bool lIsDirty = false;

        // Grab the motion
        BoneControllerMotor lMotor = mSkeleton.Motors[rMotorIndex];
        if (lMotor == null) { return false; }

        object[] lMotorAttributes = lMotor.GetType().GetCustomAttributes(typeof(IKDescriptionAttribute), true);
        foreach (IKDescriptionAttribute lAttribute in lMotorAttributes)
        {
            EditorGUILayout.HelpBox(lAttribute.Description, MessageType.None, true);
        }

        EditorGUILayout.LabelField(new GUIContent("Type", "Identifies the type of motor."), new GUIContent(lMotor.GetType().Name));
        EditorGUILayout.LabelField(new GUIContent("Namespace", "Specifies the container the motor belongs to."), new GUIContent(lMotor.GetType().Namespace));

        // Force the name at the top
        string lNewName = EditorGUILayout.TextField(new GUIContent("Name", "Friendly name of the motor that can be searched for."), lMotor.Name);
        if (lNewName != lMotor.Name)
        {
            lIsDirty = true;
            lMotor.Name = lNewName;
        }

        float lNewWeight = EditorGUILayout.FloatField(new GUIContent("Weight", "Determines how much strength this motor has relative to the other motors (0 to 1)."), lMotor.Weight);
        if (lNewWeight != lMotor.Weight)
        {
            lIsDirty = true;
            lMotor.Weight = lNewWeight;
        }

        EditorGUILayout.BeginHorizontal();

        // Determines if the motor will run
        bool lNewIsEnabled = EditorGUILayout.Toggle(new GUIContent("Is Enabled", "Determines if the motor will run or not."), lMotor.IsEnabled);
        if (lNewIsEnabled != lMotor.IsEnabled)
        {
            lIsDirty = true;
            lMotor._IsEnabled = lNewIsEnabled;
        }

        // Determines if we'll run in the scene editor
        GUILayout.Space(20);
        EditorGUILayout.LabelField(new GUIContent("Run In Editor", "Determines if the motor runs in the scene editor."), GUILayout.Width(80));
        bool lNewIsEditorEnabled = EditorGUILayout.Toggle(lMotor.IsEditorEnabled, GUILayout.Width(16));
        if (lNewIsEditorEnabled != lMotor.IsEditorEnabled)
        {
            lIsDirty = true;
            lMotor.IsEditorEnabled = lNewIsEditorEnabled;
        }

        GUILayout.FlexibleSpace();

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();

        // Determines if we'll run in the scene editor
        bool lNewIsFixedUpdateEnabled = EditorGUILayout.Toggle(new GUIContent("Use Fixed Update", "Determines if we attempt to keep a stable update schedule. This is useful for physics simulations."), lMotor.IsFixedUpdateEnabled);
        if (lNewIsFixedUpdateEnabled != lMotor.IsFixedUpdateEnabled)
        {
            lIsDirty = true;
            lMotor.IsFixedUpdateEnabled = lNewIsFixedUpdateEnabled;
        }

        GUILayout.Space(20);
        EditorGUILayout.LabelField(new GUIContent("FPS", "Determines the frame rate we're targeting for fixed updates."), GUILayout.Width(30));
        int lNewFixedUpdateFPS = EditorGUILayout.IntField((int)lMotor.FixedUpdateFPS, GUILayout.Width(65));
        if (lNewFixedUpdateFPS != lMotor.FixedUpdateFPS)
        {
            lIsDirty = true;
            lMotor.FixedUpdateFPS = lNewFixedUpdateFPS;
        }

        GUILayout.FlexibleSpace();

        EditorGUILayout.EndHorizontal();

        // Determines if we'll run in the scene editor
        bool lNewShowDebug = EditorGUILayout.Toggle(new GUIContent("Is Debug Enabled", "Determines if we show debug information while running."), lMotor.IsDebugEnabled);
        if (lNewShowDebug != lMotor.IsDebugEnabled)
        {
            lIsDirty = true;
            lMotor.IsDebugEnabled = lNewShowDebug;
        }

        // Determine if we use the custom inspectior. We pass in the selected bones
        // and they may be edited by the motor's inspector
        bool lIsMotorDirty = lMotor.OnInspectorGUI(mSelectedBones);
        lIsDirty = lIsDirty || lIsMotorDirty;

        // Ensure we still have the right selected bones
        if (mSelectedBones.Count > 0 && mSelectedBone != mSelectedBones[0])
        {
            mSelectedBone = mSelectedBones[0];
        }

        return lIsDirty;
    }
    
    /// <summary>
    /// Adds a new motion
    /// </summary>
    /// <param name="rLayerIndex">Layer index to add the motion to</param>
    /// <returns>Index of the new motion</returns>
    private void AddMotor(Type rMotorType)
    {
        BoneControllerMotor lMotor = Activator.CreateInstance(rMotorType) as BoneControllerMotor;
        //BoneControllerMotor lMotor = ScriptableObject.CreateInstance(rMotorType) as BoneControllerMotor;

        lMotor.Skeleton = mSkeleton;
        mSkeleton.Motors.Add(lMotor);

        mSelectedMotorIndex = mSkeleton.Motors.Count - 1;
    }

    /// <summary>
    /// Removes the specified motion
    /// </summary>
    /// <param name="rLayerIndex">Index of the layer the motion belongs to</param>
    /// <param name="rMotorIndex">Index of the motion to remove</param>
    private void RemoveMotor(int rMotorIndex)
    {
        if (rMotorIndex < 0) { return; }
        if (rMotorIndex >= mSkeleton.Motors.Count) { return; }

        mSkeleton.Motors.RemoveAt(rMotorIndex);
        if (mSelectedMotorIndex >= mSkeleton.Motors.Count) { mSelectedMotorIndex = mSkeleton.Motors.Count - 1; }
    }

    /// <summary>
    /// Adds a bone placeholder to the list of bones for the skeleton.
    /// </summary>
    /// <param name="rParent"></param>
    private void AddBone(BoneControllerBone rParent)
    {
        mSkeleton.AddBone(rParent);
    }

    /// <summary>
    /// Removes a bone (and it's children) from the skeleton
    /// </summary>
    /// <param name="rBone"></param>
    private void RemoveBone(BoneControllerBone rBone)
    {
        mSkeleton.RemoveBone(rBone);
    }

    /// <summary>
    /// Selects the new bone and deselects the old one
    /// </summary>
    /// <param name="rBone"></param>
    private void SelectBone(BoneControllerBone rBone)
    {
        // Flag the old bone as deselected
        if (mSelectedBone != null) { mSelectedBone.OnDisable(); }

        // Flag the new bone as selected
        if (rBone == null)
        {
            mSelectedBones.Clear();
        }
        else if (mSelectedBones.Count > 0)
        {
            mSelectedBones[0] = rBone;
        }
        else
        {
            mSelectedBones.Add(rBone);
        }

        mSelectedBone = rBone;
        if (mSelectedBone != null) { mSelectedBone.OnEnable(); }
    }

    /// <summary>
    /// Shoots a ray from the mouse point to determine if the object
    /// under it is a bone. If so, we return the bone index.
    /// </summary>
    /// <param name="rMousePosition">Vector reprsenting the screen position of the mouse</param>
    /// <returns>Index of the bone that the mouse is over or -1 if it's not</returns>
    private int RaycastForBone(Vector2 rMousePosition, bool rUseTruePositions)
    {
        int lHitIndex = -1;
        float lHitDistance = float.MaxValue;

        Ray lRay = HandleUtility.GUIPointToWorldRay(rMousePosition);
        for (float lLength = 0f; lLength < 5f; lLength += 0.01f)
        {
            Vector3 lRayPoint = SceneView.currentDrawingSceneView.camera.transform.position + (lRay.direction * lLength);

            for (int i = 0; i < mSkeleton.Bones.Count; i++)
            {
                BoneControllerBone lBone = mSkeleton.Bones[i];
                if (lBone == null || lBone.Transform == null) { continue; }

                Quaternion lBoneRotation = lBone.Transform.rotation;
                float lBoneLength = lBone.Length;
                float lBoneWidth = 0.1f * lBoneLength;

                // We can use the actual bone positions and length
                if (rUseTruePositions)
                {
                    // While we have the transform rotation, we need to get the bone forward rotation
                    lBoneRotation = lBoneRotation * Quaternion.FromToRotation(Vector3.up, lBone.BoneForward);

                    // Create a matrix to transform the click point and make it relative to the visual bone
                    Matrix4x4 lBoneMatrix = Matrix4x4.TRS(lBone.Transform.position, lBoneRotation, Vector3.one).inverse;

                    // Simple distance check to the root of the bone
                    //float lDistance = Vector3.Distance(lBone.Transform.position, lRayPoint);

                    // Get the local point relative to the visual bone and test it
                    Vector3 lLocalPoint = lBoneMatrix.MultiplyPoint(lRayPoint);
                    if (Mathf.Abs(lLocalPoint.x) < lBoneWidth)
                    {
                        if (Mathf.Abs(lLocalPoint.z) < lBoneWidth)
                        {
                            if (lLocalPoint.y > 0 && lLocalPoint.y < lBoneLength)
                            {
                                lHitIndex = i;
                                lHitDistance = 0.025f;
                            }
                        }
                    }
                }
                // Or we can use more visually appealing approach that connects the bones. 
                // However, it doesn't always represent the true skeleton
                else
                {
                    // Here's where it gets a bit tricky. The bones we visualize aren't actually
                    // rotated the way the true bones are. We simply visualize it this way so that
                    // it makes more sense to the user. That means we need to test the click against
                    // the visual bone and not the true one. 
                    //
                    // For each child, we have a visual bone. Process each of them.
                    int lChildCount = Mathf.Max(lBone.Children.Count, 1);
                    for (int j = 0; j < lChildCount; j++)
                    {
                        BoneControllerBone lChildBone = null;

                        // If we're not at the root bone, we can determine
                        // the visual rotation and length based on it's child.
                        if (lBone.Children.Count > j)
                        {
                            lChildBone = lBone.Children[j];
                            lBoneRotation = Quaternion.FromToRotation(Vector3.up, lChildBone.Transform.position - lBone.Transform.position);
                            lBoneLength = Vector3.Distance(lBone.Transform.position, lChildBone.Transform.position);
                            lBoneWidth = 0.1f * lBoneLength;
                        }

                        // Create a matrix to transform the click point and make it relative to the visual bone
                        Matrix4x4 lBoneMatrix = Matrix4x4.TRS(lBone.Transform.position, lBoneRotation, Vector3.one).inverse;

                        // Simple distance check to the root of the bone
                        float lDistance = Vector3.Distance(lBone.Transform.position, lRayPoint);
                        if (lDistance < 0.025f)
                        {
                            if (lDistance < lHitDistance)
                            {
                                lHitIndex = i;
                                lHitDistance = lDistance;
                            }
                        }
                        // Do a more complex check looking at the bounds
                        else
                        {
                            // Get the local point relative to the visual bone and test it
                            Vector3 lLocalPoint = lBoneMatrix.MultiplyPoint(lRayPoint);
                            if (Mathf.Abs(lLocalPoint.x) < lBoneWidth)
                            {
                                if (Mathf.Abs(lLocalPoint.z) < lBoneWidth)
                                {
                                    if (lLocalPoint.y > 0 && lLocalPoint.y < lBoneLength)
                                    {
                                        lHitIndex = i;
                                        lHitDistance = 0.025f;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            // If we have a hit, we are done
            if (lHitIndex >= 0) { break; }
        }

        return lHitIndex;
    }
}
