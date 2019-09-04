using System;
using System.Collections.Generic;
using UnityEngine;
using com.ootii.Base;
using com.ootii.Geometry;
using com.ootii.Utilities;
using com.ootii.Utilities.Debug;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace com.ootii.Actors.BoneControllers
{
    /// <summary>
    /// An IK solver is used to determine bone rotations based on
    /// bone chains and thier properties. Different solvers find the
    /// end result in different ways
    /// </summary>
    [Serializable]
    public class IKSolver : BaseScriptableObject
    {
    }
}
