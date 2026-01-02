# Refactor.Fsm

接口驱动的零 GC 状态机。Update 比 Lambda 方案快 7 倍，创建分配少 2 倍。

## 哲学

> **任何应用程序，本质上都是一个大型的分层状态机。**

从游戏流程（Loading → Menu → Playing → GameOver），再到 AI 行为（Idle → Chase → Attack），状态与转换无处不在。

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
```

**每个委托都是一个闭包对象（24+ bytes），注册 10 个状态就是 240+ bytes。**

为了解决闭包和语义问题，我转向了经典的接口方案：

```csharp
public interface IStateHandler<TState>
{
    void OnEnter(TState state, TState fromState);
    void OnExit(TState state, TState toState);
}

public interface IUpdatableHandler<TState> : IStateHandler<TState>
{
    void OnUpdate(TState state, float deltaTime);
}
```

有了 Handler 接口后，我遇到了新问题：**如何让某些功能可选？**

```csharp
// 场景 1: UI (需要栈).
MainMenu → Settings → Graphics
Graphics → Settings → MainMenu // ✅ 需要返回.

// 场景 2: 游戏流程 (不需要栈).
Loading → Menu → Playing → GameOver // ❌ 单向流转.
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

不用栈的 FSM 也要付出内存成本，且每次调用 Push/Pop 都有性能损失。

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

但测量结果：

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

借此，一个基于状态栈的状态机诞生了，但后来，借助奥卡姆剃刀，我对设计进行了更激进的简化：

**删除状态栈！**

**问题**：
- 栈逻辑与 FSM 核心职责正交

**新设计**：删除内置栈，用户可以用装饰器或外部栈实现：

```csharp
// 用户自己管理栈.
var stack = new Stack<State>();
stack.Push(currentState);
fsm.GoTo(newState);

// 返回.
var previous = stack.Pop();
fsm.GoTo(previous);
```

进一步，我删除了 ISuspendable：

**原设计**：`ISuspendable` 接口提供 `OnSuspend`/`OnResume` 钩子。

**问题**：与 `OnEnter`/`OnExit` 语义重叠，Handler 需要判断"是 Push 还是 GoTo"。

**新设计**：通过 `fromState`/`toState` 参数，Handler 自行判断：

```csharp
public void OnExit(State toState, Context ctx)
{
    if (toState == State.Paused)
    {
        // 暂停逻辑 (类似 Suspend).
    }
    else
    {
        // 正常退出逻辑.
    }
}
```

继续删除 Update 钩子的时间参数：

**原设计**：`OnUpdate(float deltaTime, float scaledTime, float unscaledTime)`。

**问题**：
- 每次调用传递 3 个参数，增加调用开销
- Unity 已有 `Time.deltaTime` 静态访问

**新设计**：Handler 通过 `Time.deltaTime` 自行获取：

```csharp
public void OnUpdate(Context ctx)
{
    ctx.Transform.position += velocity * Time.deltaTime;
}
```

接着是拆分接口：

**原设计**：`IStateHandler` 强制实现 `OnEnter` + `OnExit`。

**问题**：很多状态只需要其中一个，强制实现导致空方法。

**新设计**：独立的可选接口：

```csharp
// 只需要 Enter.
class IdleHandler : IEnterHandler<State, Context>
{
    public void OnEnter(State from, Context ctx) { }
}

// 只需要 Update.
class MoveHandler : IUpdatable<Context>
{
    public void OnUpdate(Context ctx) { }
}
```

另外，还有 Handler 类型设计

**考虑过的方案**：

| 方案 | 优点 | 缺点 |
|------|------|------|
| `object handler` | API 简洁 | 无编译时类型检查 |
| `IStateHandler` 标记接口 | 类型安全 | 需要额外接口 |
| 泛型约束 | 完全类型安全 | 泛型爆炸 |
| struct Handler | 避免 GC | 接口存储会装箱 |

**Benchmark 结论**：

```
object (3x as 转换): 4ms / 10M
IStateHandler (3x as 转换): 7ms / 10M
```

`as` 转换极快（0.4ns/次），标记接口没有性能收益。最终选择 `object handler`。

于是最终接口：

```csharp
// 全部可选.
IEnterHandler<TState, TContext>   // void OnEnter(TState fromState, TContext ctx).
IExitHandler<TState, TContext>    // void OnExit(TState toState, TContext ctx).
IUpdatable<TContext>              // void OnUpdate(TContext ctx).
IFixedUpdatable<TContext>         // void OnFixedUpdate(TContext ctx).
ILateUpdatable<TContext>          // void OnLateUpdate(TContext ctx).
```

至此，Fsm 已无大碍，将目光转向 Builder，之前，Builder 使用 `new State[]` 分配数组，借助后一个包 Gas 的设计，选择使用 `ArrayPool<State>.Shared` 池化数组。

---

### Benchmark 验证

对比接口方案和 Lambda 方案（类似 QFramework/UnityHFSM）：

- **Update 快 13 倍**：接口方案在每帧调用的热路径上是明显优势（1.3ns 几乎等于直接调用开销）。
- **GoTo 慢 2 倍**：Lambda 方案的小字典查找在 CPU 缓存命中极高时非常快（~5ns），而 Struct 拷贝（32 bytes）带来了些许开销（~11ns）。考虑到状态切换频率远低于 Update，这是完全可接受的权衡。
- **创建 GC 少 2 倍**：接口方案每 FSM 只需 336 bytes，且无闭包隐患。

## 使用

### 接口

```csharp
// 进入状态时.
public interface IEnterHandler<TState, TContext>
{
    void OnEnter(TState fromState, TContext context);
}

// 退出状态时.
public interface IExitHandler<TState, TContext>
{
    void OnExit(TState toState, TContext context);
}

// Update 循环.
public interface IUpdatable<TContext>
{
    void OnUpdate(TContext context);
}

// FixedUpdate 循环.
public interface IFixedUpdatable<TContext>
{
    void OnFixedUpdate(TContext context);
}

// LateUpdate 循环.
public interface ILateUpdatable<TContext>
{
    void OnLateUpdate(TContext context);
}
```

### 创建与操作

```csharp
// 创建.
var fsm = Fsms.Create<TState, TContext>()
    .With(state, handler)
    .WithContext(context)
    .Build();

// 操作.
fsm.GoTo(state);   // 转换状态.
fsm.Reenter();     // 重新进入当前状态 (Exit → Enter).

// 更新.
fsm.Update();
fsm.FixedUpdate();
fsm.LateUpdate();

// 暂停/恢复.
fsm.Pause();
fsm.Resume();

// 查询.
fsm.CurrentState   // 当前状态.
fsm.Context        // 共享上下文.
fsm.IsPaused       // 是否暂停.
```

### 共享上下文

```csharp
// 定义上下文
public class PlayerContext
{
    public Transform Transform;
    public Animator Animator;
    public Fsm<PlayerState, PlayerContext> Fsm;  // 可持有 FSM 引用.
}

// Handler 中访问.
public void OnUpdate(PlayerContext ctx)
{
    if (ctx.Input.Move != Vector2.zero)
        ctx.Fsm.GoTo(PlayerState.Walk);
}

// 外部修改.
fsm.Context.HP = newHP;
```

### 状态增删改

```csharp
// 从现有 FSM 克隆并修改.
var derivedFsm = Fsms.From(existing)
    .With(State.NewState, new NewHandler())  // 添加.
    .Without(State.OldState)                  // 移除.
    .Build();
```

## 贡献

欢迎 Issue 与 PR。