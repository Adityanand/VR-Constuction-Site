using System;

namespace com.ootii.Actors.BoneControllers
{
    /// <summary>
    /// Allows for a friendly name for the class
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class IKNameAttribute : Attribute
    {
        public string Name;
        public IKNameAttribute(string rValue)
        {
            Name = rValue;
        }
    }

    /// <summary>
    /// Defines the tooltip value for motor properties
    /// </summary>
    public class IKTooltipAttribute : Attribute
    {
        public string Tooltip;
        public IKTooltipAttribute(string rValue)
        {
            Tooltip = rValue;
        }
    }

    /// <summary>
    /// Defines the name value for the editors
    /// </summary>
    public class IKDescriptionAttribute : Attribute
    {
        public string Description;
        public IKDescriptionAttribute(string rValue)
        {
            Description = rValue;
        }
    }
}
