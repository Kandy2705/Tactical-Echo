using TacticalEcho.AI.Brain;
using TacticalEcho.AI.Memory;
using TacticalEcho.AI.Perception;
using TacticalEcho.AI.States;
using TacticalEcho.Character.Player;
using TacticalEcho.Combat.Health;
using TacticalEcho.Combat.Weapons;
using TMPro;
using UnityEngine;

namespace TacticalEcho.AI.Debugging
{
    /// <summary>
    /// Runtime-only debug harness for the sandbox scene.
    /// It reuses Kaia's existing player visual as a visible enemy so perception can be tested
    /// before a dedicated enemy art/prefab is available.
    /// </summary>
    public static class EnemyPerceptionDemoBootstrap
    {
        private const string SandboxSceneName = "TacticalEcho_Sandbox";
        private const string DemoEnemyName = "Enemy_PerceptionTest_Kaia";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void SpawnDemoEnemy()
        {
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != SandboxSceneName)
            {
                return;
            }

            if (Object.FindFirstObjectByType<EnemyPerceptionDemoView>() != null)
            {
                return;
            }

            PlayerVisualController playerVisual = Object.FindFirstObjectByType<PlayerVisualController>();
            if (playerVisual == null || playerVisual.VisualRoot == null)
            {
                Debug.LogWarning("[AI Perception Demo] Player visual was not found; demo enemy was not spawned.");
                return;
            }

            Transform player = playerVisual.transform;
            GameObject enemy = new(DemoEnemyName);
            enemy.transform.position = ResolveSpawnPosition(player);

            Vector3 facingAwayFromPlayer = enemy.transform.position - player.position;
            facingAwayFromPlayer.y = 0f;
            if (facingAwayFromPlayer.sqrMagnitude <= 0.001f)
            {
                facingAwayFromPlayer = -player.forward;
            }

            enemy.transform.rotation = Quaternion.LookRotation(facingAwayFromPlayer.normalized, Vector3.up);

            GameObject visualClone = Object.Instantiate(playerVisual.VisualRoot.gameObject, enemy.transform, false);
            visualClone.name = "Visual_Kaia_Enemy";
            visualClone.transform.localPosition = Vector3.zero;
            visualClone.transform.localRotation = Quaternion.identity;
            visualClone.transform.localScale = Vector3.one;

            // The clone only needs the rifle visually. Player input never drives this copy.
            foreach (WeaponController clonedWeapon in visualClone.GetComponentsInChildren<WeaponController>(true))
            {
                clonedWeapon.enabled = false;
            }

            CapsuleCollider bodyCollider = enemy.AddComponent<CapsuleCollider>();
            bodyCollider.height = 1.7f;
            bodyCollider.radius = 0.31f;
            bodyCollider.center = new Vector3(0f, 0.85f, 0f);

            Health health = enemy.AddComponent<Health>();
            HearingSensor hearing = enemy.AddComponent<HearingSensor>();
            EnemyMemory memory = enemy.AddComponent<EnemyMemory>();

            Transform eyeOrigin = new GameObject("EyeOrigin").transform;
            eyeOrigin.SetParent(enemy.transform, false);
            eyeOrigin.localPosition = new Vector3(0f, 1.55f, 0.05f);

            VisionSensor vision = enemy.AddComponent<VisionSensor>();
            vision.Configure(eyeOrigin, player, Physics.DefaultRaycastLayers, Physics.DefaultRaycastLayers);

            EnemyBrain brain = enemy.AddComponent<EnemyBrain>();
            brain.ConfigurePerception(vision, hearing, memory);
            brain.ConfigureExecution(null, null, health);

            EnemyAIGizmos gizmos = enemy.AddComponent<EnemyAIGizmos>();
            gizmos.Configure(brain);

            CreateStatusLabel(enemy.transform, out TMP_Text statusText, out Transform labelRoot);

            EnemyPerceptionDemoView view = enemy.AddComponent<EnemyPerceptionDemoView>();
            view.Configure(brain, health, statusText, labelRoot);

            Debug.Log(
                "[AI Perception Demo] Spawned Enemy_PerceptionTest_Kaia. " +
                "It starts facing away. Fire within rifle noise range: PATROL -> INVESTIGATE; " +
                "after turning and seeing the Player: -> COMBAT. Shoot it to validate the damage pipeline too.");
        }

        private static Vector3 ResolveSpawnPosition(Transform player)
        {
            Vector3 desired = player.position + player.forward * 9f + player.right * 2.5f;
            Vector3 rayOrigin = desired + Vector3.up * 15f;

            if (Physics.Raycast(
                    rayOrigin,
                    Vector3.down,
                    out RaycastHit groundHit,
                    40f,
                    Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore))
            {
                desired.y = groundHit.point.y;
            }
            else
            {
                desired.y = player.position.y;
            }

            return desired;
        }

