using System;
using System.Collections.Generic;
using UnityEngine;
using com.ootii.Base;

namespace com.ootii.Actors.BoneControllers
{
    /// <summary>
    /// Base class for all Bone Controller motors. Provides the 
    /// basic properties and functionality of the bones
    /// </summary>
    public abstract class IKMotor : BaseObject
    {
        /// <summary>
        /// Determines if the motor is available to work
        /// </summary>
        public bool _IsEnabled = true;
        public virtual bool IsEnabled
        {
            get { return _IsEnabled; }
            set { _IsEnabled = value; }
        }

        /// <summary>
        /// Determines if the motor is actually runnable while we're in edit mode.
        /// </summary>
        public bool _IsEditorEnabled = false;
        public virtual bool IsEditorEnabled
        {
            get { return _IsEditorEnabled; }
            set { _IsEditorEnabled = value; }
        }

        /// <summary>
        /// Determines if we show the debug information while in the editor
        /// </summary>
        public bool _IsDebugEnabled = false;
        public virtual bool IsDebugEnabled
        {
            get { return _IsDebugEnabled; }
            set { _IsDebugEnabled = value; }
        }

        /// <summary>
        /// Determines if we should use a fixed update or the standard update. The
        /// fixed update is important for smoothing out lerps or using physics.
        /// </summary>
        public bool _IsFixedUpdateEnabled = false;
        public virtual bool IsFixedUpdateEnabled
        {
            get { return _IsFixedUpdateEnabled; }
            set { _IsFixedUpdateEnabled = value; }
        }

        /// <summary>
        /// Determines the frame rate we're targeting for fixed updates. The
        /// fixed update is important for smoothing out lerps or using physics.
        /// </summary>
        public float _FixedUpdateFPS = 60f;
        public virtual float FixedUpdateFPS
        {
            get { return _FixedUpdateFPS; }
            set { _FixedUpdateFPS = value; }
        }

        /// <summary>
        /// Impact this motor has relative to the current state of the skeleton.
        /// Typically this would be the state after animations happen
        /// </summary>
        public float _Weight = 1f;
        public virtual float Weight
        {
            get { return _Weight; }
            set { _Weight = Mathf.Clamp01(value); }
        }

        /// <summary>
        /// Determines the amount of weight the motor has relative
        /// to the other motors (0 = none, 1 = full)
        /// </summary>
        public float _BoneWeight = 1f;
        public virtual float BoneWeight
        {
            get { return _BoneWeight; }
            set { _BoneWeight = Mathf.Clamp01(value); }
        }

        /// <summary>
        /// Default constructor
        /// </summary>
        public IKMotor()
            : base()
        {
        }
    }
}
