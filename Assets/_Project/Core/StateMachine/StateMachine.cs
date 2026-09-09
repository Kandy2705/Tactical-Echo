using System;
using System.Collections.Generic;

namespace TacticalEcho.Core.StateMachine
{
    public sealed class StateMachine<TStateId> where TStateId : struct, Enum
    {
        private readonly Dictionary<TStateId, IState> states = new();

        public bool HasCurrentState { get; private set; }
        public TStateId CurrentId { get; private set; }
        public IState CurrentState { get; private set; }

        public void Register(TStateId id, IState state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            states[id] = state;
        }

        public bool ChangeState(TStateId nextId)
        {
            if (!states.TryGetValue(nextId, out IState nextState))
            {
                return false;
            }

            if (HasCurrentState && EqualityComparer<TStateId>.Default.Equals(CurrentId, nextId))
            {
                return true;
            }

            CurrentState?.Exit();
            CurrentId = nextId;
            CurrentState = nextState;
            HasCurrentState = true;
            CurrentState.Enter();
            return true;
        }

        public void Tick(float deltaTime)
        {
            CurrentState?.Tick(deltaTime);
        }
    }
}
