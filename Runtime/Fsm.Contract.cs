using System;

namespace Refactor.Fsm
{
    public interface IStateHandler<TState, TContext> where TState : struct, Enum
    {
        void OnEnter(TState fromState, TContext context);
        void OnExit(TState toState, TContext context);
    }

    public interface IUpdatable<in TContext>
    {
        void OnUpdate(float deltaTime, float scaledTime, float unscaledTime, TContext context);
    }

    public interface IFixedUpdatable<in TContext>
    {
        void OnFixedUpdate(float fixedDeltaTime, float fixedTime, float fixedUnscaledTime, TContext context);
    }

    public interface ISuspendable<in TContext>
    {
        void OnSuspend(TContext context);
        void OnResume(TContext context);
    }
}
