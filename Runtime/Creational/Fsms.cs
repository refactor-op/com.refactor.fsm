#nullable enable
using System;
using System.Runtime.CompilerServices;
using Unity.Collections.LowLevel.Unsafe;

namespace Refactor.Fsm
{
    public static class Fsms
    {
        public static InitialBuilder<TState, TContext> Create<TState, TContext>() where TState : struct, Enum
        {
            return new InitialBuilder<TState, TContext>
            {
                _states = new Fsm<TState, TContext>.StateInfo[InitialBuilder<TState, TContext>.GetMaxEnumValue<TState>() + 1],
                _initialState = default,
                _initialStateSet = false
            };
        }

        public static FsmBuilder<TState, TContext> From<TState, TContext>(Fsm<TState, TContext> existingFsm)
            where TState : struct, Enum
        {
            return new FsmBuilder<TState, TContext>(
                existingFsm.GetStates(),
                null,
                existingFsm.CurrentState,
                existingFsm.Context,
                existingFsm.GetStackCapacity()
            );
        }
    }

    public ref struct InitialBuilder<TState, TContext>
        where TState : struct, Enum
    {
        internal Fsm<TState, TContext>.StateInfo[] _states;
        internal TState _initialState;
        internal bool _initialStateSet;

        public InitialBuilder<TState, TContext> With(TState state, IStateHandler<TState, TContext> handler)
        {
            var index = GetStateIndex(state);
            _states[index] = new Fsm<TState, TContext>.StateInfo(handler);

            if (!_initialStateSet)
            {
                _initialState = state;
                _initialStateSet = true;
            }

            return this;
        }

        public ContextRequiredBuilder<TState, TContext> WithContext(TContext context)
        {
            if (!_initialStateSet)
                throw new InvalidOperationException("No states registered. Use .With() first.");

            return new ContextRequiredBuilder<TState, TContext>(_states, _initialState, context);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int GetStateIndex(TState state) => UnsafeUtility.As<TState, int>(ref state);

        internal static int GetMaxEnumValue<T>() where T : struct, Enum
        {
            var values = Enum.GetValues(typeof(T));
            var max = 0;
            for (var i = 0; i < values.Length; i++)
            {
                var value = (T)values.GetValue(i)!;
                var intValue = UnsafeUtility.As<T, int>(ref value);
                if (intValue > max)
                    max = intValue;
            }
            return max;
        }
    }

    public ref struct ContextRequiredBuilder<TState, TContext>
        where TState : struct, Enum
    {
        private Fsm<TState, TContext>.StateInfo[] _states;
        private TState _initialState;
        private TContext _context;
        private int? _stackCapacity;

        internal ContextRequiredBuilder(
            Fsm<TState, TContext>.StateInfo[] states,
            TState initialState,
            TContext context)
        {
            _states = states;
            _initialState = initialState;
            _context = context;
            _stackCapacity = null;
        }

        public ContextRequiredBuilder<TState, TContext> WithStack(int capacity = 8)
        {
            _stackCapacity = capacity;
            return this;
        }

        public Fsm<TState, TContext> Build()
        {
            return new Fsm<TState, TContext>(_states, _initialState, _context, _stackCapacity);
        }
    }

    public ref struct FsmBuilder<TState, TContext>
        where TState : struct, Enum
    {
        private Fsm<TState, TContext>.StateInfo[] _states;
        private System.Collections.Generic.HashSet<int>? _removedStates;
        private TState _initialState;
        private TContext _context;
        private int? _stackCapacity;

        internal FsmBuilder(
            Fsm<TState, TContext>.StateInfo[] states,
            System.Collections.Generic.HashSet<int>? removedStates,
            TState initialState,
            TContext context,
            int? stackCapacity)
        {
            _states = new Fsm<TState, TContext>.StateInfo[states.Length];
            Array.Copy(states, _states, states.Length);
            _removedStates = removedStates != null ? new System.Collections.Generic.HashSet<int>(removedStates) : null;
            _initialState = initialState;
            _context = context;
            _stackCapacity = stackCapacity;
        }

        public FsmBuilder<TState, TContext> With(TState state, IStateHandler<TState, TContext> handler)
        {
            var index = GetStateIndex(state);
            _states[index] = new Fsm<TState, TContext>.StateInfo(handler);
            if (_removedStates != null)
                _removedStates.Remove(index);
            return this;
        }
        
        public FsmBuilder<TState, TContext> Without(TState state)
        {
            var index = GetStateIndex(state);
            _states[index] = default;
            _removedStates ??= new System.Collections.Generic.HashSet<int>();
            _removedStates.Add(index);
            return this;
        }

        public FsmBuilder<TState, TContext> WithContext(TContext context)
        {
            _context = context;
            return this;
        }
        
        public FsmBuilder<TState, TContext> WithInitialState(TState state)
        {
            _initialState = state;
            return this;
        }

        public FsmBuilder<TState, TContext> WithStack(int capacity = 8)
        {
            _stackCapacity = capacity;
            return this;
        }

        public FsmBuilder<TState, TContext> WithoutStack()
        {
            _stackCapacity = null;
            return this;
        }

        public Fsm<TState, TContext> Build()
        {
            var index = GetStateIndex(_initialState);
            if (index < 0 || index >= _states.Length || _states[index].Handler == null)
            {
                if (_removedStates != null && _removedStates.Contains(index))
                    throw new InvalidOperationException($"Initial state {_initialState} is not registered. It was removed via .Without().");

                 throw new InvalidOperationException($"Initial state {_initialState} is not registered.");
            }

            return new Fsm<TState, TContext>(_states, _initialState, _context, _stackCapacity);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int GetStateIndex(TState state) => UnsafeUtility.As<TState, int>(ref state);
    }
}
