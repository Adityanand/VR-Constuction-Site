using System;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace com.ootii.Helpers
{
    /// <summary>
    /// Provides helpers for rendering values and user input to the inspector
    /// </summary>
    public class InspectorHelper
    {

        /// <summary>
        /// This function renders out the handles that allow us to edit the twist limits. It
        /// isn't actually meant to change the twist itself
        /// </summary>
        /// <param name="rBone"></param>
        /// <param name="rMinAngle"></param>
        /// <param name="rMaxAngle"></param>
        /// <returns></returns>
        public static Vector3 Vector3Fields(string rName, string rTip, Vector3 rValue, bool rShowX, bool rShowY, bool rShowZ)
        {
            Vector3 lResult = rValue;

#if UNITY_EDITOR

            if (rShowX)
            {
                lResult.x = EditorGUILayout.FloatField(new GUIContent(rName + " Pitch", rTip), lResult.x, GUILayout.MinWidth(40));
            }

            if (rShowY)
            {
                lResult.y = EditorGUILayout.FloatField(new GUIContent(rName + " Yaw", rTip), lResult.y, GUILayout.MinWidth(40));
            }

            if (rShowZ)
            {
                lResult.z = EditorGUILayout.FloatField(new GUIContent(rName + " Roll", rTip), lResult.z, GUILayout.MinWidth(40));
            }

#endif

            return lResult;
        }
    }
}
