<div align="center">
  <h1>Refactor Fsm</h1>
  <p>
    <img src="https://img.shields.io/badge/Unity-2021.3+-black?logo=unity" />
  </p>
</div>

## 为什么 Refactor.FSM

todo

## Benchmark

todo

## 快速开始

```csharp
using Refactor.Fsm;

// 1. 定义状态 Enum.
public enum PlayerState
{
    Idle,
    Run,
    Jump
}

// 2. 定义上下文.
public struct PlayerContext
{
    public float Speed;
    public bool IsGrounded;
}

// 3. 定义状态行为.
public struct IdleState : IState<PlayerContext>
{
    public void OnEnter(ref PlayerContext ctx) 
    { 
        ctx.Speed = 0f; 
    }
    
    public void OnUpdate(ref PlayerContext ctx) { }
    public void OnFixedUpdate(ref PlayerContext ctx) { }
    public void OnLateUpdate(ref PlayerContext ctx) { }
    public void OnExit(ref PlayerContext ctx) { }
}

public struct RunState : IState<PlayerContext>
{
    public void OnEnter(ref PlayerContext ctx) 
    { 
        ctx.Speed = 5f; 
    }
    
    public void OnUpdate(ref PlayerContext ctx) 
    { 
        // ... 移动逻辑
    }
    
    public void OnFixedUpdate(ref PlayerContext ctx) { }
    public void OnLateUpdate(ref PlayerContext ctx) { }
    public void OnExit(ref PlayerContext ctx) { }
}

// 4. 使用.
public class Player : IDisposable
{
    private Fsm<PlayerState, PlayerContext> _fsm;
    private PlayerContext _context;

    public void Initialize()
    {
        _fsm = Fsms.Create<PlayerState, PlayerContext>()
            .With<IdleState>(PlayerState.Idle)
            .With<RunState>(PlayerState.Run)
            .With<JumpState>(PlayerState.Jump)
            .Build();
        
        _fsm.Start(PlayerState.Idle, ref _context);
    }

    public void Update()
    {
        _fsm.Update(ref _context); // 手动在 Update 中驱动, 未来可能支持自动驱动.

        if (_fsm.IsIn(PlayerState.Idle) && Input.GetKey(KeyCode.W))
            _fsm.TransitionTo(PlayerState.Run, ref _context);
        
        if (_fsm.IsIn(PlayerState.Run) && Input.GetKeyDown(KeyCode.Space))
            _fsm.TransitionTo(PlayerState.Jump, ref _context);
    }

    public void Dispose()
    {
        _fsm.Dispose();  // 归还 Dictionary 到池
    }
}
```

## 心路历程

todo

## 贡献

欢迎 PR & Issue！

## 致谢

Refactor.Fsm 的设计受到以下开发者/项目的启发：

- **[Cysharp](https://github.com/Cysharp)**
- **[Ben Adams](https://github.com/benaadams)**
- **[QFramework](https://github.com/liangxiegame/QFramework)**

<div align="center">
  <p><i>Your time is limited, so don't waste it living someone else's life.</i></p>
</div>