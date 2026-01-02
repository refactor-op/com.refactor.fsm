#nullable enable
using System;

namespace Refactor.Fsm
{
    public interface IEnterHandler<TState, TContext> where TState : struct, Enum
    {
        void OnEnter(TState fromState, TContext context);
    }

    public interface IExitHandler<TState, TContext> where TState : struct, Enum
    {
        void OnExit(TState toState, TContext context);
    }

    public interface IUpdatable<TContext>
    {
        void OnUpdate(TContext context);
    }

    public interface IFixedUpdatable<TContext>
    {
        void OnFixedUpdate(TContext context);
    }

    public interface ILateUpdatable<TContext>
    {
        void OnLateUpdate(TContext context);
    }
}
