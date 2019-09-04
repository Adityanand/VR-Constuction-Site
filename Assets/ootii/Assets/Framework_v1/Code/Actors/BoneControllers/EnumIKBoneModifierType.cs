using System;

namespace com.ootii.Actors.BoneControllers
{
    /// <summary>
    /// Defines the different modes of the controller
    /// </summary>
    public class EnumIKBoneModifierType
    {
        /// <summary>
        /// Simple rotation that rotates the bone based on
        /// local rotation
        /// </summary>
        public const int LOCAL_ROTATION = 0;

        /// <summary>
        /// World based rotation that needs to be converted to local 
        /// rotation
        /// </summary>
        public const int WORLD_ROTATION = 1;

        /// <summary>
        /// Local position of the next bone. Will
        /// help define the final rotation for this bone
        /// </summary>
        public const int NEXT_LOCAL_POSITION = 2;

        /// <summary>
        /// World position of the next bone. will
        /// help define the final rotation
        /// </summary>
        public const int END_WORLD_POSITION = 3;

        /// <summary>
        /// Friendly name of the type
        /// </summary>
        public static string[] Names = new string[] { 
            "Local Rotation", 
            "World Rotation", 
            "End Local Position", 
            "End World Position"
        };
    }
}

