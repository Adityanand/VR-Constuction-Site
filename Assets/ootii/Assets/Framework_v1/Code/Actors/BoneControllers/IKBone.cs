using System;
using System.Collections.Generic;
using UnityEngine;
using com.ootii.Base;

namespace com.ootii.Actors.BoneControllers
{
    /// <summary>
    /// Base class for all Bone Controller bones. Provides the
    /// basic properties and functionality of the bones
    /// </summary>
    public abstract class IKBone : BaseObject
    {
        /// <summary>
        /// Underlying transform this bone represents
        /// </summary>
        public Transform _Transform;
        public virtual Transform Transform
        {
            get { return _Transform; }

            set
            {
                _Transform = value;

                Name = _Transform.name;
                mCleanName = BaseBoneController.CleanBoneName(Name);

                _BindRotation = _Transform.localRotation;
                _BindMatrix = _Transform.worldToLocalMatrix;
            }
        }

        /// <summary>
        /// Cleans the 
        /// </summary>
        /// <param name="rName"></param>
        /// <returns></returns>
        protected string mCleanName = "";
        public virtual string CleanName
        {
            get { return mCleanName; }
        }

        /// <summary>
        /// Rotation that the bone imports with. We consider this the
        /// "bind pose". Meaning this is the rotation the bone was 
        /// probably modelled with and the skin was bound to.
        /// 
        /// Note: We keep this field public so they are serialized
        /// </summary>
        public Quaternion _BindRotation = Quaternion.identity;
        public virtual Quaternion BindRotation
        {
            get { return _BindRotation; }
        }

        /// <summary>
        /// Position that the bone imports with. Meaning this is the position
        /// the bone was probably modeled with and the skin was bound to.
        /// </summary>
        public Vector3 _BindPosition = Vector3.zero;
        public virtual Vector3 BindPosition
        {
            get { return _BindPosition; }
        }

        /// <summary>
        /// Matrix that represents the bone's rotation and position at the
        /// point when it was imported. We assume the import pose is the
        /// "bind pose".
        /// </summary>
        public Matrix4x4 _BindMatrix = Matrix4x4.identity;
        public virtual Matrix4x4 BindMatrix
        {
            get { return _BindMatrix; }
        }

        /// <summary>
        /// Direction the bone was created in. This defines the
        /// natural directon of the tip from the base
        /// </summary>
        public Vector3 _BoneForward = Vector3.forward;
        public virtual Vector3 BoneForward
        {
            get { return _BoneForward; }
            set { _BoneForward = value; }
        }

        /// <summary>
        /// Up direction the bone was created in. This defines the
        /// natural directon of the up vector.
        /// </summary>
        public Vector3 _BoneUp = Vector3.up;
        public virtual Vector3 BoneUp
        {
            get { return _BoneUp; }
            set { _BoneUp = value; }
        }

        /// <summary>
        /// Right direction the bone was created in. This defines the
        /// natural directon of the right vector.
        /// </summary>
        public Vector3 _BoneRight = Vector3.right;
        public virtual Vector3 BoneRight
        {
            get { return _BoneRight; }
            set { _BoneRight = value; }
        }

        /// <summary>
        /// Quaternion that when rotate a forward facing direction
        /// to a bone-forward facing direction.
        /// </summary>
        public Quaternion _ToBoneForward = Quaternion.identity;
        public virtual Quaternion ToBoneForward
        {
            get { return _ToBoneForward; }
            set { _ToBoneForward = value; }
        }

        /// <summary>
        /// Determines if the bone values are dirty. If so, we'll process them.
        /// If not, we don't actually run the bone
        /// </summary>
        public bool _IsDirty = false;
        public virtual bool IsDirty
        {
            get { return _IsDirty; }
            set { _IsDirty = value; }
        }

        /// <summary>
        /// Returns the length of the bone.
        /// </summary>
        public float _Length = 0.02f;
        public virtual float Length
        {
            get { return _Length; }
            set { _Length = value; }
        }

        /// <summary>
        /// Weight that determines how much weight the IK has on the current
        /// bone rotation (0 = none, 1 = total);
        /// </summary>
        public float _Weight = 1f;
        public virtual float Weight
        {
            get { return _Weight; }
            set { _Weight = Mathf.Clamp01(value); }
        }

