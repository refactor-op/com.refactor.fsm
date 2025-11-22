#nullable enable
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Collections.LowLevel.Unsafe;

namespace Refactor.Fsm
{
    public sealed class Fsm<TState, TContext> : IDisposable
        where TState : struct, Enum
    {
        private readonly IStateHandler<TState>[] _states;
        private TState _currentState;
        private IStateHandler<TState> _currentHandler;
        private TContext _context;
        private bool _isPaused;
        private IStackPolicy<TState>? _stackPolicy;

        public TState CurrentState => _currentState;
        public ref TContext Context => ref _context;
        public bool IsPaused => _isPaused;

        internal Fsm(
            IStateHandler<TState>[] states,
            TState initialState,
            TContext context,
            IStackPolicy<TState>? stackPolicy)
        {
            _states = states;
            _currentState = initialState;
            _context = context;
            _stackPolicy = stackPolicy;

            var index = GetStateIndex(initialState);
            _currentHandler = _states[index];
            _currentHandler.OnEnter(initialState, initialState);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void GoTo(TState newState)
        {
            if (EqualityComparer<TState>.Default.Equals(_currentState, newState))
                return;

            var newIndex = GetStateIndex(newState);
            var newHandler = _states[newIndex];
            var oldState = _currentState;

            _currentHandler.OnExit(oldState, newState);
            _currentState = newState;
            _currentHandler = newHandler;
            newHandler.OnEnter(newState, oldState);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Push(TState newState)
        {
            if (_stackPolicy == null)
                throw new InvalidOperationException("Stack not enabled. Use .WithStack() to enable.");

            if (_currentHandler is ISuspendableHandler<TState> suspendable)
                suspendable.OnSuspend(_currentState);
            else
                _currentHandler.OnExit(_currentState, newState);

            _stackPolicy.Push(_currentState, _currentHandler);

            var newIndex = GetStateIndex(newState);
            var newHandler = _states[newIndex];
            var oldState = _currentState;

            _currentState = newState;
            _currentHandler = newHandler;
            newHandler.OnEnter(newState, oldState);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Pop()
        {
            if (_stackPolicy == null || !_stackPolicy.TryPop(out var previousState, out var previousHandler))
                return;

            var currentState = _currentState;
            _currentHandler.OnExit(currentState, previousState);
            _currentState = previousState;
            _currentHandler = previousHandler;

            if (previousHandler is ISuspendableHandler<TState> suspendable)
                suspendable.OnResume(previousState);
            else
                previousHandler.OnEnter(previousState, currentState);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Update(float deltaTime, float scaledTime, float unscaledTime)
        {
            if (_isPaused) return;

            if (_currentHandler is IUpdatableHandler<TState, TContext> updatable)
                updatable.OnUpdate(_currentState, deltaTime, scaledTime, unscaledTime, _context);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void FixedUpdate(float fixedDeltaTime, float fixedTime, float fixedUnscaledTime)
        {
            if (_isPaused) return;

            if (_currentHandler is IFixedUpdatableHandler<TState, TContext> fixedUpdatable)
                fixedUpdatable.OnFixedUpdate(_currentState, fixedDeltaTime, fixedTime, fixedUnscaledTime, _context);
        }

        public void Pause() => _isPaused = true;
        public void Resume() => _isPaused = false;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int GetStateIndex(TState state) => UnsafeUtility.As<TState, int>(ref state);

        public void Dispose() => _currentHandler.OnExit(_currentState, default);
    }
}