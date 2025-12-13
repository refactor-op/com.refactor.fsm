#nullable enable
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Collections.LowLevel.Unsafe;

namespace Refactor.Fsm
{
    public sealed class Fsm<TState, TContext>
        where TState : struct, Enum
    {
        public readonly struct StateInfo
        {
            public readonly IStateHandler<TState, TContext> Handler;
            public readonly IUpdatable<TContext>? Updatable;
            public readonly IFixedUpdatable<TContext>? FixedUpdatable;
            public readonly ISuspendable<TContext>? Suspendable;

            public StateInfo(IStateHandler<TState, TContext> handler)
            {
                Handler        = handler;
                Updatable      = handler as IUpdatable<TContext>;
                FixedUpdatable = handler as IFixedUpdatable<TContext>;
                Suspendable    = handler as ISuspendable<TContext>;
            }
        }

        private readonly StateInfo[] _states;
        private TState _currentState;
        private StateInfo _currentStateInfo;
        private TContext _context;
        private bool _isPaused;

        private readonly TState[]? _stack;
        private int _stackCount;

        public TState CurrentState => _currentState;
        public ref TContext Context => ref _context;
        public bool IsPaused => _isPaused;

        #region Creational

        internal Fsm(
            StateInfo[] states,
            TState initialState,
            TContext context,
            int? stackCapacity)
        {
            _states       = states;
            _currentState = initialState;
            _context      = context;
            
            if (stackCapacity.HasValue && stackCapacity.Value > 0)
            {
                _stack      = new TState[stackCapacity.Value];
                _stackCount = 0;
            }

            var index = GetStateIndex(initialState);
            _currentStateInfo = _states[index];
            
            // 注意: 第一次进入时, fromState 与 initialState相同.
            _currentStateInfo.Handler.OnEnter(initialState, _context);
        }
        
        internal StateInfo[] GetStates()
        {
            var copy = new StateInfo[_states.Length];
            Array.Copy(_states, copy, _states.Length);
            return copy;
        }

        internal int? GetStackCapacity() => _stack?.Length;

        #endregion

        public void Pause() => _isPaused = true;
        public void Resume() => _isPaused = false;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void GoTo(TState newState)
        {
            if (EqualityComparer<TState>.Default.Equals(_currentState, newState))
                return;

            var newIndex = GetStateIndex(newState);
            if (newIndex < 0 || newIndex >= _states.Length || _states[newIndex].Handler == null)
                throw new InvalidOperationException($"State {newState} is not registered.");

            var newStateInfo = _states[newIndex];
            var oldState = _currentState;

            _currentStateInfo.Handler.OnExit(newState, _context);
            
            _currentState = newState;
            _currentStateInfo = newStateInfo;
            
            _currentStateInfo.Handler.OnEnter(oldState, _context);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Push(TState newState)
        {
            if (_stack == null)
                throw new InvalidOperationException("Stack not enabled. Use .WithStack() to enable.");

            if (_stackCount >= _stack.Length)
                throw new InvalidOperationException($"Stack overflow (max: {_stack.Length})");

            // 挂起当前状态.
            if (_currentStateInfo.Suspendable != null)
                _currentStateInfo.Suspendable.OnSuspend(_context);
            else
                _currentStateInfo.Handler.OnExit(newState, _context);

            // 将当前状态压入栈.
            _stack[_stackCount++] = _currentState;

            // 进入新状态.
            var newIndex = GetStateIndex(newState);
            if (newIndex < 0 || newIndex >= _states.Length || _states[newIndex].Handler == null)
                throw new InvalidOperationException($"State {newState} is not registered.");

            var newStateInfo = _states[newIndex];
            var oldState = _currentState;

            _currentState = newState;
            _currentStateInfo = newStateInfo;
            
            _currentStateInfo.Handler.OnEnter(oldState, _context);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Pop()
        {
            if (_stack == null || _stackCount == 0)
                return;

            var previousState = _stack[--_stackCount];
            var currentState = _currentState;
            
            // 退出当前状态.
            _currentStateInfo.Handler.OnExit(previousState, _context);

            // 恢复前一个状态.
            _currentState = previousState;
            var prevIndex = GetStateIndex(previousState);
            _currentStateInfo = _states[prevIndex];

            if (_currentStateInfo.Suspendable != null)
                _currentStateInfo.Suspendable.OnResume(_context);
            else
                _currentStateInfo.Handler.OnEnter(currentState, _context);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Update(float deltaTime, float scaledTime, float unscaledTime)
        {
            if (_isPaused) return;

            _currentStateInfo.Updatable?.OnUpdate(deltaTime, scaledTime, unscaledTime, _context);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void FixedUpdate(float fixedDeltaTime, float fixedTime, float fixedUnscaledTime)
        {
            if (_isPaused) return;

            _currentStateInfo.FixedUpdatable?.OnFixedUpdate(fixedDeltaTime, fixedTime, fixedUnscaledTime, _context);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int GetStateIndex(TState state) => UnsafeUtility.As<TState, int>(ref state);
    }
}
