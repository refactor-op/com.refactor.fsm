using System;

namespace Refactor.Fsm
{
    public static class Fsms
    {
        public static FsmBuilder<TStateEnum, TContext> Create<TStateEnum, TContext>()
            where TStateEnum : unmanaged, Enum =>
            new();
    }
    
    public struct FsmBuilder<TEnum, TContext> where TEnum : unmanaged, Enum
    {
        private Fsm<TEnum, TContext> _fsm;

        public FsmBuilder<TEnum, TContext> With<TState>(TEnum stateEnum)
            where TState : struct, IState<TContext>
        {
            _fsm.Register<TState>(stateEnum);
            return this;
        }
        
        public Fsm<TEnum, TContext> Build() => _fsm;
    }
}