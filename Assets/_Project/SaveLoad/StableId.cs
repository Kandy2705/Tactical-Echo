using System;
using UnityEngine;

namespace TacticalEcho.SaveLoad
{
    public sealed class StableId : MonoBehaviour
    {
        [SerializeField] private string id;

        public string Id => id;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                id = Guid.NewGuid().ToString("N");
                UnityEditor.EditorUtility.SetDirty(this);
            }
        }
#endif
    }
}