        private static void CreateStatusLabel(
            Transform parent,
            out TMP_Text statusText,
            out Transform labelRoot)
        {
            GameObject canvasObject = new("AI_StatusCanvas", typeof(RectTransform));
            canvasObject.transform.SetParent(parent, false);

            RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
            canvasRect.localPosition = new Vector3(0f, 2.15f, 0f);
            canvasRect.localRotation = Quaternion.identity;
            canvasRect.localScale = Vector3.one * 0.005f;
            canvasRect.sizeDelta = new Vector2(360f, 110f);

            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 100;

            GameObject textObject = new("Status", typeof(RectTransform));
            textObject.transform.SetParent(canvasObject.transform, false);

            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
            text.text = "AI TEST\nPATROL";
            text.fontSize = 28f;
            text.alignment = TextAlignmentOptions.Center;
            text.enableWordWrapping = false;
            text.raycastTarget = false;
            text.color = Color.white;

            statusText = text;
            labelRoot = canvasObject.transform;
        }
    }

    /// <summary>
    /// Makes the perception result immediately visible without implementing combat/navigation early.
    /// Real sensing and memory still come from VisionSensor, HearingSensor and EnemyMemory.
    /// </summary>
    public sealed class EnemyPerceptionDemoView : MonoBehaviour
    {
        [SerializeField, Min(0.01f)] private float turnSharpness = 8f;

        private EnemyBrain brain;
        private Health health;
        private TMP_Text statusText;
        private Transform labelRoot;
        private Camera mainCamera;
        private EnemyStateId previousState = (EnemyStateId)(-1);
        private int previousHealth = -1;

        public void Configure(
            EnemyBrain newBrain,
            Health newHealth,
            TMP_Text newStatusText,
            Transform newLabelRoot)
        {
            brain = newBrain;
            health = newHealth;
            statusText = newStatusText;
            labelRoot = newLabelRoot;
            RefreshStatus(force: true);
        }

        private void Update()
        {
            if (brain == null)
            {
                return;
            }

            RotateTowardPerceivedPosition();
            FaceStatusTowardCamera();
            RefreshStatus(force: false);
        }

        private void RotateTowardPerceivedPosition()
        {
            Vector3 targetPosition;

            if (brain.CurrentState == EnemyStateId.Combat && brain.Vision != null && brain.Vision.VisibleTarget != null)
            {
                targetPosition = brain.Vision.VisibleTarget.position;
            }
            else if ((brain.CurrentState == EnemyStateId.Investigate || brain.CurrentState == EnemyStateId.Search)
                     && brain.Memory != null
                     && brain.Memory.HasKnownPosition)
            {
                targetPosition = brain.Memory.LastKnownPosition;
            }
            else
            {
                return;
            }

            Vector3 direction = targetPosition - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.001f)
            {
                return;
            }

            Quaternion desiredRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            float t = 1f - Mathf.Exp(-turnSharpness * Time.deltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, t);
        }

        private void FaceStatusTowardCamera()
        {
            if (labelRoot == null)
            {
                return;
            }

            if (mainCamera == null)
            {
                mainCamera = Camera.main;
            }

            if (mainCamera == null)
            {
                return;
            }

            Vector3 direction = labelRoot.position - mainCamera.transform.position;
            if (direction.sqrMagnitude > 0.001f)
            {
                labelRoot.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            }
        }

        private void RefreshStatus(bool force)
        {
            if (statusText == null || brain == null)
            {
                return;
            }

            int currentHealth = health != null ? Mathf.CeilToInt(health.Current) : 0;
            if (!force && previousState == brain.CurrentState && previousHealth == currentHealth)
            {
                return;
            }

            previousState = brain.CurrentState;
            previousHealth = currentHealth;

            string stateDescription;
            Color stateColor;

            switch (brain.CurrentState)
            {
                case EnemyStateId.Investigate:
                    stateDescription = "INVESTIGATE - HEARD SHOT";
                    stateColor = Color.yellow;
                    break;
                case EnemyStateId.Combat:
                    stateDescription = "COMBAT - SEES PLAYER";
                    stateColor = new Color(1f, 0.25f, 0.2f, 1f);
                    break;
                case EnemyStateId.Search:
                    stateDescription = "SEARCH - LAST KNOWN";
                    stateColor = Color.cyan;
                    break;
                case EnemyStateId.Dead:
                    stateDescription = "DEAD";
                    stateColor = Color.gray;
                    break;
                default:
                    stateDescription = "PATROL - NO CONTACT";
                    stateColor = Color.white;
                    break;
            }

            string healthLine = health != null
                ? $"HP {health.Current:0} / {health.Max:0}"
                : string.Empty;

            statusText.text = $"AI TEST\n{stateDescription}\n{healthLine}";
            statusText.color = stateColor;
        }
    }
}
