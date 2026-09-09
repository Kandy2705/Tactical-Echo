using System.Text;
using TacticalEcho.AI.Brain;
using TacticalEcho.AI.TacticalActions;
using TMPro;
using UnityEngine;

namespace TacticalEcho.DebugTools
{
    public sealed class AIDebugOverlay : MonoBehaviour
    {
        [SerializeField] private EnemyBrain observedBrain;
        [SerializeField] private TMP_Text outputText;

        private readonly StringBuilder builder = new();

        private void LateUpdate()
        {
            if (observedBrain == null || outputText == null)
            {
                return;
            }

            builder.Clear();
            builder.Append("State: ").AppendLine(observedBrain.CurrentState.ToString());

            if (observedBrain.TacticalEvaluator != null)
            {
                builder.Append("Action: ").AppendLine(observedBrain.TacticalEvaluator.LastDecision.ToString());
                foreach (KeyValuePair<TacticalActionId, float> pair in observedBrain.TacticalEvaluator.LastScores)
                {
                    builder.Append(pair.Key).Append(": ").AppendLine(pair.Value.ToString("0.00"));
                }
            }

            if (observedBrain.Memory != null && observedBrain.Memory.HasKnownPosition)
            {
                builder.Append("Memory confidence: ").AppendLine(observedBrain.Memory.Confidence.ToString("0.00"));
                builder.Append("Last known: ").AppendLine(observedBrain.Memory.LastKnownPosition.ToString("F1"));
            }

            outputText.text = builder.ToString();
        }
    }
}
