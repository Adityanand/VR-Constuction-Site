using UnityEngine;
using com.ootii.Actors.BoneControllers;
using com.ootii.Geometry;
using com.ootii.Utilities.Debug;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace com.ootii.Helpers
{
    /// <summary>
    /// Unity provides the 'Handles' class for creating GUI
    /// handles to help manipulate objects. This class supports
    /// some custom handles.
    /// </summary>
    public class HandlesHelper
    {
        /// <summary>
        /// Keep the handles a little smaller than Unity's defaults
        /// </summary>
        public static float HandleScale = 1.0f;

        public static float Scale = 0.15f;

        public static Color InactiveColor = new Color(0.8f, 0.8f, 0.8f, 0.5f);

        public static float LastFloat = 0f;

        public static Vector3[] Vectors = new Vector3[4];

        public static Quaternion StartRotation = Quaternion.identity;

#if UNITY_EDITOR

        public static Vector3[] DrawnBones = new Vector3[6];

#endif

        /// <summary>
        /// Handle used to swing the bone using GUI handles
        /// </summary>
        /// <param name="rBone">Bone that is being rotated</param>
        /// <param name="rSwing">Local space swing</param>
        /// <returns></returns>
        public static bool JointSwingHandle(BoneControllerBone rBone, IKBoneModifier rModifier)
        {
            bool lIsDirty = false;

#if UNITY_EDITOR

            Vector3 lWorldPosition = rBone._Transform.position;
            Quaternion lWorldSwing = rBone.WorldBindRotation * rModifier.Swing;

            Color lHandleColor = Handles.color;
            float lHandleSnapSettingsRotation = 1f;

            float lHandleScale = 0.1f;
            if (rBone.Skeleton.EditorAutoScaleHandles) { lHandleScale = HandleUtility.GetHandleSize(lWorldPosition) * HandlesHelper.HandleScale; }

            // Track the rotation when we start using the handle.
            Event lEvent = Event.current;
            switch (lEvent.type)
            {
                case EventType.MouseDown:
                    StartRotation = lWorldSwing;
                    break;
            }

            Quaternion lOldValue = StartRotation;
            Quaternion lNewValue = Quaternion.identity;

            // PITCH
            Handles.color = InactiveColor;
            Handles.DrawWireArc(lWorldPosition, lWorldSwing * Vector3.right, lWorldSwing * Vector3.up, 360f, lHandleScale);

            Handles.color = Handles.xAxisColor;
            lNewValue = Handles.Disc(lOldValue, lWorldPosition, lWorldSwing * Vector3.right, lHandleScale, true, lHandleSnapSettingsRotation);
            if (lNewValue != lOldValue)
            {
                rModifier.Swing = Quaternion.Inverse(rBone.WorldBindRotation) * lNewValue;

                lIsDirty = true;
                lOldValue = lNewValue;
            }

            // YAW
            Handles.color = InactiveColor;
            Handles.DrawWireArc(lWorldPosition, lWorldSwing * Vector3.up, lWorldSwing * Vector3.forward, 360f, lHandleScale);

            Handles.color = Handles.yAxisColor;
            lNewValue = Handles.Disc(lOldValue, lWorldPosition, lWorldSwing * Vector3.up, lHandleScale, true, lHandleSnapSettingsRotation);
            if (lNewValue != lOldValue)
            {
                rModifier.Swing = Quaternion.Inverse(rBone.WorldBindRotation) * lNewValue;

                lIsDirty = true;
                lOldValue = lNewValue;
            }

            // Reset
            Handles.color = lHandleColor;

#endif

            return lIsDirty;
        }

        /// <summary>
        /// Handle used to swing the bone using GUI handles
        /// </summary>
        /// <param name="rBone">Bone that is being rotated</param>
        /// <param name="rSwing">Local space swing</param>
        /// <returns></returns>
        public static bool JointSwingHandle(BoneControllerBone rBone, ref Quaternion rSwing)
        {
            bool lIsDirty = false;

#if UNITY_EDITOR

            Vector3 lWorldPosition = rBone._Transform.position;
            Quaternion lWorldSwing = rBone.WorldBindRotation * rSwing;

            Color lHandleColor = Handles.color;
            float lHandleSnapSettingsRotation = 1f;

            float lHandleScale = 0.1f;
            if (rBone.Skeleton.EditorAutoScaleHandles) { lHandleScale = HandleUtility.GetHandleSize(lWorldPosition) * HandlesHelper.HandleScale; }

            // Track the rotation when we start using the handle.
            Event lEvent = Event.current;
            switch (lEvent.type)
            {
                case EventType.MouseDown:
                    StartRotation = lWorldSwing;
                    break;
            }

            Quaternion lOldValue = StartRotation;
            Quaternion lNewValue = Quaternion.identity;

            // PITCH
            Handles.color = InactiveColor;
            Handles.DrawWireArc(lWorldPosition, lWorldSwing * Vector3.right, lWorldSwing * Vector3.up, 360f, lHandleScale);

            Handles.color = Handles.xAxisColor;
            lNewValue = Handles.Disc(lOldValue, lWorldPosition, lWorldSwing * Vector3.right, lHandleScale, true, lHandleSnapSettingsRotation);
            if (lNewValue != lOldValue)
            {
                rSwing = Quaternion.Inverse(rBone.WorldBindRotation) * lNewValue;

                lIsDirty = true;
                lOldValue = lNewValue;
            }

            // YAW
            Handles.color = InactiveColor;
            Handles.DrawWireArc(lWorldPosition, lWorldSwing * Vector3.up, lWorldSwing * Vector3.forward, 360f, lHandleScale);

            Handles.color = Handles.yAxisColor;
            lNewValue = Handles.Disc(lOldValue, lWorldPosition, lWorldSwing * Vector3.up, lHandleScale, true, lHandleSnapSettingsRotation);
            if (lNewValue != lOldValue)
            {
                rSwing = Quaternion.Inverse(rBone.WorldBindRotation) * lNewValue;

                lIsDirty = true;
                lOldValue = lNewValue;
            }

            // Reset
            Handles.color = lHandleColor;

#endif

            return lIsDirty;
        }

        /// <summary>
        /// Handle used to twist the bone
        /// 
        /// Note: We return "Unity Forward" values, not "Bone Forward" ones
        /// </summary>
        /// <returns></returns>
        public static bool JointTwistHandle(BoneControllerBone rBone, IKBoneModifier rModifier)
        {
            bool lIsDirty = false;

#if UNITY_EDITOR

            if (rBone != null)
            {
                Vector3 lWorldPosition = rBone._Transform.position;
                Quaternion lWorldSwing = rBone.WorldBindRotation * rModifier.Swing;

                Color lHandleColor = Handles.color;
                float lHandleSnapSettingsRotation = 1f;

                float lHandleScale = 0.1f;
                if (rBone.Skeleton.EditorAutoScaleHandles) { lHandleScale = HandleUtility.GetHandleSize(lWorldPosition) * HandlesHelper.HandleScale; }

                // Track the rotation when we start using the handle.
                Event lEvent = Event.current;
                switch (lEvent.type)
                {
                    case EventType.MouseDown:
                        StartRotation = rModifier.Twist;
                        break;
                }

                // ROLL
                Handles.color = InactiveColor;
                Handles.DrawWireArc(lWorldPosition, lWorldSwing * Vector3.forward, lWorldSwing * Vector3.right, 360f, lHandleScale);

                Handles.color = Handles.zAxisColor;
                Quaternion lRotation = Quaternion.identity;
                Quaternion lNewRotation = Handles.Disc(lRotation, lWorldPosition, lWorldSwing * Vector3.forward, lHandleScale, true, lHandleSnapSettingsRotation);
                if (lNewRotation != lRotation)
                {
                    float lTwistAngle = 0f;
                    Vector3 lTwistAxis = Vector3.zero;
                    lNewRotation.ToAngleAxis(out lTwistAngle, out lTwistAxis);

                    // When the rotation exceeds 360 degress, our angle starts to
                    // reverse its trending. We can fix that here.
                    float lTwistInvertTest = Vector3.Angle(lWorldSwing * Vector3.forward, lTwistAxis);
                    float lAngle = (Mathf.Abs(lTwistInvertTest) < 1f ? 1 : -1) * lTwistAngle;

                    // We return Unity Forward values
                    rModifier.Twist = StartRotation * Quaternion.AngleAxis(lAngle, Vector3.forward);

                    // Flag the skeleton as dirty
                    lIsDirty = true;
                }

                // Reset
                Handles.color = lHandleColor;
            }

#endif

            return lIsDirty;
        }

        /// <summary>
        /// Handle used to twist the bone
        /// </summary>
        /// <returns></returns>
        public static bool JointTwistHandle(BoneControllerBone rBone, ref Quaternion rTwist)
        {
            bool lIsDirty = false;

#if UNITY_EDITOR

            if (rBone != null)
            {
                Vector3 lWorldPosition = rBone._Transform.position;
                Quaternion lWorldSwing = rBone.WorldBindRotation * rBone.Swing;

                Color lHandleColor = Handles.color;
                float lHandleSnapSettingsRotation = 1f;

                float lHandleScale = 0.1f;
                if (rBone.Skeleton.EditorAutoScaleHandles) { lHandleScale = HandleUtility.GetHandleSize(lWorldPosition) * HandlesHelper.HandleScale; }

                // Track the rotation when we start using the handle.
                Event lEvent = Event.current;
                switch (lEvent.type)
                {
                    case EventType.MouseDown:
                        StartRotation = rTwist;
                        break;
                }

                // ROLL
                Handles.color = InactiveColor;
                Handles.DrawWireArc(lWorldPosition, lWorldSwing * Vector3.forward, lWorldSwing * Vector3.right, 360f, lHandleScale);

                Handles.color = Handles.zAxisColor;
                Quaternion lRotation = Quaternion.identity;
                Quaternion lNewRotation = Handles.Disc(lRotation, lWorldPosition, lWorldSwing * Vector3.forward, lHandleScale, true, lHandleSnapSettingsRotation);
                if (lNewRotation != lRotation)
                {
                    float lTwistAngle = 0f;
                    Vector3 lTwistAxis = Vector3.zero;
                    lNewRotation.ToAngleAxis(out lTwistAngle, out lTwistAxis);

                    // When the rotation exceeds 360 degress, our angle starts to
                    // reverse its trending. We can fix that here.
                    float lTwistInvertTest = Vector3.Angle(lWorldSwing * Vector3.forward, lTwistAxis);
                    float lAngle = (Mathf.Abs(lTwistInvertTest) < 1f ? 1 : -1) * lTwistAngle;

                    // We return Unity Forward values
                    rTwist = StartRotation * Quaternion.AngleAxis(lAngle, Vector3.forward);

                    // Flag the skeleton as dirty
                    lIsDirty = true;
                }

                // Reset
                Handles.color = lHandleColor;
            }

#endif

            return lIsDirty;
        }

        /// <summary>
        /// Handle used to twist the bone
        /// </summary>
        /// <returns></returns>
        public static bool JointSwingAxisHandle(BoneControllerBone rBone, Vector3 rAxis, ref Quaternion rSwing)
        {
            bool lIsDirty = false;

#if UNITY_EDITOR

            Quaternion lWorldBind = rBone.WorldBindRotation;

            Vector3 lWorldPosition = rBone._Transform.position;
            Quaternion lWorldSwing = rBone.WorldBindRotation * rSwing;

            Vector3 lWorldAxis = lWorldBind * rAxis;

            Color lHandleColor = Handles.color;
            float lHandleSnapSettingsRotation = 1f;

            float lHandleScale = 0.1f;
            if (rBone.Skeleton.EditorAutoScaleHandles) { lHandleScale = HandleUtility.GetHandleSize(rBone.Transform.position) * HandlesHelper.HandleScale; }

            // Render the backs
            Handles.color = InactiveColor;
            //Handles.DrawWireArc(lWorldPosition, lWorldSwing * Vector3.forward, lWorldSwing * -Vector3.up, 360f, lHandleSize);
            //Handles.DrawWireArc(lWorldPosition, lWorldSwing * Vector3.right, lWorldSwing * -Vector3.up, 360f, lHandleSize);

            // Track the rotation when we start using the handle.
            Event lEvent = Event.current;
            switch (lEvent.type)
            {
                case EventType.MouseDown:
                    StartRotation = lWorldSwing;
                    break;
            }

            // Render the rotation handle for the z axis
            Handles.color = new Color(0.94118f, 0.39608f, 0.13333f, 1f);
            Quaternion lNewRotationZ = Handles.Disc(StartRotation, lWorldPosition, lWorldAxis, lHandleScale, false, lHandleSnapSettingsRotation);
            if (lNewRotationZ != StartRotation)
            {
                rSwing = rBone.TransformWorldRotationToLocalRotation(lNewRotationZ);
                lIsDirty = true;
            }

            // Reset
            Handles.color = lHandleColor;

#endif

            return lIsDirty;
        }

        /// <summary>
        /// Handle used to twist the bone
        /// </summary>
        /// <returns></returns>
        public static bool JointSwingAxisHandle(BoneControllerBone rBone, Vector3 rAxis, IKBoneModifier rModifier)
        {
            bool lIsDirty = false;

#if UNITY_EDITOR

            Vector3 lWorldPosition = rBone._Transform.position;
            Quaternion lWorldSwing = rBone.WorldBindRotation * rModifier.Swing;

            Vector3 lWorldAxis = rBone.WorldBindRotation * rAxis;

            Color lHandleColor = Handles.color;
            float lHandleSnapSettingsRotation = 1f;

            float lHandleScale = 0.1f;
            if (rBone.Skeleton.EditorAutoScaleHandles) { lHandleScale = HandleUtility.GetHandleSize(lWorldPosition) * HandlesHelper.HandleScale; }

            // Track the rotation when we start using the handle.
            Event lEvent = Event.current;
            switch (lEvent.type)
            {
                case EventType.MouseDown:
                    StartRotation = lWorldSwing;
                    break;
            }

            Quaternion lOldValue = StartRotation;
            Quaternion lNewValue = Quaternion.identity;

            // AXIS
            Handles.color = new Color(0.94118f, 0.39608f, 0.13333f, 1f);
            lNewValue = Handles.Disc(lOldValue, lWorldPosition, lWorldAxis, lHandleScale, false, lHandleSnapSettingsRotation);
            if (lNewValue != lOldValue)
            {
                rModifier.Swing = Quaternion.Inverse(rBone.WorldBindRotation) * lNewValue;

                lIsDirty = true;
                lOldValue = lNewValue;
            }

            // Reset
            Handles.color = lHandleColor;

#endif

            return lIsDirty;
        }

        /// <summary>
        /// This function renders out the handles that allow us to edit the twist limits. It
        /// isn't actually meant to change the twist itself
        /// </summary>
        /// <param name="rBone"></param>
        /// <param name="rMinAngle"></param>
        /// <param name="rMaxAngle"></param>
        /// <returns></returns>
        public static bool JointSwingAxisLimitsHandle(BoneControllerBone rBone, Vector3 rAxis, float rMinAngle, float rMaxAngle)
        {
            bool lIsDirty = false;

#if UNITY_EDITOR

            Vector3 lWorldPosition = rBone._Transform.position;
            Quaternion lWorldSwing = rBone.WorldBindRotation * rBone.Swing;

            Color lGUIColor = GUI.color;
            Color lHandleColor = Handles.color;

            float lHandleScale = 0.2f;
            if (rBone.Skeleton.EditorAutoScaleHandles) { lHandleScale = HandleUtility.GetHandleSize(rBone.Transform.position) * HandlesHelper.HandleScale; }

            // Render the border of the angles
            Quaternion lMinRotation = Quaternion.AngleAxis(rMinAngle, rAxis);
            Quaternion lMaxRotation = Quaternion.AngleAxis(rMaxAngle, rAxis);

            // We don't use the world swing since we want the rotation based on the bind position
            Vector3 lMinOffset = rBone.WorldBindRotation * lMinRotation * Vector3.forward * lHandleScale;
            Vector3 lMaxOffset = rBone.WorldBindRotation * lMaxRotation * Vector3.forward * lHandleScale;

            Handles.color = new Color(0.94118f, 0.39608f, 0.13333f, 1f);
            Handles.DrawLine(lWorldPosition, lWorldPosition + lMinOffset);
            Handles.DrawLine(lWorldPosition, lWorldPosition + lMaxOffset);

            // Render the solid of the angles
            Handles.color = new Color(0.94118f, 0.39608f, 0.13333f, 0.1f);
            Handles.DrawSolidArc(lWorldPosition, rBone.WorldBindRotation * rAxis, (rBone.WorldBindRotation * lMinRotation * Vector3.forward), Mathf.Abs(rMinAngle) + rMaxAngle, lHandleScale);

            // Draw text
            GUI.color = new Color(0.72549f, 0.30588f, 0.10588f, 1f);
            Handles.Label(lWorldPosition + lMinOffset, "min:\r\n" + rMinAngle.ToString("0.00"));
            Handles.Label(lWorldPosition + lMaxOffset, "max:\r\n" + rMaxAngle.ToString("0.00"));

            Vector3 lDirectionAxis = Vector3.Cross(rAxis, rBone._BindRotation * rBone._BoneForward);
            float lSwingAngle = Vector3Ext.SignedAngle(lDirectionAxis, rBone.Swing * lDirectionAxis, rAxis);
            Handles.Label(lWorldPosition + (lWorldSwing * (Vector3.forward * (lHandleScale * 1.3f))), "  " + lSwingAngle.ToString("0.00"));

            // Reset
            GUI.color = lGUIColor;
            Handles.color = lHandleColor;
#endif

            return lIsDirty;
        }

        /// <summary>
        /// This function renders out the handles that allow us to edit the twist limits. It
        /// isn't actually meant to change the twist itself
        /// </summary>
        /// <param name="rBone"></param>
        /// <param name="rMinAngle"></param>
        /// <param name="rMaxAngle"></param>
        /// <returns></returns>
        public static bool JointTwistLimitsHandle(BoneControllerBone rBone, ref float rMinAngle, ref float rMaxAngle)
        {
            bool lIsDirty = false;

#if UNITY_EDITOR

            Vector3 lWorldPosition = rBone._Transform.position;
            Quaternion lWorldSwingRotation = rBone.WorldBindRotation * rBone._Swing;

            Color lGUIColor = GUI.color;
            Color lHandleColor = Handles.color;

            float lHandleScale = 0.2f;
            if (rBone.Skeleton.EditorAutoScaleHandles) { lHandleScale = HandleUtility.GetHandleSize(rBone.Transform.position) * HandlesHelper.HandleScale; }

            // Render the border of the angles
            float lTwistAngle = Vector3Ext.SignedAngle(Vector3.up, rBone._Twist * Vector3.up, Vector3.forward);

            Quaternion lActualRotation = Quaternion.AngleAxis(lTwistAngle, Vector3.forward);
            Quaternion lMinRotation = Quaternion.AngleAxis(rMinAngle, Vector3.forward);
            Quaternion lMaxRotation = Quaternion.AngleAxis(rMaxAngle, Vector3.forward);

            Vector3 lMinOffset = lWorldSwingRotation * lMinRotation * Vector3.up * lHandleScale;
            Vector3 lMaxOffset = lWorldSwingRotation * lMaxRotation * Vector3.up * lHandleScale;

            Handles.color = new Color(0.1f, 0.1f, 0.6f, 1f);
            Handles.DrawLine(lWorldPosition, lWorldPosition + lMinOffset);
            Handles.DrawLine(lWorldPosition, lWorldPosition + lMaxOffset);
            //Handles.DrawLine(lWorldPosition, lWorldPosition + (lWorldSwingRotation * rBone.Twist * Vector3.up * (lHandleScale * 1.1f)));

            // Render the solid of the angles
            Handles.color = new Color(0.25f, 0.60f, 0.95f, 0.1f);
            Handles.DrawSolidArc(lWorldPosition, rBone.Transform.rotation * rBone._BoneForward, lWorldSwingRotation * lMinRotation * Vector3.up, Mathf.Abs(rMinAngle) + rMaxAngle, lHandleScale);

            // Render text
            GUI.color = new Color(0.05f, 0.05f, 0.5f, 1f);
            Handles.Label(lWorldPosition + lMinOffset, "min:\r\n" + rMinAngle.ToString("0.00"));
            Handles.Label(lWorldPosition + lMaxOffset, "max:\r\n" + rMaxAngle.ToString("0.00"));
            Handles.Label(lWorldPosition + (lWorldSwingRotation * lActualRotation * Vector3.up * (lHandleScale * 1.3f)), " " + lTwistAngle.ToString("0.00"));

            // Reset
            GUI.color = lGUIColor;
            Handles.color = lHandleColor;
#endif

            return lIsDirty;
        }

        /// <summary>
        /// Renders a transform using the handle draw functions
        /// </summary>
        /// <param name="rTransform"></param>
        public static void DrawTransform(Transform rTransform, bool rAutoScale)
        {
#if UNITY_EDITOR

            float lHandleScale = 0.1f;
            if (rAutoScale) { lHandleScale = HandleUtility.GetHandleSize(rTransform.position) * HandlesHelper.HandleScale; }

            Color lHandleColor = Handles.color;

            Handles.color = Color.blue;
            Handles.DrawLine(rTransform.position, rTransform.position + (rTransform.forward * lHandleScale));

            Handles.color = Color.green;
            Handles.DrawLine(rTransform.position, rTransform.position + (rTransform.up * lHandleScale));

            Handles.color = Color.red;
            Handles.DrawLine(rTransform.position, rTransform.position + (rTransform.right * lHandleScale));

            Handles.color = lHandleColor;

#endif
        }

        /// <summary>
        /// Renders a transform using the handle draw functions
        /// </summary>
        /// <param name="rTransform"></param>
        public static void DrawTransform(Vector3 rPosition, Quaternion rRotation, float rAlpha, bool rAutoScale, float rScale = 1f)
        {
#if UNITY_EDITOR

            float lHandleScale = 0.1f;
            if (rAutoScale) { lHandleScale = HandleUtility.GetHandleSize(rPosition) * HandlesHelper.HandleScale * 2f; }

            Color lHandleColor = Handles.color;

            Handles.color = new Color(0f, 0f, 1f, rAlpha);
            Handles.DrawLine(rPosition, rPosition + (rRotation * (Vector3.forward * lHandleScale * rScale)));

            Handles.color = new Color(0f, 1f, 0f, rAlpha);
            Handles.DrawLine(rPosition, rPosition + (rRotation * (Vector3.up * lHandleScale * rScale)));

            Handles.color = new Color(1f, 0f, 0f, rAlpha);
            Handles.DrawLine(rPosition, rPosition + (rRotation * (Vector3.right * lHandleScale * rScale)));

            Handles.color = lHandleColor;

#endif
        }

        /// <summary>
        /// Renders a transform using the handle draw functions
        /// </summary>
        /// <param name="rTransform"></param>
        public static void DrawTransform(Vector3 rPosition, Quaternion rRotation, Vector3 rForward, Vector3 rUp, Vector3 rRight, float rAlpha, bool rAutoScale, float rScale = 1f)
        {
#if UNITY_EDITOR

            float lHandleScale = 0.1f;
            if (rAutoScale) { lHandleScale = HandleUtility.GetHandleSize(rPosition) * HandlesHelper.HandleScale * 2f; }

            Color lHandleColor = Handles.color;

            Handles.color = new Color(0f, 0f, 1f, rAlpha);
            Handles.DrawLine(rPosition, rPosition + (rRotation * (rForward * lHandleScale * rScale)));

            Handles.color = new Color(0f, 1f, 0f, rAlpha);
            Handles.DrawLine(rPosition, rPosition + (rRotation * (rUp * lHandleScale * rScale)));

            Handles.color = new Color(1f, 0f, 0f, rAlpha);
            Handles.DrawLine(rPosition, rPosition + (rRotation * (rRight * lHandleScale * rScale)));

            Handles.color = lHandleColor;

#endif
        }

        /// <summary>
        /// Renders a bone using the handle draw functions
        /// </summary>
        /// <param name="rBone"></param>
        /// <param name="rColor"></param>
        public static void DrawBone(BoneControllerBone rBone, Color rColor)
        {
#if UNITY_EDITOR

            if (rBone == null) { return; }
            if (rBone._Transform == null) { return; }

            Color lLineColor = rColor * 0.90f;

            //Vector3[] lBones = Bone.BoneVertices;
            if (DrawnBones == null) { DrawnBones = new Vector3[Bone.BoneVertices.Length]; }

            for (int i = 0; i < DrawnBones.Length; i++)
            {
                DrawnBones[i] = Bone.BoneVertices[i];
            }

            if (rBone.BoneForward != Vector3.up)
            {
                Quaternion lBoneToForward = Quaternion.FromToRotation(Vector3.up, rBone.BoneForward);

                for (int i = 0; i < DrawnBones.Length; i++)
                {
                    DrawnBones[i] = lBoneToForward * Bone.BoneVertices[i];
                }
            }

            // Force the bone rotation
            for (int i = 0; i < DrawnBones.Length; i++)
            {
                DrawnBones[i] = rBone._Transform.rotation * (DrawnBones[i] * rBone.Length);
            }

            // Render the bones
            Vectors[0] = rBone._Transform.position + DrawnBones[0];
            Vectors[1] = rBone._Transform.position + DrawnBones[0];
            Vectors[2] = rBone._Transform.position + DrawnBones[1];
            Vectors[3] = rBone._Transform.position + DrawnBones[2];
            Handles.DrawSolidRectangleWithOutline(Vectors, rColor, lLineColor);

            Vectors[0] = rBone._Transform.position + DrawnBones[0];
            Vectors[1] = rBone._Transform.position + DrawnBones[0];
            Vectors[2] = rBone._Transform.position + DrawnBones[2];
            Vectors[3] = rBone._Transform.position + DrawnBones[3];
            Handles.DrawSolidRectangleWithOutline(Vectors, rColor, lLineColor);

            Vectors[0] = rBone._Transform.position + DrawnBones[0];
            Vectors[1] = rBone._Transform.position + DrawnBones[0];
            Vectors[2] = rBone._Transform.position + DrawnBones[3];
            Vectors[3] = rBone._Transform.position + DrawnBones[4];
            Handles.DrawSolidRectangleWithOutline(Vectors, rColor, lLineColor);

            Vectors[0] = rBone._Transform.position + DrawnBones[0];
            Vectors[1] = rBone._Transform.position + DrawnBones[0];
            Vectors[2] = rBone._Transform.position + DrawnBones[4];
            Vectors[3] = rBone._Transform.position + DrawnBones[1];
            Handles.DrawSolidRectangleWithOutline(Vectors, rColor, lLineColor);

            Vectors[0] = rBone._Transform.position + DrawnBones[5];
            Vectors[1] = rBone._Transform.position + DrawnBones[5];
            Vectors[2] = rBone._Transform.position + DrawnBones[1];
            Vectors[3] = rBone._Transform.position + DrawnBones[2];
            Handles.DrawSolidRectangleWithOutline(Vectors, rColor, lLineColor);

            Vectors[0] = rBone._Transform.position + DrawnBones[5];
            Vectors[1] = rBone._Transform.position + DrawnBones[5];
            Vectors[2] = rBone._Transform.position + DrawnBones[2];
            Vectors[3] = rBone._Transform.position + DrawnBones[3];
            Handles.DrawSolidRectangleWithOutline(Vectors, rColor, lLineColor);

            Vectors[0] = rBone._Transform.position + DrawnBones[5];
            Vectors[1] = rBone._Transform.position + DrawnBones[5];
            Vectors[2] = rBone._Transform.position + DrawnBones[3];
            Vectors[3] = rBone._Transform.position + DrawnBones[4];
            Handles.DrawSolidRectangleWithOutline(Vectors, rColor, lLineColor);

            Vectors[0] = rBone._Transform.position + DrawnBones[5];
            Vectors[1] = rBone._Transform.position + DrawnBones[5];
            Vectors[2] = rBone._Transform.position + DrawnBones[4];
            Vectors[3] = rBone._Transform.position + DrawnBones[1];
            Handles.DrawSolidRectangleWithOutline(Vectors, rColor, lLineColor);

#endif
        }

        /// <summary>
        /// Renders a bone using the handle draw functions
        /// </summary>
        /// <param name="rBone"></param>
        /// <param name="rColor"></param>
        public static void DrawBoneCollider(BoneControllerBone rBone, Color rColor)
        {
#if UNITY_EDITOR

            if (rBone == null) { return; }
            if (rBone._Transform == null) { return; }
            if (rBone._ColliderSize.x == 0f && rBone._ColliderSize.y == 0f && rBone._ColliderSize.z == 0f) { return; }

            Color lHandleColor = Handles.color;
            Handles.color = rColor;

            // Sphere type
            if (rBone._ColliderType == 1)
            {
                Handles.DrawWireDisc(rBone._Transform.position, rBone._Transform.rotation * rBone._BoneForward, rBone._ColliderSize.x);
                Handles.DrawWireDisc(rBone._Transform.position, rBone._Transform.rotation * rBone._BoneUp, rBone._ColliderSize.x);
                Handles.DrawWireDisc(rBone._Transform.position, rBone._Transform.rotation * rBone._BoneRight, rBone._ColliderSize.x);
            }
            // Box type
            else
            {
                float lHalfX = rBone._ColliderSize.x / 2f;
                float lHalfY = rBone._ColliderSize.y / 2f;
                float lZ = rBone.ColliderSize.z;

                Vector3 lPosition = rBone._Transform.position;
                Quaternion lRotation = rBone._Transform.rotation * rBone._ToBoneForward;

                Vector3 lPoint0 = lRotation * new Vector3(-lHalfX, lHalfY, 0f);
                Vector3 lPoint1 = lRotation * new Vector3(-lHalfX, -lHalfY, 0f);
                Vector3 lPoint2 = lRotation * new Vector3(lHalfX, lHalfY, 0f);
                Vector3 lPoint3 = lRotation * new Vector3(lHalfX, -lHalfY, 0f);
                Vector3 lPoint4 = lRotation * new Vector3(-lHalfX, lHalfY, lZ);
                Vector3 lPoint5 = lRotation * new Vector3(-lHalfX, -lHalfY, lZ);
                Vector3 lPoint6 = lRotation * new Vector3(lHalfX, lHalfY, lZ);
                Vector3 lPoint7 = lRotation * new Vector3(lHalfX, -lHalfY, lZ);

                Handles.DrawLine(lPosition + lPoint0, lPosition + lPoint1);
                Handles.DrawLine(lPosition + lPoint0, lPosition + lPoint2);
                Handles.DrawLine(lPosition + lPoint2, lPosition + lPoint3);
                Handles.DrawLine(lPosition + lPoint1, lPosition + lPoint3);

                Handles.DrawLine(lPosition + lPoint4, lPosition + lPoint5);
                Handles.DrawLine(lPosition + lPoint4, lPosition + lPoint6);
                Handles.DrawLine(lPosition + lPoint6, lPosition + lPoint7);
                Handles.DrawLine(lPosition + lPoint5, lPosition + lPoint7);

                Handles.DrawLine(lPosition + lPoint0, lPosition + lPoint4);
                Handles.DrawLine(lPosition + lPoint1, lPosition + lPoint5);
                Handles.DrawLine(lPosition + lPoint2, lPosition + lPoint6);
                Handles.DrawLine(lPosition + lPoint3, lPosition + lPoint7);
            }

            Handles.color = lHandleColor;
#endif
        }

        /// <summary>
        /// Render out the skeleton using bones from this helper
        /// </summary>
        /// <param name="rSkeleton"></param>
        /// <param name="rColor"></param>
        public static void DrawSkeleton(BoneController rSkeleton, Color rBoneColor, Color rColliderColor)
        {
#if UNITY_EDITOR

            if (rSkeleton == null || rSkeleton.RootTransform == null) { return; }

            for (int i = 0; i < rSkeleton.Bones.Count; i++)
            {
                DrawBone(rSkeleton.Bones[i], rBoneColor);
                DrawBoneCollider(rSkeleton.Bones[i], rColliderColor);
            }

#endif
        }

        /// <summary>
        /// Renders a bone using the handle draw functions
        /// </summary>
        /// <param name="rBone"></param>
        /// <param name="rColor"></param>
        public static void DrawBox(Vector3 rMin, Vector3 rMax, Color rColor)
        {
#if UNITY_EDITOR

            Color lHandleColor = Handles.color;
            Handles.color = rColor;

            Vector3 lStart = rMin;
            Vector3 lEnd = rMin;

            lEnd.x = rMax.x;
            Handles.DrawLine(lStart, lEnd);

            lEnd.x = rMin.x;
            lEnd.y = rMax.y;
            Handles.DrawLine(lStart, lEnd);

            lEnd.y = rMin.y;
            lEnd.z = rMax.z;
            Handles.DrawLine(lStart, lEnd);

            // min top
            lStart.y = rMax.y;
            lEnd.y = rMax.y;
            Handles.DrawLine(lStart, lEnd);

            // min right
            lStart.y = rMin.y;
            lStart.z = rMax.z;
            lEnd.y = rMax.y;
            Handles.DrawLine(lStart, lEnd);

            // min/max bottom
            lEnd.x = rMax.x;
            lEnd.y = rMin.y;
            Handles.DrawLine(lStart, lEnd);

            lStart = rMax;
            lEnd = rMax;
            lEnd.x = rMin.x;
            Handles.DrawLine(lStart, lEnd);

            lEnd.x = rMax.x;
            lEnd.y = rMin.y;
            Handles.DrawLine(lStart, lEnd);

            lEnd.y = rMax.y;
            lEnd.z = rMin.z;
            Handles.DrawLine(lStart, lEnd);

            lStart.y = rMin.y;
            lEnd.y = rMin.y;
            Handles.DrawLine(lStart, lEnd);

            lStart.z = rMin.z;
            lEnd.y = rMax.y;
            Handles.DrawLine(lStart, lEnd);

            lStart.y = rMax.y;
            lEnd.x = rMin.x;
            Handles.DrawLine(lStart, lEnd);

            Handles.color = Color.red;
#if UNITY_4 || UNITY_5_0 || UNITY_5_1 || UNITY_5_2 || UNITY_5_3 || UNITY_5_4 || UNITY_5_5
            Handles.SphereCap(2, rMin, Quaternion.identity, 0.01f);
#else
            Handles.SphereHandleCap(2, rMin, Quaternion.identity, 0.01f, EventType.Layout | EventType.Repaint);
#endif

            Handles.color = Color.blue;
#if UNITY_4 || UNITY_5_0 || UNITY_5_1 || UNITY_5_2 || UNITY_5_3 || UNITY_5_4 || UNITY_5_5
            Handles.SphereCap(3, rMax, Quaternion.identity, 0.01f);
#else
            Handles.SphereHandleCap(3, rMax, Quaternion.identity, 0.01f, EventType.Layout | EventType.Repaint);
#endif

            Handles.color = lHandleColor;

#endif
        }
    }
}
