using System.Collections.Generic;
using UnityEngine;

namespace TacticalEcho.AI.TacticalActions
{
    public sealed class TacticalEvaluator : MonoBehaviour
    {
        private readonly List<ITacticalAction> actions = new();
        private readonly Dictionary<TacticalActionId, float> lastScores = new();

        public TacticalActionId LastDecision { get; private set; } = TacticalActionId.None;
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

        public ITacticalAction Evaluate(in TacticalContext context)
        {
            ITacticalAction bestAction = null;
            float bestScore = 0f;
            lastScores.Clear();

            foreach (ITacticalAction action in actions)
            {
                if (!action.CanExecute(context))
                {
                    lastScores[action.Id] = 0f;
                    continue;
                }

                float score = Mathf.Max(0f, action.Score(context));
                lastScores[action.Id] = score;

                if (score <= bestScore)
                {
                    continue;
                }

                bestScore = score;
                bestAction = action;
            }

            LastDecision = bestAction?.Id ?? TacticalActionId.None;
            return bestAction;
        }
    }
}
