using System;
using System.Collections.Generic;
using System.Text;

namespace com.ootii.Actors.BoneControllers
{
    /// <summary>
    /// Defines the name value for the editors
    /// </summary>
    public class IKBoneJointNameAttribute : Attribute
    {
        /// <summary>
        /// Default value
        /// </summary>
        protected string mValue;
        public string Value
        {
            get { return mValue; }
        }

        /// <summary>
        /// Constructor for the attribute
        /// </summary>
        /// <param name="rValue">Value that is to be used</param>
        public IKBoneJointNameAttribute(string rValue)
        {
            mValue = rValue;
        }
    }
}
