using System;

namespace com.ootii.Actors.BoneControllers
{
    /// <summary>
    /// Defines the different collider types of the bone
    /// </summary>
    public class EnumIKBoneColliderType
    {
        /// <summary>
        /// Simple rotation that rotates the bone based on
        /// local rotation
        /// </summary>
        public const int BOX = 0;

        /// <summary>
        /// World based rotation that needs to be converted to local 
        /// rotation
        /// </summary>
        public const int SPHERE = 1;

        /// <summary>
        /// Local position of the next bone. Will
        /// help define the final rotation for this bone
        /// </summary>
        public const int CAPSULE = 2;

        /// <summary>
        /// Friendly name of the type
        /// </summary>
        public static string[] Names = new string[] { 
            "Box", 
            "Sphere"
        };
    }
}