        /// <summary>
        /// The current local rotation of the bone. The swing
        /// represents rotation along the axes that are perpendicular
        /// to the direction of the bone. For example, if the bone
        /// is built along the 'y' axis, this is the rotation 
        /// along the x and z axis.
        /// </summary>
        public Quaternion _Swing = Quaternion.identity;
        public virtual Quaternion Swing
        {
            get { return _Swing; }
        }

        /// <summary>
        /// The current local rotation of the bone.  The twist
        /// represents rotation along the axis that is the bone direction.
        /// If the bone is built along the 'y' axis, this is the roll of
        /// the bone along that y axis.
        /// </summary>
        public Quaternion _Twist = Quaternion.identity;
        public virtual Quaternion Twist
        {
            get { return _Twist; }
        }

        /// <summary>
        /// Defines the type of collider we're using.
        /// 0 = box
        /// 1 = sphere
        /// 2 = capsule
        /// </summary>
        public int _ColliderType = 0;
        public virtual int ColliderType
        {
            get { return _ColliderType; }
            set { _ColliderType = value; }
        }

        /// <summary>
        /// Determines the size of the bone's object aligned bounding box. This
        /// is what we'll use to test for impact or other collisions as it's faster
        /// than attaching true colliders.
        /// Box = [x = width, y = height, z = depth (forward)]
        /// sphere = [x, y, z = radius]
        /// capsule = [x, y = radius, z = depth (forward)]
        /// </summary>
        public Vector3 _ColliderSize = Vector3.zero;
        public Vector3 ColliderSize
        {
            get { return _ColliderSize; }
            set { _ColliderSize = value; }
        }

        /// <summary>
        /// Due to Unity serialization depth and reference issues, we need to serialize IDs and not
        /// the actual objects. This will up us recreate our hierarchies
        /// 
        /// Note: We keep this field public so it is serialized
        /// </summary>
        public int SerializationParentIndex = -1;

        /// <summary>
        /// Due to Unity serialization depth and reference issues issues, we need to serialize IDs and not
        /// the actual objects. This will up us recreate our hierarchies
        /// 
        /// Note: We keep this field public so it is serialized
        /// </summary>
        public List<int> SerializationChildIndexes = new List<int>();

        /// <summary>
        /// Default constructor
        /// </summary>
        public IKBone()
            : base()
        {
        }

        /// <summary>
        /// If any bones are saved, they should be invalidated and re-grabbed
        /// from the skeleton.
        /// </summary>
        public abstract void InvalidateBones();

        /// <summary>
        /// We can't load the bones in InvalidateBones because that's called from
        /// a seperate thread sometimes. So, we'll load the actual bone
        /// references (if needed) in this seperate function.
        /// </summary>
        public abstract void LoadBones();

        /// <summary>
        /// Clears the bone of any objects it maintains.
        /// </summary>
        public abstract void Clear();

        /// <summary>
        /// Clears the rotation value so that it's not applied
        /// </summary>
        public abstract void ClearRotation();

        /// <summary>
        /// Converts a full "world" rotation to a local rotation. The local rotation can
        /// than be used to process constraints.
        /// </summary>
        /// <param name="rWorldRotation"></param>
        /// <returns></returns>
        public abstract Quaternion TransformWorldRotationToLocalRotation(Quaternion rWorldRotation);

        /// <summary>
        /// Converts a full "world" direction to a local direction.
        /// </summary>
        /// <param name="rWorldDirection"></param>
        /// <returns></returns>
        public abstract Vector3 TransformWorldRotationToLocalRotation(Vector3 rWorldDirection);

        /// <summary>
        /// Converts a local rotation to a world rotation. The local rotation can
        /// than be used to process constraints.
        /// </summary>
        /// <param name="rLocalRotation"></param>
        /// <returns></returns>
        public abstract Quaternion TransformLocalRotationToWorldRotation(Quaternion rLocalRotation);

        /// <summary>
        /// Converts a local direction to a world direction.
        /// </summary>
        /// <param name="rLocalRotation"></param>
        /// <returns></returns>
        public abstract Vector3 TransformLocalRotationToWorldRotation(Vector3 rLocalDirection);

        /// <summary>
        /// Converts a local point to a world point
        /// </summary>
        /// <param name="rLocalPoint"></param>
        /// <returns></returns>
        public abstract Vector3 TransformLocalPointToWorldPoint(Vector3 rLocalPoint);

        /// <summary>
        /// Sets just the world swing and forces the twist to Quaternion.identity
        /// </summary>
        /// <param name="rRotation"></param>
        /// <param name="rWeight"></param>
        public abstract void SetWorldSwing(Quaternion rRotation, float rWeight);

