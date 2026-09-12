using TacticalEcho.Character.Player;
using TacticalEcho.Combat.Weapons;
using UnityEngine;

namespace TacticalEcho.AnimationSystem.Runtime
{
    [RequireComponent(typeof(Animator))]
    public sealed class WeaponHandIKController : MonoBehaviour
    {
        private static readonly int RifleAimHash = Animator.StringToHash("Rifle Aim");
        private static readonly int RifleFireHash = Animator.StringToHash("Rifle Fire");
        private static readonly int RifleReloadHash = Animator.StringToHash("Rifle Reload");

        [Header("Weapon")]
        [SerializeField] private WeaponGripPoints gripPoints;
        [SerializeField] private string upperBodyLayerName = "Upper Body";

        [Header("Left Hand IK")]
        [SerializeField, Range(0f, 1f)] private float positionWeight = 1f;
        [SerializeField, Range(0f, 1f)] private float rotationWeight = 0.9f;
        [SerializeField, Min(0.01f)] private float blendSpeed = 14f;
        [SerializeField] private bool enableLeftHandIK = true;

        [Header("Animation State Weights")]
        [Tooltip("Normal rifle holding/aiming keeps the support hand locked to the handguard.")]
        [SerializeField, Range(0f, 1f)] private float aimWeight = 1f;
        [Tooltip("Fire keeps most of the IK while allowing the authored recoil animation to move slightly.")]
        [SerializeField, Range(0f, 1f)] private float fireWeight = 0.9f;
        [Tooltip("Reload releases the support hand so the authored reload animation can reach the magazine.")]
        [SerializeField, Range(0f, 1f)] private float reloadWeight = 0f;
        [Tooltip("Fallback for other upper-body states if more rifle animations are added later.")]
        [SerializeField, Range(0f, 1f)] private float defaultWeight = 0.8f;

        private Animator animator;
        private int upperBodyLayerIndex = -1;
        private float currentWeight;
        private bool weaponFacingChecked;

        private void Awake()
        {
            animator = GetComponent<Animator>();
            ResolveUpperBodyLayer();
        }

        private void OnEnable()
        {
            weaponFacingChecked = false;
            ResolveUpperBodyLayer();
        }

        private void Start()
        {
            CorrectWeaponFacingIfNeeded();
        }

        public void Configure(WeaponGripPoints newGripPoints)
        {
            gripPoints = newGripPoints;
            weaponFacingChecked = false;
            ResolveUpperBodyLayer();
        }

        private void OnAnimatorIK(int layerIndex)
        {
            if (!weaponFacingChecked)
            {
                CorrectWeaponFacingIfNeeded();
            }

            if (animator == null || gripPoints == null || gripPoints.LeftHandGrip == null)
            {
                return;
            }

            if (upperBodyLayerIndex < 0)
            {
                ResolveUpperBodyLayer();
            }

            if (upperBodyLayerIndex < 0 || layerIndex != upperBodyLayerIndex)
            {
                return;
            }

            float targetWeight = enableLeftHandIK ? EvaluateStateWeight() : 0f;
            float blend = 1f - Mathf.Exp(-blendSpeed * Time.deltaTime);
            currentWeight = Mathf.Lerp(currentWeight, targetWeight, blend);

            Transform target = gripPoints.LeftHandGrip;
            animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, positionWeight * currentWeight);
            animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, rotationWeight * currentWeight);
            animator.SetIKPosition(AvatarIKGoal.LeftHand, target.position);
            animator.SetIKRotation(AvatarIKGoal.LeftHand, target.rotation);
        }

        private void CorrectWeaponFacingIfNeeded()
        {
            weaponFacingChecked = true;

            if (animator == null || !animator.isHuman || gripPoints == null)
            {
                return;
            }

            Transform rightGrip = gripPoints.RightHandGrip;
            Transform leftGrip = gripPoints.LeftHandGrip;
            Transform rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            Transform weaponMount = gripPoints.transform.parent;

            if (rightGrip == null || leftGrip == null || rightHand == null || weaponMount == null)
            {
                return;
            }

            PlayerController player = animator.GetComponentInParent<PlayerController>();
            Transform characterRoot = player != null ? player.transform : animator.transform;
            Vector3 up = characterRoot.up;

            // For a rifle, the support-hand grip is physically in front of the trigger-hand grip.
            // That makes RightHandGrip -> LeftHandGrip a stable weapon-forward reference without
            // depending on the imported model's arbitrary FBX local axes.
            Vector3 gripForward = Vector3.ProjectOnPlane(leftGrip.position - rightGrip.position, up);
            Vector3 characterForward = Vector3.ProjectOnPlane(characterRoot.forward, up);

            if (gripForward.sqrMagnitude < 0.0001f || characterForward.sqrMagnitude < 0.0001f)
            {
                return;
            }

            gripForward.Normalize();
            characterForward.Normalize();

            // If the barrel/support-hand side points behind the character, rotate the complete
            // weapon mount around the right-hand pivot. Because the right-hand grip already sits
            // on that pivot, its position remains locked while the rifle direction is corrected.
            if (Vector3.Dot(gripForward, characterForward) < -0.05f)
            {
                weaponMount.RotateAround(rightHand.position, up, 180f);
            }
        }

        private float EvaluateStateWeight()
        {
            AnimatorStateInfo currentState = animator.GetCurrentAnimatorStateInfo(upperBodyLayerIndex);
            float currentStateWeight = GetWeightForState(currentState.shortNameHash);

            if (!animator.IsInTransition(upperBodyLayerIndex))
            {
                return currentStateWeight;
            }

            AnimatorStateInfo nextState = animator.GetNextAnimatorStateInfo(upperBodyLayerIndex);
            AnimatorTransitionInfo transition = animator.GetAnimatorTransitionInfo(upperBodyLayerIndex);
            float nextStateWeight = GetWeightForState(nextState.shortNameHash);
            float transitionProgress = Mathf.Clamp01(transition.normalizedTime);

            return Mathf.Lerp(currentStateWeight, nextStateWeight, transitionProgress);
        }

        private float GetWeightForState(int shortNameHash)
        {
            if (shortNameHash == RifleReloadHash)
            {
                return reloadWeight;
            }

            if (shortNameHash == RifleFireHash)
            {
                return fireWeight;
            }

            if (shortNameHash == RifleAimHash)
            {
                return aimWeight;
            }

            return defaultWeight;
        }

        private void ResolveUpperBodyLayer()
        {
            if (animator == null || animator.runtimeAnimatorController == null)
            {
                upperBodyLayerIndex = -1;
                return;
            }

            upperBodyLayerIndex = animator.GetLayerIndex(upperBodyLayerName);
        }
    }
}
