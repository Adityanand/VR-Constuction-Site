using System;

namespace com.ootii.Actors.BoneControllers
{
    /// <summary>
    /// Simple class for defining what axis to rotate on
    /// </summary>
    public class EnumIKBoneRotationAxis
    {
        public const int BONE = 0;
        public const int MODEL = 1;

        public static string[] Names = new string[] { "Bone", "Model" };
    }
}
