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
        public readonly struct State
        {
            public readonly TState Id;
            public readonly IEnterHandler<TState, TContext>? Enter;
            public readonly IExitHandler<TState, TContext>? Exit;
            public readonly IUpdatable<TContext>? Updatable;
            public readonly IFixedUpdatable<TContext>? FixedUpdatable;
            public readonly ILateUpdatable<TContext>? LateUpdatable;

            public State(TState id, object handler)
            {
                Id             = id;
                Enter          = handler as IEnterHandler<TState, TContext>;
                Exit           = handler as IExitHandler<TState, TContext>;
                Updatable      = handler as IUpdatable<TContext>;
                FixedUpdatable = handler as IFixedUpdatable<TContext>;
                LateUpdatable  = handler as ILateUpdatable<TContext>;
            }

            public bool HasAnyHandler =>
                Enter != null || Exit != null || 
                Updatable != null || FixedUpdatable != null || LateUpdatable != null;
        }

        private readonly State[] _states;

        private State _current;
        private TContext _context;
        private bool _isPaused;

        public ref readonly State CurrentState => ref _current;
        public ref TContext Context => ref _context;
        public bool IsPaused => _isPaused;

        #region Creational

        internal Fsm(State[] states, TState initialState, TContext context)
        {
            _states  = states;
            _context = context;

            var index = GetStateIndex(initialState);
            _current = _states[index];

            _current.Enter?.OnEnter(default, _context);
        }

        internal State[] GetStates()
        {
            var copy = new State[_states.Length];
            Array.Copy(_states, copy, _states.Length);
            return copy;
        }

        #endregion

        #region Control

        /// <summary>暂停 Update/FixedUpdate/LateUpdate 循环.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Pause() => _isPaused = true;

        /// <summary>恢复 Update/FixedUpdate/LateUpdate 循环.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Resume() => _isPaused = false;

        #endregion

        #region Transition

        /// <summary>
        /// 转换到新状态.
        /// <para>如果目标状态与当前状态相同, 不执行任何操作.</para>
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void GoTo(TState newState)
        {
            if (EqualityComparer<TState>.Default.Equals(_current.Id, newState))
                return;

            var newIndex = GetStateIndex(newState);
            var newStateData = _states[newIndex];
            var oldState = _current.Id;

            _current.Exit?.OnExit(newState, _context);

            _current = newStateData;

            _current.Enter?.OnEnter(oldState, _context);
        }

        /// <summary>
        /// 重新进入当前状态 (触发 Exit => Enter).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Reenter()
        {
            var state = _current.Id;
            _current.Exit?.OnExit(state, _context);
            _current.Enter?.OnEnter(state, _context);
        }

        #endregion

        #region Update

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Update()
        {
            if (_isPaused) return;
            _current.Updatable?.OnUpdate(_context);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void FixedUpdate()
        {
            if (_isPaused) return;
            _current.FixedUpdatable?.OnFixedUpdate(_context);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void LateUpdate()
        {
            if (_isPaused) return;
            _current.LateUpdatable?.OnLateUpdate(_context);
        }

        #endregion

        #region Indexer

        private static readonly Func<TState, int> _toIndex = CreateIndexer();

        private static Func<TState, int> CreateIndexer()
        {
            var underlying = Enum.GetUnderlyingType(typeof(TState));

            if (underlying == typeof(byte))   return state => UnsafeUtility.As<TState, byte>(ref state);
            if (underlying == typeof(sbyte))  return state => UnsafeUtility.As<TState, sbyte>(ref state);
            if (underlying == typeof(short))  return state => UnsafeUtility.As<TState, short>(ref state);
            if (underlying == typeof(ushort)) return state => UnsafeUtility.As<TState, ushort>(ref state);
            if (underlying == typeof(int))    return state => UnsafeUtility.As<TState, int>(ref state);
            if (underlying == typeof(uint))   return state => (int)UnsafeUtility.As<TState, uint>(ref state);

            throw new NotSupportedException(
                $"Enum {typeof(TState).Name} uses unsupported underlying type {underlying.Name}.");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int GetStateIndex(TState state) => _toIndex(state);

        #endregion
    }
}
