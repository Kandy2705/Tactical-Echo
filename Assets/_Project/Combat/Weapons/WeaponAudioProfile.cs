using UnityEngine;

namespace TacticalEcho.Combat.Weapons
{
    [CreateAssetMenu(menuName = "Tactical Echo/Combat/Weapon Audio Profile", fileName = "WeaponAudioProfile")]
    public sealed class WeaponAudioProfile : ScriptableObject
    {
        [SerializeField] private AudioClip[] fireClips;
        [SerializeField] private AudioClip reloadStartClip;
        [SerializeField] private AudioClip reloadCompleteClip;

        public AudioClip ReloadStartClip => reloadStartClip;
        public AudioClip ReloadCompleteClip => reloadCompleteClip;

        public AudioClip GetRandomFireClip()
        {
            if (fireClips == null || fireClips.Length == 0)
            {
                return null;
            }

            if (fireClips.Length == 1)
            {
                return fireClips[0];
            }

            int startIndex = Random.Range(0, fireClips.Length);
            for (int offset = 0; offset < fireClips.Length; offset++)
            {
                AudioClip clip = fireClips[(startIndex + offset) % fireClips.Length];
                if (clip != null)
                {
                    return clip;
                }
            }

            return null;
        }
    }
}
