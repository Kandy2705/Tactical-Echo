using System;
using TacticalEcho.Combat.Damage;
using TMPro;
using UnityEngine;

namespace TacticalEcho.UI
{
    public static class FloatingDamageNumberSystem
    {
        private const int PoolSize = 24;
        private const float Lifetime = 0.8f;
        private const float RiseSpeed = 0.65f;

        private sealed class Entry
        {
            public GameObject GameObject;
            public TextMeshPro Text;
            public float EndTime;
            public Vector3 Drift;
        }

        private static Entry[] pool;
        private static int nextIndex;
        private static Camera cachedCamera;

        public static void Show(in DamageFeedback feedback)
        {
            EnsurePool();
            if (pool == null || pool.Length == 0)
            {
                return;
            }

            Entry entry = pool[nextIndex];
            nextIndex = (nextIndex + 1) % pool.Length;

            int shownDamage = Mathf.Max(1, Mathf.RoundToInt(feedback.Amount));
            entry.Text.text = shownDamage.ToString();
            entry.Text.fontSize = feedback.IsCritical ? 4.8f : 3.6f;
            entry.Text.fontStyle = feedback.IsCritical ? FontStyles.Bold : FontStyles.Normal;
            entry.Text.color = feedback.IsCritical
                ? new Color(1f, 0.35f, 0.12f, 1f)
                : Color.white;

            Vector2 random = UnityEngine.Random.insideUnitCircle * 0.18f;
            entry.GameObject.transform.position = feedback.Point + new Vector3(random.x, 0.12f + Mathf.Abs(random.y) * 0.25f, random.y * 0.35f);
            entry.GameObject.transform.localScale = Vector3.one * (feedback.IsCritical ? 0.075f : 0.06f);
            entry.Drift = new Vector3(random.x * 0.7f, RiseSpeed, random.y * 0.3f);
            entry.EndTime = Time.time + Lifetime;
            entry.GameObject.SetActive(true);

            FloatingDamageNumberUpdater.EnsureExists();
        }

        internal static void Tick()
        {
            if (pool == null)
            {
                return;
            }

            if (cachedCamera == null)
            {
                cachedCamera = Camera.main;
            }

            float now = Time.time;
            foreach (Entry entry in pool)
            {
                if (entry == null || entry.GameObject == null || !entry.GameObject.activeSelf)
                {
                    continue;
                }

                float remaining = entry.EndTime - now;
                if (remaining <= 0f)
                {
                    entry.GameObject.SetActive(false);
                    continue;
                }

                entry.GameObject.transform.position += entry.Drift * Time.deltaTime;

                if (cachedCamera != null)
                {
                    Vector3 toCamera = entry.GameObject.transform.position - cachedCamera.transform.position;
                    if (toCamera.sqrMagnitude > 0.001f)
                    {
                        entry.GameObject.transform.rotation = Quaternion.LookRotation(toCamera.normalized, Vector3.up);
                    }
                }

                Color color = entry.Text.color;
                color.a = Mathf.Clamp01(remaining / Lifetime);
                entry.Text.color = color;
            }
        }

        private static void EnsurePool()
        {
            if (pool != null)
            {
                return;
            }

            pool = new Entry[PoolSize];
            nextIndex = 0;

            for (int i = 0; i < PoolSize; i++)
            {
                GameObject go = new($"FloatingDamage_{i:00}");
                go.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSave;

                TextMeshPro text = go.AddComponent<TextMeshPro>();
                text.alignment = TextAlignmentOptions.Center;
                text.enableWordWrapping = false;
                text.richText = false;
                text.sortingOrder = 250;
                text.text = string.Empty;

                go.SetActive(false);
                pool[i] = new Entry
                {
                    GameObject = go,
                    Text = text,
                    EndTime = 0f,
                    Drift = Vector3.zero
                };
            }
        }

        private sealed class FloatingDamageNumberUpdater : MonoBehaviour
        {
            private static FloatingDamageNumberUpdater instance;

            public static void EnsureExists()
            {
                if (instance != null)
                {
                    return;
                }

                GameObject updater = new("FloatingDamageNumberUpdater");
                updater.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSave;
                instance = updater.AddComponent<FloatingDamageNumberUpdater>();
            }

            private void Update()
            {
                Tick();
            }
        }
    }
}
