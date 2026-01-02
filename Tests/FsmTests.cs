using NUnit.Framework;

namespace Refactor.Fsm.Tests
{
    public class FsmTests
    {
        public enum State { Idle, Move, Attack, Dead }
        public class Context { public int Value; }

        private class SpyHandler : IEnterHandler<State, Context>,
                                   IExitHandler<State, Context>,
                                   IUpdatable<Context>,
                                   IFixedUpdatable<Context>,
                                   ILateUpdatable<Context>
        {
            public int EnterCount;
            public int ExitCount;
            public int UpdateCount;
            public int FixedUpdateCount;
            public int LateUpdateCount;

            public State LastEnteredFrom;
            public State LastExitedTo;

            public void OnEnter(State fromState, Context context)
            {
                EnterCount++;
                LastEnteredFrom = fromState;
            }

            public void OnExit(State toState, Context context)
            {
                ExitCount++;
                LastExitedTo = toState;
            }

            public void OnUpdate(Context context) => UpdateCount++;
            public void OnFixedUpdate(Context context) => FixedUpdateCount++;
            public void OnLateUpdate(Context context) => LateUpdateCount++;
        }

        private Context _context;
        private SpyHandler _idleHandler;
        private SpyHandler _moveHandler;

        [SetUp]
        public void Setup()
        {
            _context = new Context();
            _idleHandler = new SpyHandler();
            _moveHandler = new SpyHandler();
        }

        [Test]
        public void Build_SetsInitialState_And_CallsOnEnter()
        {
            // Arrange & Act
            var fsm = Fsms.Create<State, Context>()
                .With(State.Idle, _idleHandler)
                .WithContext(_context)
                .Build();

            // Assert
            Assert.AreEqual(State.Idle, fsm.CurrentState.Id);
            Assert.AreEqual(1, _idleHandler.EnterCount, "Should enter initial state immediately");
            Assert.AreEqual(default(State), _idleHandler.LastEnteredFrom, "Initial entry 'from' state should be default");
        }

        [Test]
        public void GoTo_ValidState_TransitionsCorrectly()
        {
            // Arrange
            var fsm = Fsms.Create<State, Context>()
                .With(State.Idle, _idleHandler)
                .With(State.Move, _moveHandler)
                .WithContext(_context)
                .Build();

            // Act
            fsm.GoTo(State.Move);

            // Assert
            Assert.AreEqual(State.Move, fsm.CurrentState.Id);

            // Check Idle Exit
            Assert.AreEqual(1, _idleHandler.ExitCount);
            Assert.AreEqual(State.Move, _idleHandler.LastExitedTo);

            // Check Move Enter
            Assert.AreEqual(1, _moveHandler.EnterCount);
            Assert.AreEqual(State.Idle, _moveHandler.LastEnteredFrom);
        }

        [Test]
        public void GoTo_SameState_DoesNothing()
        {
            // Arrange
            var fsm = Fsms.Create<State, Context>()
                .With(State.Idle, _idleHandler)
                .WithContext(_context)
                .Build();

            _idleHandler.EnterCount = 0; // Reset after init

            // Act
            fsm.GoTo(State.Idle);

            // Assert
            Assert.AreEqual(0, _idleHandler.ExitCount);
            Assert.AreEqual(0, _idleHandler.EnterCount);
        }

        [Test]
        public void Reenter_TriggersExitAndEnter()
        {
            // Arrange
            var fsm = Fsms.Create<State, Context>()
                .With(State.Idle, _idleHandler)
                .WithContext(_context)
                .Build();

            _idleHandler.EnterCount = 0; // Reset after init

            // Act
            fsm.Reenter();

            // Assert
            Assert.AreEqual(1, _idleHandler.ExitCount);
            Assert.AreEqual(1, _idleHandler.EnterCount);
            Assert.AreEqual(State.Idle, _idleHandler.LastEnteredFrom);
            Assert.AreEqual(State.Idle, _idleHandler.LastExitedTo);
        }

        [Test]
        public void Update_WhenRunning_InvokesHandler()
        {
            // Arrange
            var fsm = Fsms.Create<State, Context>()
                .With(State.Idle, _idleHandler)
                .WithContext(_context)
                .Build();

            // Act
            fsm.Update();
            fsm.FixedUpdate();
            fsm.LateUpdate();

            // Assert
            Assert.AreEqual(1, _idleHandler.UpdateCount);
            Assert.AreEqual(1, _idleHandler.FixedUpdateCount);
            Assert.AreEqual(1, _idleHandler.LateUpdateCount);
        }

        [Test]
        public void Update_WhenPaused_DoesNotInvokeHandler()
        {
            // Arrange
            var fsm = Fsms.Create<State, Context>()
                .With(State.Idle, _idleHandler)
                .WithContext(_context)
                .Build();

            // Act
            fsm.Pause();
            fsm.Update();

            // Assert
            Assert.AreEqual(0, _idleHandler.UpdateCount);
            Assert.IsTrue(fsm.IsPaused);

            // Act - Resume
            fsm.Resume();
            fsm.Update();

            // Assert
            Assert.AreEqual(1, _idleHandler.UpdateCount);
            Assert.IsFalse(fsm.IsPaused);
        }

        [Test]
        public void From_ClonesAndModifies()
        {
            // Arrange
            var fsm = Fsms.Create<State, Context>()
                .With(State.Idle, _idleHandler)
                .With(State.Move, _moveHandler)
                .WithContext(_context)
                .Build();

            // Create a derived FSM with Attack added
            var attackHandler = new SpyHandler();
            var derivedFsm = Fsms.From(fsm)
                .With(State.Attack, attackHandler)
                .Build();

            // Act
            derivedFsm.GoTo(State.Attack);

            // Assert
            Assert.AreEqual(State.Attack, derivedFsm.CurrentState.Id);
            Assert.AreEqual(1, attackHandler.EnterCount);
        }

        [Test]
        public void Without_RemovesState()
        {
            // Arrange
            var fsm = Fsms.Create<State, Context>()
                .With(State.Idle, _idleHandler)
                .With(State.Move, _moveHandler)
                .WithContext(_context)
                .Build();

            // Create a derived FSM with Move removed
            var derivedFsm = Fsms.From(fsm)
                .Without(State.Move)
                .Build();

            // Act - GoTo removed state should not have handler
            derivedFsm.GoTo(State.Move);

            // Assert - Move state has no handlers, so no callbacks
            // (Current design allows transition but no callbacks)
            Assert.AreEqual(State.Move, derivedFsm.CurrentState.Id);
        }

        [Test]
        public void Build_WithoutContext_ThrowsException()
        {
            // Arrange & Act & Assert
            Assert.Throws<System.InvalidOperationException>(() =>
            {
                Fsms.Create<State, Context>()
                    .With(State.Idle, _idleHandler)
                    .Build();
            });
        }

        [Test]
        public void Build_WithoutStates_ThrowsException()
        {
            // Arrange & Act & Assert
            Assert.Throws<System.InvalidOperationException>(() =>
            {
                Fsms.Create<State, Context>()
                    .WithContext(_context)
                    .Build();
            });
        }
    }
}
