using System.Collections.Generic;
using UnityEngine;

namespace TacticalEcho.AI.TacticalActions
{
    // Registers every ITacticalAction (see StandardTacticalActions.cs), scores them against a
    // TacticalContext on a fixed interval, and picks one - with hysteresis so the enemy does not flicker
    // between two similarly-scored actions every decision tick. Owns selection only; it never touches
    // Movement/Weapon/Animator itself, that is each action's own Execute().
    public sealed class TacticalEvaluator : MonoBehaviour
    {
        [Header("Decision Stability")]
        [SerializeField, Min(0f)] private float decisionInterval = 0.15f;
        // How much higher a new action's score must beat the current one before switching - prevents
        // rapid back-and-forth when two actions are nearly tied.
        [SerializeField, Range(0f, 0.5f)] private float switchThreshold = 0.08f;

        private readonly List<ITacticalAction> actions = new();
        private readonly Dictionary<TacticalActionId, float> lastScores = new();

        private ITacticalAction selectedAction;
        private float nextEvaluationTime;

        public TacticalActionId LastDecision { get; private set; } = TacticalActionId.None;
        public float LastDecisionScore { get; private set; }
        public TacticalActionId LastHighestScoreAction { get; private set; } = TacticalActionId.None;
        public float LastHighestScore { get; private set; }
        public IReadOnlyDictionary<TacticalActionId, float> LastScores => lastScores;

        private void Awake()
        {
            actions.Add(new ShootAction());
            actions.Add(new AdvanceAction());
            actions.Add(new TakeCoverAction());
            actions.Add(new RepositionAction());
            actions.Add(new ReloadAction());
            actions.Add(new RetreatAction());
        }

        public ITacticalAction Evaluate(in TacticalContext context, bool force = false)
        {
            bool selectedActionStillValid = selectedAction != null && selectedAction.CanExecute(context);
            if (!force && selectedActionStillValid && Time.time < nextEvaluationTime)
            {
                return selectedAction;
            }

            ITacticalAction highestScoreAction = null;
            float highestScore = 0f;
            lastScores.Clear();

            foreach (ITacticalAction action in actions)
            {
                float score = action.CanExecute(context)
                    ? Mathf.Clamp01(action.Score(context))
                    : 0f;

                lastScores[action.Id] = score;

                if (score <= highestScore)
                {
                    continue;
                }

                highestScore = score;
                highestScoreAction = action;
            }

            LastHighestScoreAction = highestScoreAction?.Id ?? TacticalActionId.None;
            LastHighestScore = highestScore;

            ITacticalAction nextAction = highestScoreAction;
            float nextScore = highestScore;

            // Sticky selection: keep the current action unless the challenger clears it by more than
            // switchThreshold, even if the challenger's raw score is technically higher.
            if (selectedActionStillValid
                && lastScores.TryGetValue(selectedAction.Id, out float selectedScore)
                && selectedScore > 0f
                && highestScoreAction != selectedAction
                && highestScore < selectedScore + switchThreshold)
            {
                nextAction = selectedAction;
                nextScore = selectedScore;
            }

            selectedAction = nextAction;
            LastDecision = selectedAction?.Id ?? TacticalActionId.None;
            LastDecisionScore = selectedAction != null ? nextScore : 0f;
            nextEvaluationTime = Time.time + decisionInterval;
            return selectedAction;
        }
    }
}
