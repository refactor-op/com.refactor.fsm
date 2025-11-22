using System;
using System.Runtime.CompilerServices;

namespace Refactor.Fsm
{
    public interface IStateHandler<in TState> where TState : struct, Enum
    {
        void OnEnter(TState state, TState fromState);
        void OnExit(TState state, TState toState);
    }

    public interface IUpdatableHandler<in TState, in TContext> : IStateHandler<TState>
        where TState : struct, Enum
    {
        void OnUpdate(TState state, float deltaTime, float scaledTime, float unscaledTime, TContext context);
    }

    public interface IFixedUpdatableHandler<in TState, in TContext> : IStateHandler<TState>
        where TState : struct, Enum
    {
        void OnFixedUpdate(TState state, float fixedDeltaTime, float fixedTime, float fixedUnscaledTime, TContext context);
    }

    public interface ISuspendableHandler<in TState> : IStateHandler<TState> 
        where TState : struct, Enum
    {
        void OnSuspend(TState state);
        void OnResume(TState state);
    }
    
    public interface IStackPolicy<TState> where TState : struct, Enum
    {
        void Push(TState state, IStateHandler<TState> handler);
        bool TryPop(out TState state, out IStateHandler<TState> handler);
    }

    public sealed class StackPolicy<TState> : IStackPolicy<TState>
        where TState : struct, Enum
    {
        private struct StackEntry
        {
            public TState State;
            public IStateHandler<TState> Handler;
        }

        private readonly StackEntry[] _buffer;
        private int _count;

        public StackPolicy(int capacity)
        {
            _buffer = new StackEntry[capacity];
            _count  = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Push(TState state, IStateHandler<TState> handler)
        {
            if (_count >= _buffer.Length)
                throw new InvalidOperationException($"Stack overflow (max: {_buffer.Length})");

            _buffer[_count++] = new StackEntry { State = state, Handler = handler };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryPop(out TState state, out IStateHandler<TState> handler)
        {
            if (_count == 0)
            {
                state   = default;
                handler = null!;
                return false;
            }

            ref var entry = ref _buffer[--_count];
            state   = entry.State;
            handler = entry.Handler;
            return true;
        }
    }
}