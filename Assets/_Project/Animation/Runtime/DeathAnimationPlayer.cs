using System;
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
    public sealed class DeathAnimationPlayer : MonoBehaviour
    {
        private const string DeathClipResourcePath = "Death/Death_From_Front_Headshot";

        private static AnimationClip cachedDeathClip;
        private static bool warnedMissingClip;

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

        private bool PlayInternal(Animator animator)
        {
            AnimationClip clip = ResolveDeathClip();
            if (clip == null)
            {
                return false;
            }

            DisableWeaponIk(animator);

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

        private static void DisableWeaponIk(Animator animator)
        {
            Transform root = animator.transform.root;
            foreach (WeaponHandIKController ik in root.GetComponentsInChildren<WeaponHandIKController>(true))
            {
                ik.enabled = false;
            }
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
