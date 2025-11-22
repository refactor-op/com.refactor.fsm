# Refactor.Fsm

通过 Nullable 策略实现栈状态机的零开销抽象，使用共享上下文消除重复传参。

## 哲学

> **任何应用程序，本质上都是一个大型状态机。**

从游戏流程（Loading → Menu → Playing → GameOver），到 UI 导航（MainMenu → Settings → Graphics），再到 AI 行为（Idle → Chase → Attack），状态与转换无处不在。

而 GameFramework 的 `Procedure` 模块让我对这句话有了更深的感悟：**状态机是抽象流程控制的最佳实现方式。** 它将复杂的流程分解为清晰的状态，用转换逻辑串联起来，既简洁又可维护。

我首先研究了 QFramework 的 FSM 实现，核心思想是用委托（`Action`）表示状态逻辑，用字典管理状态。

这种设计非常直观，用起来也很舒服：

```csharp
fsm.AddState(GameState.Menu, () => {
    Debug.Log("Entered Menu");
    // 状态逻辑.
});
```

委托看似简洁，但在实际使用中会产生隐藏的分配：

```csharp
// ❌ 每次注册都会产生闭包.
fsm.AddState(GameState.Playing, () => {
    _player.Activate();    // 捕获 this.
    _enemyCount = 0;       // 捕获字段.
});

// 生成的代码类似:
class <>c__DisplayClass0 {  // 闭包类 (堆分配).
    public Player _player;
    public int _enemyCount;
    
    public void <AddState>b__0() {
        _player.Activate();
        _enemyCount = 0;
    }
}
```

**每个委托都是一个闭包对象（24+ bytes），注册 10 个状态就是 240+ bytes。**

为了解决闭包和语义问题，我转向了经典的接口方案：

```csharp
public interface IStateHandler<in TState>
{
    void OnEnter(TState state, TState fromState);
    void OnExit(TState state, TState toState);
}

public interface IUpdatableHandler<in TState> : IStateHandler<TState>
{
    void OnUpdate(TState state, float deltaTime);
}
```

有了 Handler 接口后，我遇到了新问题：**如何让某些功能可选？**

```csharp
// 场景 1: UI 导航 (需要栈).
MainMenu → Settings → Graphics
Graphics → Pop → Settings  // ✅ 需要返回.

// 场景 2: 游戏流程 (不需要栈).
Loading → Menu → Playing → GameOver  // ❌ 单向流转.
```

**第一个想法**：在 FSM 内部加一个 `Stack<State>`

```csharp
public class Fsm<TState>
{
    private Stack<TState>? _stack;  // 即使不用, 也占 8 bytes.
    
    public void Push(TState state)
    {
        if (_stack == null)  // 每次都判断.
            throw new Exception();
    }
}
```

不用栈的 FSM 也要付出内存成本，且每次调用 Push/Pop 都要运行时判断（性能损失）。

---

**第二个想法**：Policy-Based 设计

受前一个包 Pooling 的启发，我想：

> **能否通过泛型策略，在编译时决定有没有栈？**

```csharp
// 不需要栈.
Fsm<State, NoStackPolicy<State>> // NoStackPolicy = 空 struct (0 bytes).

// 需要栈.
Fsm<State, WithStackPolicy<State>> // WithStackPolicy = 有字段的 struct.
```

不用不付费，编译时决定（JIT 可以内联，消除分支），且零虚调用（struct 实现接口）。

于是我扩展了这个思路：

```csharp
Fsm<TState, TContext, TStackPolicy, TTransitionPolicy>
// TStackPolicy: 可选的栈.
// TTransitionPolicy: 可选的条件转换.
```

**看起来很"学院派"，但问题很快暴露。**

我设计了 TransitionPolicy，希望实现"自动条件转换"：

```csharp
fsm.When(AIState.Patrol, ctx => ctx.EnemyDistance < 10f, AIState.Chase);
```

但委托 `Func<TContext, bool>` 会导致：
1. **装箱**：`TContext` 是 struct，传入委托会装箱
2. **闭包**：捕获外部变量会产生闭包对象
3. **逻辑混乱**："条件检查"到底是轮询还是事件驱动？

