using TacticalEcho.AI.Brain;
using TacticalEcho.AI.States;
using TacticalEcho.AI.TacticalActions;
using TacticalEcho.Combat.Health;
using TMPro;
using UnityEngine;

namespace TacticalEcho.AI.Debugging
{
    
    
    
    
    public sealed class EnemyPerceptionDemoView : MonoBehaviour
    {
        [SerializeField, HideInInspector] private bool hasAuthoringContract;

        private EnemyBrain brain;
        private Health health;
        private TMP_Text statusText;
        private Transform labelRoot;
        private Camera mainCamera;
        private EnemyStateId previousState = (EnemyStateId)(-1);
        private TacticalActionId previousAction = (TacticalActionId)(-1);
        private int previousHealth = -1;
        private bool previousNavigationReady;
        private bool hasPreviousNavigationState;

        public bool HasAuthoringContract => hasAuthoringContract;

        public void Configure(EnemyBrain newBrain, Health newHealth, TMP_Text newStatusText, Transform newLabelRoot)
        {
            hasAuthoringContract = true;
            brain = newBrain;
            health = newHealth;
            statusText = newStatusText;
            labelRoot = newLabelRoot;

            if (Application.isPlaying) RefreshStatus(force: true);
            else ShowEditModeStatus();
        }

        private void Update()
        {
            if (!Application.isPlaying) return;
            FaceStatusTowardCamera();
            RefreshStatus(force: false);
        }

        private void FaceStatusTowardCamera()
        {
            if (mainCamera == null) mainCamera = Camera.main;
            if (mainCamera == null) return;

            Vector3 direction = labelRoot.position - mainCamera.transform.position;
            if (direction.sqrMagnitude > 0.001f) labelRoot.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        }

        private void ShowEditModeStatus()
        {
            float maxHealth = health.Max;
            statusText.text = $"AI TEST\nEDIT MODE\nTACTICAL CHECK ON PLAY\nHP -- / {maxHealth:0}";
            statusText.color = Color.white;
        }

        private void RefreshStatus(bool force)
        {
            int currentHealth = Mathf.CeilToInt(health.Current);
            bool navigationReady = brain.Movement.IsOnNavMesh;
            TacticalActionId currentAction = brain.TacticalEvaluator.LastDecision;

            if (!force
                && previousState == brain.CurrentState
                && previousAction == currentAction
                && previousHealth == currentHealth
                && hasPreviousNavigationState
                && previousNavigationReady == navigationReady)
            {
                return;
            }

            previousState = brain.CurrentState;
            previousAction = currentAction;
            previousHealth = currentHealth;
            previousNavigationReady = navigationReady;
            hasPreviousNavigationState = true;

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
                case EnemyStateId.Retreat:
                    stateDescription = "RETREAT";
                    stateColor = new Color(1f, 0.55f, 0.15f, 1f);
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

            string actionLine = brain.CurrentState == EnemyStateId.Combat
                ? $"ACTION {currentAction.ToString().ToUpperInvariant()}"
                : "ACTION --";
            string healthLine = $"HP {health.Current:0} / {health.Max:0}";
            string navigationLine = navigationReady ? "NAV READY" : "NAV NOT READY";

            statusText.text = $"AI TEST\n{stateDescription}\n{actionLine}\n{navigationLine}\n{healthLine}";
            statusText.color = stateColor;
        }
    }
}
