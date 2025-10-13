using System;
using System.Collections.Generic;
using Refactor.Core.Pool.Extra;

namespace Refactor.Core.Fsm
{
    /// <summary>
    /// 有限状态机.
    /// </summary>
    public struct Fsm<TEnum, TContext> : IDisposable
        where TEnum : unmanaged, Enum
    {
        private TEnum _current;
        private TEnum _previous;
        private StateCallbackTable<TContext> _currentCallbacks;
        private Dictionary<TEnum, StateCallbackTable<TContext>> _stateMap;

        public readonly TEnum Current => _current;
        public readonly TEnum Previous => _previous;

        public readonly bool IsIn(TEnum state) =>
            EqualityComparer<TEnum>.Default.Equals(_current, state);

        public readonly bool WasIn(TEnum state) =>
            EqualityComparer<TEnum>.Default.Equals(_previous, state);

        public void Register<TState>(TEnum stateEnum)
            where TState : struct, IState<TContext>
        {
            _stateMap            ??= DictionaryPool<TEnum, StateCallbackTable<TContext>>.Default.Rent();
            _stateMap[stateEnum] =   StateStorage<TState, TContext>.Callbacks;
        }

        public void Start(TEnum initialState, ref TContext context)
        {
            _current = initialState;
            _previous = default;

            if (_stateMap != null && _stateMap.TryGetValue(initialState, out var callbacks))
            {
                _currentCallbacks = callbacks;
                _currentCallbacks.OnEnter.Invoke(ref context);
            }
        }

        public void TransitionTo(TEnum nextState, ref TContext context)
        {
            if (EqualityComparer<TEnum>.Default.Equals(_current, nextState))
                return;

            _currentCallbacks.OnExit.Invoke(ref context);
            _previous = _current;
            _current = nextState;

            if (_stateMap != null && _stateMap.TryGetValue(nextState, out var callbacks))
                _currentCallbacks = callbacks;

            _currentCallbacks.OnEnter.Invoke(ref context);
        }

        public void Update(ref TContext context) => _currentCallbacks.OnUpdate.Invoke(ref context);
        public void FixedUpdate(ref TContext context) => _currentCallbacks.OnFixedUpdate.Invoke(ref context);
        public void LateUpdate(ref TContext context) => _currentCallbacks.OnLateUpdate.Invoke(ref context);

        /// <summary>
        /// 释放资源（归还 Dictionary 到池）
        /// </summary>
        public void Dispose()
        {
            if (_stateMap != null)
            {
                DictionaryPool<TEnum, StateCallbackTable<TContext>>.Default.Return(_stateMap);
                _stateMap = null;
            }
            
            _current = default;
            _previous = default;
            _currentCallbacks = default;
        }
    }
}