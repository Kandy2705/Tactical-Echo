using System.Collections.Generic;
using System.Text;
using TacticalEcho.AI.Brain;
using TacticalEcho.AI.TacticalActions;
using UnityEngine;

namespace TacticalEcho.DebugTools
{
    
    
    
    
    public sealed class AIDebugOverlay : DebugOverlayBase
    {
        [Header("Observed")]
        [SerializeField] private EnemyBrain observedBrain;

        protected override string OverlayName => "AI Debug";

        public void Observe(EnemyBrain brain)
        {
            observedBrain = brain;
        }

        protected override void BuildText(StringBuilder text)
        {
            if (observedBrain == null)
            {
                text.AppendLine("no EnemyBrain observed");
                return;
            }

            text.Append("State: ").AppendLine(observedBrain.CurrentState.ToString());

            if (observedBrain.TacticalEvaluator != null)
            {
                TacticalEvaluator evaluator = observedBrain.TacticalEvaluator;
                text.Append("Selected: ")
                    .Append(evaluator.LastDecision)
                    .Append(" (")
                    .Append(evaluator.LastDecisionScore.ToString("0.00"))
                    .AppendLine(")");

                text.Append("Highest: ")
                    .Append(evaluator.LastHighestScoreAction)
                    .Append(" (")
                    .Append(evaluator.LastHighestScore.ToString("0.00"))
                    .AppendLine(")");

                foreach (KeyValuePair<TacticalActionId, float> pair in evaluator.LastScores)
                {
                    text.Append(pair.Key)
                        .Append(": ")
                        .AppendLine(pair.Value.ToString("0.00"));
                }
            }

            if (observedBrain.Memory != null && observedBrain.Memory.HasKnownPosition)
            {
                text.Append("Memory confidence: ")
                    .AppendLine(observedBrain.Memory.Confidence.ToString("0.00"));
                text.Append("Last known: ")
                    .AppendLine(observedBrain.Memory.LastKnownPosition.ToString("F1"));
            }
        }
    }
}
