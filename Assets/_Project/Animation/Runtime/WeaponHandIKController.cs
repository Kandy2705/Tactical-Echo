using System.Collections;
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

        [Header("Calibration")]
        [Tooltip("Align grip anchors once to the animated hands at startup. Disable after final manual grip authoring if desired.")]
        [SerializeField] private bool calibrateGripPointsOnStart = true;

        private Animator animator;
        private float currentWeight;
        private bool calibrated;

        private void Awake()
        {
            animator = GetComponent<Animator>();
        }

        private void OnEnable()
        {
            if (calibrateGripPointsOnStart)
            {
                StartCoroutine(CalibrateAfterAnimatorEvaluates());
            }
        }

        public void Configure(WeaponGripPoints newGripPoints, bool calibrateOnStart = true)
        {
            gripPoints = newGripPoints;
            calibrateGripPointsOnStart = calibrateOnStart;
        }

        private IEnumerator CalibrateAfterAnimatorEvaluates()
        {
            yield return null;
            yield return new WaitForEndOfFrame();
            CalibrateGripPointsFromCurrentPose();
        }

        [ContextMenu("Calibrate Grip Points From Current Pose")]
        public void CalibrateGripPointsFromCurrentPose()
        {
            if (animator == null || !animator.isHuman || gripPoints == null)
            {
                return;
            }

            Transform rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            Transform leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);

            if (rightHand != null && gripPoints.RightHandGrip != null)
            {
                gripPoints.RightHandGrip.SetPositionAndRotation(rightHand.position, rightHand.rotation);
            }

            if (leftHand != null && gripPoints.LeftHandGrip != null)
            {
                gripPoints.LeftHandGrip.SetPositionAndRotation(leftHand.position, leftHand.rotation);
            }

            calibrated = true;
        }

        private void Update()
        {
            bool canUseIK = enableLeftHandIK
                && calibrated
                && gripPoints != null
                && gripPoints.LeftHandGrip != null;

            float target = canUseIK ? 1f : 0f;
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