**经过深入分析，我意识到：**

> **条件转换不适合作为 Policy，应该由外部事件驱动。**

```csharp
// ✅ 正确方式: 外部系统负责判断并发出事件.
void OnHealthChanged(float hp)
{
    if (hp < 0.3f && fsm.CurrentState == AIState.Chase)
        fsm.GoTo(AIState.Retreat);
}
```

**于是我删除了 TransitionPolicy，API 从 4 个泛型参数减少到 3 个。**

接着第二次质疑：NoStackPolicy 真的需要吗？

此时设计变成了：

```csharp
Fsm<TState, TContext, TStackPolicy>  // 3 个泛型参数.
```

> "NoStackPolicy 既然没有字段，实现的接口也是'没实现的'，那么我觉得其实可以优化掉（完全变成 null）。"

```csharp
public struct NoStackPolicy<TState> : IStackPolicy<TState>
{
    // ❌ 没有字段.
    // ❌ Push/Pop 直接抛异常.
    public void Push(...) => throw new Exception();
}
```

**为什么不直接用 nullable？**

```csharp
// ✅ 更简洁.
private IStackPolicy<TState>? _stackPolicy;  // null = 平面 FSM.

public void Push(TState state)
{
    if (_stackPolicy == null)  // <- 分支预测准确率极高.
        throw new Exception();
}
```

**我一开始的假设是**：Policy-Based 能避免分支，性能更好。

**但测量结果震惊了我。**

```
Policy-Based: 18.23 ns
Nullable:      6.50 ns <- 快了 64%！
```

**为什么 Nullable 可能更快？**

1. **更少的泛型参数**（2 vs 3）→ 更小的泛型实例化开销
2. **更简单的代码路径** → JIT 更容易内联
3. **分支预测准确率 > 99.9%** → nullable 检查几乎免费

彼时接触了 UE5 State Tree，对比后：

- 其状态继承可以通过 `AStateHandler : BStateHandler` 实现
- 分层状态机可以通过状态持有状态机实现多层状态机实现

## 使用

```csharp
public interface IStateHandler<in TState>
{
    // ...
}

public interface IUpdatableHandler<in TState, in TContext> : IStateHandler<TState>
{
    // ...
}

public interface ISuspendableHandler<in TState> : IStateHandler<TState>
{
    // ...
}

// 创建.
var fsm = FsmBuilder.Create<TState, TContext>()
    .With(state, handler)
    .WithContext(context)
    .WithStack(capacity)  // 可选.
    .Build();

// 操作.
fsm.GoTo(state); // 不可返回的转换.
fsm.Push(state); // 可返回的推栈.
fsm.Pop();       // 弹栈.

// 更新.
fsm.Update(Time.deltaTime, Time.time, Time.unscaledTime);

// 暂停/恢复.
fsm.Pause();
fsm.Resume();

// 查询.
fsm.CurrentState
fsm.IsPaused
```

### 共享上下文

```csharp
// 定义上下文（推荐 struct）
public struct AIContext
{
    public Transform Transform;
    public float HP;
    public Vector3 EnemyPosition;
}

// 传入 FSM
.WithContext(context)

// Handler 中访问
public void OnUpdate(AIState state, float dt, float scaled, float unscaled, AIContext ctx)
{
    ctx.Transform.position += velocity * dt;
}

// 外部修改
fsm.Context.HP = newHP;
```

### 栈状态机

```csharp
public enum UIState { MainMenu, Settings, Graphics, Audio }

var fsm = FsmBuilder.Create<UIState, EmptyContext>()
    .With(UIState.MainMenu, new MainMenuHandler())
    .With(UIState.Settings, new SettingsHandler())
    .WithContext(new EmptyContext())
    .WithStack(capacity: 8)  // 启用栈.
    .Build();

// 推入新状态 (可返回).
fsm.Push(UIState.Settings); // MainMenu -> Settings.
fsm.Push(UIState.Graphics); // Settings -> Graphics.

// 返回上一个状态.
fsm.Pop(); // Graphics -> Settings.
fsm.Pop(); // Settings -> MainMenu.
```

## 许可证

MIT License

## 贡献

欢迎 Issue 与 PR。