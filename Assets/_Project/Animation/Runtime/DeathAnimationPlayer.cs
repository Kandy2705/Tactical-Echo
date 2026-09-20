using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace TacticalEcho.AnimationSystem.Runtime
{











    public sealed class DeathAnimationPlayer : MonoBehaviour
    {
        private const string DeathClipResourcePath = "Death/Death_From_Front_Headshot";

        private static AnimationClip cachedDeathClip;
        private static bool warnedMissingClip;

        private readonly List<WeaponHandIKController> suspendedIk = new();

        private PlayableGraph graph;
        private bool hasSuspendedRootState;
        private bool previousApplyRootMotion;
        private Vector3 previousLocalPosition;
        private Quaternion previousLocalRotation;

        public static bool Play(Animator animator)
        {
            if (animator == null)
            {
                return false;
            }

            DeathAnimationPlayer player = animator.GetComponent<DeathAnimationPlayer>();
            if (player == null)
            {
                player = animator.gameObject.AddComponent<DeathAnimationPlayer>();
            }

            return player.PlayInternal(animator);
        }






        public static void Stop(Animator animator)
        {
            if (animator == null)
            {
                return;
            }

            DeathAnimationPlayer player = animator.GetComponent<DeathAnimationPlayer>();
            player?.StopInternal();
        }

        private bool PlayInternal(Animator animator)
        {
            AnimationClip clip = ResolveDeathClip();
            if (clip == null)
            {
                return false;
            }

            SuspendWeaponIk(animator);
            SuspendRootMotionState(animator);

            if (graph.IsValid())
            {
                graph.Destroy();
            }

            graph = PlayableGraph.Create($"{animator.name}_DeathAnimation");
            graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);

            AnimationPlayableOutput output = AnimationPlayableOutput.Create(graph, "Death", animator);
            AnimationClipPlayable playable = AnimationClipPlayable.Create(graph, clip);
            playable.SetApplyFootIK(false);
            playable.SetApplyPlayableIK(false);
            output.SetSourcePlayable(playable);

            graph.Play();
            return true;
        }

        private void StopInternal()
        {
            if (graph.IsValid())
            {
                graph.Destroy();
            }

            RestoreWeaponIk();
            RestoreRootMotionState();
        }

        private static AnimationClip ResolveDeathClip()
        {
            if (cachedDeathClip != null)
            {
                return cachedDeathClip;
            }

            AnimationClip[] clips = Resources.LoadAll<AnimationClip>(DeathClipResourcePath);
            cachedDeathClip = Array.Find(
                clips,
                clip => clip != null && !clip.name.StartsWith("__preview__", StringComparison.OrdinalIgnoreCase));

            if (cachedDeathClip == null && !warnedMissingClip)
            {
                warnedMissingClip = true;
                Debug.LogWarning(
                    $"[Death Animation] No AnimationClip found at Resources/{DeathClipResourcePath}. " +
                    "Death logic still works, but no death pose will play.");
            }

            return cachedDeathClip;
        }











        private void SuspendRootMotionState(Animator animator)
        {
            if (!hasSuspendedRootState)
            {
                hasSuspendedRootState = true;
                previousApplyRootMotion = animator.applyRootMotion;
                previousLocalPosition = animator.transform.localPosition;
                previousLocalRotation = animator.transform.localRotation;
            }

            animator.applyRootMotion = true;
        }

        private void RestoreRootMotionState()
        {
            if (!hasSuspendedRootState)
            {
                return;
            }

            hasSuspendedRootState = false;

            Animator animator = GetComponent<Animator>();
            if (animator == null)
            {
                return;
            }

            animator.applyRootMotion = previousApplyRootMotion;
            animator.transform.localPosition = previousLocalPosition;
            animator.transform.localRotation = previousLocalRotation;
        }






        private void SuspendWeaponIk(Animator animator)
        {
            suspendedIk.Clear();

            foreach (WeaponHandIKController ik in animator.GetComponentsInChildren<WeaponHandIKController>(true))
            {
                if (ik == null || !ik.enabled)
                {
                    continue;
                }

                ik.enabled = false;
                suspendedIk.Add(ik);
            }
        }

        private void RestoreWeaponIk()
        {
            foreach (WeaponHandIKController ik in suspendedIk)
            {
                if (ik != null)
                {
                    ik.enabled = true;
                }
            }

            suspendedIk.Clear();
        }

        private void OnDestroy()
        {
            if (graph.IsValid())
            {
                graph.Destroy();
            }
        }
    }
}
