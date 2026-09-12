using TacticalEcho.Combat.Weapons;
using UnityEngine;

namespace TacticalEcho.AnimationSystem.Runtime
{
    [RequireComponent(typeof(Animator))]
    public sealed class WeaponHandIKController : MonoBehaviour
    {
        [Header("Weapon")]
        [SerializeField] private WeaponGripPoints gripPoints;

        [Header("Left Hand IK")]
        [SerializeField, Range(0f, 1f)] private float positionWeight = 1f;
        [SerializeField, Range(0f, 1f)] private float rotationWeight = 1f;
        [SerializeField, Min(0.01f)] private float blendSpeed = 12f;
        [SerializeField] private bool enableLeftHandIK = true;

        private Animator animator;
        private float currentWeight;

        private void Awake()
        {
            animator = GetComponent<Animator>();
        }

        public void Configure(WeaponGripPoints newGripPoints)
        {
            gripPoints = newGripPoints;
        }

        private void Update()
        {
            float target = enableLeftHandIK && gripPoints != null && gripPoints.LeftHandGrip != null ? 1f : 0f;
            float t = 1f - Mathf.Exp(-blendSpeed * Time.deltaTime);
            currentWeight = Mathf.Lerp(currentWeight, target, t);
        }

        private void OnAnimatorIK(int layerIndex)
        {
            if (animator == null || gripPoints == null || gripPoints.LeftHandGrip == null)
            {
                return;
            }

            Transform target = gripPoints.LeftHandGrip;
            animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, positionWeight * currentWeight);
            animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, rotationWeight * currentWeight);
            animator.SetIKPosition(AvatarIKGoal.LeftHand, target.position);
            animator.SetIKRotation(AvatarIKGoal.LeftHand, target.rotation);
        }
    }
}
