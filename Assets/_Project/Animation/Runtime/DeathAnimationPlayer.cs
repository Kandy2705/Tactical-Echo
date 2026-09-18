using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace TacticalEcho.AnimationSystem.Runtime
{
    /// <summary>
    /// Plays the shared full-body death clip directly through the Animator using Playables.
    /// This keeps death independent from locomotion/weapon Animator states and lets Player
    /// and Enemy reuse the exact same animation path.
    /// </summary>
    /// <remarks>
    /// While the graph is playing it owns the Animator's output completely: the
    /// AnimatorController (locomotion blend tree, upper body layer) drives nothing. That is
    /// correct for a corpse, but it means the graph must be torn down again by anything that
    /// brings the character back - see <see cref="Stop"/>.
    /// </remarks>
    public sealed class DeathAnimationPlayer : MonoBehaviour
    {
        private const string DeathClipResourcePath = "Death/Death_From_Front_Headshot";

        private static AnimationClip cachedDeathClip;
        private static bool warnedMissingClip;

        private readonly List<WeaponHandIKController> suspendedIk = new();

        private PlayableGraph graph;

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

        /// <summary>
        /// Stops the death playback and hands the rig back to the AnimatorController.
        /// Anything that clears a death flag must call this, otherwise the character keeps
        /// the death pose forever while the rest of its systems behave as if it were alive.
        /// </summary>
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

        /// <summary>
        /// Scoped to the Animator's own hierarchy on purpose. Walking up to transform.root
        /// would reach a shared scene container such as "Enemies" and suspend hand IK on
        /// every other character parented under it.
        /// </summary>
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
