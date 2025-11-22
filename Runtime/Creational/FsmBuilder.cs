#nullable enable
using System;
using System.Runtime.CompilerServices;
using Unity.Collections.LowLevel.Unsafe;

namespace Refactor.Fsm
{
    public ref struct FsmBuilder<TState, TContext>
        where TState : struct, Enum
    {
        private IStateHandler<TState>?[] _states;
        private TState _initialState;
        private bool _initialStateSet;
        private TContext _context;
        private bool _contextSet;
        private IStackPolicy<TState>? _stackPolicy;

        public static FsmBuilder<TState, TContext> Create() => new()
        {
            _states          = new IStateHandler<TState>?[GetMaxEnumValue<TState>() + 1],
            _initialState    = default,
            _initialStateSet = false,
            _context         = default!,
            _contextSet      = false,
            _stackPolicy     = null,
        };

        public FsmBuilder<TState, TContext> With(TState state, IStateHandler<TState> handler)
        {
            var index = GetStateIndex(state);
            _states[index] = handler;

            if (!_initialStateSet)
            {
                _initialState = state;
                _initialStateSet = true;
            }

            return this;
        }

        public FsmBuilder<TState, TContext> WithContext(TContext context)
        {
            _context = context;
            _contextSet = true;
            return this;
        }

        public FsmBuilder<TState, TContext> WithStack(int capacity = 8)
        {
            _stackPolicy = new StackPolicy<TState>(capacity);
            return this;
        }

        public Fsm<TState, TContext> Build()
        {
            if (!_initialStateSet)
                throw new InvalidOperationException("No states registered.");
            if (!_contextSet)
                throw new InvalidOperationException("Context not set. Use .WithContext()");

            return new Fsm<TState, TContext>(_states!, _initialState, _context, _stackPolicy);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int GetStateIndex(TState state) => UnsafeUtility.As<TState, int>(ref state);

        private static int GetMaxEnumValue<T>() where T : struct, Enum
        {
            var values = Enum.GetValues(typeof(T));
            var max    = 0;
    
            for (var i = 0; i < values.Length; i++)
            {
                var value    = (T)values.GetValue(i)!;
                var intValue = UnsafeUtility.As<T, int>(ref value);
                if (intValue > max)
                    max = intValue;
            }
    
            return max;
        }
    }
}