        /// <summary>
        /// Rotates the bone using the specified angles (in degrees)
        /// </summary>
        /// <param name="rEuler">Vector containing the angles (pitch, yaw, roll)</param>
        public abstract void SetWorldRotation(Vector3 rEuler, float rWeight);

        /// <summary>
        /// Rotates the bone using the specified angles (in degrees)
        /// </summary>
        /// <param name="rPitch">Rotation around the x axis (posative is down)</param>
        /// <param name="rYaw">Rotation around the y axis (posative is right)</param>
        /// <param name="rRoll">Rotation around the z axis (posative is right)</param>
        public abstract void SetWorldRotation(float rPitch, float rYaw, float rRoll, float rWeight);

        /// <summary>
        /// Rotates the bone using the specified rotation
        /// </summary>
        /// <param name="rRotation">Quaternion containing the rotation</param>
        public abstract void SetWorldRotation(Quaternion rRotation, float rWeight);

        /// <summary>
        /// Rotates the bone using the specified rotation
        /// </summary>
        /// <param name="rRotation">Quaternion containing the rotation</param>
        public abstract void SetWorldRotation(Quaternion rSwing, Quaternion rTwist, float rWeight);

        /// <summary>
        /// Rotates the bone using the specified angles (in degrees)
        /// </summary>
        /// <param name="rEuler">Vector containing the angles (pitch, yaw, roll)</param>
        public abstract void SetLocalRotation(Vector3 rEuler, float rWeight);

        /// <summary>
        /// Force the local rotation to the specified angles (in degrees)
        /// </summary>
        /// <param name="rPitch">Angle (in degrees) around the X-axis</param>
        /// <param name="rYaw">Angle (in degrees) around the Y-axis</param>
        /// <param name="rRoll">Angle (in degrees) around the Z-axis</param>
        public abstract void SetLocalRotation(float rPitch, float rYaw, float rRoll, float rWeight);

        /// <summary>
        /// Rotates the bone using the specified local rotation. We'll extract
        /// the swing and twist values so we can process them seperately
        /// </summary>
        /// <param name="rRotation">Quaternion containing the rotation</param>
        public abstract void SetLocalRotation(Quaternion rRotation, float rWeight);

        /// <summary>
        /// Rotates the bone along the swing and twist axis. Useful if we're 
        /// dealing with three degrees of freedome
        /// </summary>
        /// <param name="rSwing">Rotation along the axes perpedicular to the bone axis</param>
        /// <param name="rTwist">Rotation along the bone axis</param>
        public abstract void SetLocalRotation(Quaternion rSwing, Quaternion rTwist, float rWeight);

        /// <summary>
        /// Rotates the bone to point to the specified position. The up vector of the bone
        /// will be it's current up vector
        /// </summary>
        /// <param name="rPosition">Position to rotat towards</param>
        /// <param name="rWeight">Impact the modifier has</param>
        public abstract void SetWorldEndPosition(Vector3 rPosition, float rWeight);

        /// <summary>
        /// Rotates the bone to point to the specified position. The up vector of the bone
        /// will be it's current up vector
        /// </summary>
        /// <param name="rPosition">Position to rotat towards</param>
        /// <param name="rUp">Up vector to use with the look rotation</param>
        /// <param name="rWeight">Impact the modifier has</param>
        public abstract void SetWorldEndPosition(Vector3 rPosition, Vector3 rUp, float rWeight);

        /// <summary>
        /// Tests if the point is contained by the bone's collider
        /// </summary>
        /// <param name="rPoint"></param>
        /// <returns></returns>
        public abstract bool TestPointCollision(Vector3 rPoint);

        /// <summary>
        /// Tests if the ray collides with the bone's collider and if so, it returns the collision point
        /// </summary>
        /// <param name="rStart"></param>
        /// <param name="rDirection"></param>
        /// <param name="rRange"></param>
        /// <param name="rHitPoint"></param>
        /// <returns></returns>
        public abstract bool TestRayCollision(Vector3 rStart, Vector3 rDirection, float rRange, out Vector3 rHitPoint);

        /// <summary>
        /// Finds the closes point to the bone based on the collider information
        /// </summary>
        /// <param name="rPoint">World oriented point we're looking at</param>
        /// <returns>World oriented point that is on the collider bounds</returns>
        public abstract Vector3 ClosetPoint(Vector3 rPoint);
    }
}
