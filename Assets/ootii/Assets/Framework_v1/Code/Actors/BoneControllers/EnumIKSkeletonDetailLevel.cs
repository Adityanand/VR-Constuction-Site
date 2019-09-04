using System;

namespace com.ootii.Actors.BoneControllers
{
    /// <summary>
    /// Simple class for defining how much detail we'll provide
    /// </summary>
    public class EnumIKSkeletonDetailLevel
    {
        public const int LOW = 0;
        public const int MEDIUM = 1;
        public const int HIGH = 2;

        public static string[] Names = new string[] { "Low", "Medium", "High" };
    }
}
