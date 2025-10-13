namespace Refactor.Core.Fsm
{
    /// <summary>
    /// 状态接口.
    /// </summary>
    public interface IState<TContext>
    {
        void OnEnter(ref TContext context);
        void OnUpdate(ref TContext context);
        void OnFixedUpdate(ref TContext context);
        void OnLateUpdate(ref TContext context);
        void OnExit(ref TContext context);
    }

    /// <summary>
    /// 状态回调委托.
    /// </summary>
    internal delegate void StateCallback<TContext>(ref TContext context);

    /// <summary>
    /// 状态回调表.
    /// 存储一个状态内部的所有回调.
    /// </summary>
    internal readonly struct StateCallbackTable<TContext>
    {
        public readonly StateCallback<TContext> OnEnter;
        public readonly StateCallback<TContext> OnUpdate;
        public readonly StateCallback<TContext> OnFixedUpdate;
        public readonly StateCallback<TContext> OnLateUpdate;
        public readonly StateCallback<TContext> OnExit;

        public StateCallbackTable(
            StateCallback<TContext> onEnter,
            StateCallback<TContext> onUpdate,
            StateCallback<TContext> onFixedUpdate,
            StateCallback<TContext> onLateUpdate,
            StateCallback<TContext> onExit)
        {
            OnEnter = onEnter;
            OnUpdate = onUpdate;
            OnFixedUpdate = onFixedUpdate;
            OnLateUpdate = onLateUpdate;
            OnExit = onExit;
        }
    }

    /// <summary>
    /// 状态存储 (编译期泛型特化).
    /// </summary>
    internal static class StateStorage<TState, TContext>
        where TState : struct, IState<TContext>
    {
        public static readonly StateCallbackTable<TContext> Callbacks = new(
            onEnter: static (ref TContext ctx) => default(TState).OnEnter(ref ctx),
            onUpdate: static (ref TContext ctx) => default(TState).OnUpdate(ref ctx),
            onFixedUpdate: static (ref TContext ctx) => default(TState).OnFixedUpdate(ref ctx),
            onLateUpdate: static (ref TContext ctx) => default(TState).OnLateUpdate(ref ctx),
            onExit: static (ref TContext ctx) => default(TState).OnExit(ref ctx)
        );
    }
}