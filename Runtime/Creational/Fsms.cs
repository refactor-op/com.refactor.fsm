#nullable enable
using System;
using System.Buffers;
using System.Runtime.CompilerServices;

namespace Refactor.Fsm
{
    public static class Fsms
    {
        public static FsmBuilder<TState, TContext> Create<TState, TContext>()
            where TState : struct, Enum
            => new();

        public static FsmBuilder<TState, TContext> From<TState, TContext>(Fsm<TState, TContext> existing)
            where TState : struct, Enum
            => new(existing);
    }

    public ref struct FsmBuilder<TState, TContext>
        where TState : struct, Enum
    {
        private static readonly ArrayPool<Fsm<TState, TContext>.State> _pool =
            ArrayPool<Fsm<TState, TContext>.State>.Shared;

        private Fsm<TState, TContext>.State[]? _states;
        private int _stateCount;

        private TState _initialState;
        private bool _hasInitialState;
        private TContext? _context;

        internal FsmBuilder(Fsm<TState, TContext> existing)
        {
            var existingStates = existing.GetStates();
            _states = _pool.Rent(existingStates.Length);
            _stateCount = existingStates.Length;
            Array.Copy(existingStates, _states, existingStates.Length);

            _initialState = existing.CurrentState.Id;
            _hasInitialState = true;
            _context = existing.Context;
        }

        public FsmBuilder<TState, TContext> With(TState state, object handler)
        {
            EnsureCapacity();

            var index = GetStateIndex(state);
            _states![index] = new Fsm<TState, TContext>.State(state, handler);

            if (!_hasInitialState)
            {
                _initialState = state;
                _hasInitialState = true;
            }

            return this;
        }

        public FsmBuilder<TState, TContext> Without(TState state)
        {
            if (_states == null) return this;

            var index = GetStateIndex(state);
            if (index < _stateCount)
                _states[index] = default;

            return this;
        }

        public FsmBuilder<TState, TContext> StartWith(TState state)
        {
            _initialState = state;
            _hasInitialState = true;
            return this;
        }

        public FsmBuilder<TState, TContext> WithContext(TContext context)
        {
            _context = context;
            return this;
        }

        public Fsm<TState, TContext> Build()
        {
            if (_states == null)
                throw new InvalidOperationException("No states registered.");

            if (_context == null)
                throw new InvalidOperationException("Context not set.");

            var initialIndex = GetStateIndex(_initialState);
            if (initialIndex < 0 || initialIndex >= _stateCount)
                throw new InvalidOperationException($"Initial state {_initialState} index out of range.");
            if (!_states[initialIndex].HasAnyHandler)
                throw new InvalidOperationException($"Initial state {_initialState} has no handlers.");

            var resultStates = new Fsm<TState, TContext>.State[_stateCount];
            Array.Copy(_states, resultStates, _stateCount);
            _pool.Return(_states, clearArray: true);
            _states = null;
            return new Fsm<TState, TContext>(resultStates, _initialState, _context);
        }

        #region Private

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void EnsureCapacity()
        {
            if (_states != null) return;

            _stateCount = GetMaxEnumValue() + 1;
            _states = _pool.Rent(_stateCount);
            Array.Clear(_states, 0, _states.Length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int GetStateIndex(TState state) => Fsm<TState, TContext>.GetStateIndex(state);

        private static int GetMaxEnumValue()
        {
            var values = Enum.GetValues(typeof(TState));
            var max = 0;

            for (var i = 0; i < values.Length; i++)
            {
                var value = (TState)values.GetValue(i)!;
                var intValue = GetStateIndex(value);
                if (intValue > max)
                    max = intValue;
            }

            return max;
        }

        #endregion
    }
}
