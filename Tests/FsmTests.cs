using System;
using NUnit.Framework;
using Refactor.Fsm;

namespace Refactor.Fsm.Tests
{
    public class FsmTests
    {
        public enum State { Idle, Move, Attack, Dead }
        public class Context { public int Value; }

        private class SpyHandler : IStateHandler<State, Context>, 
                                   IUpdatable<Context>, 
                                   IFixedUpdatable<Context>, 
                                   ISuspendable<Context>
        {
            public int EnterCount;
            public int ExitCount;
            public int UpdateCount;
            public int FixedUpdateCount;
            public int SuspendCount;
            public int ResumeCount;

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

            public void OnUpdate(float dt, float st, float ust, Context context) => UpdateCount++;
            public void OnFixedUpdate(float fdt, float ft, float fust, Context context) => FixedUpdateCount++;
            public void OnSuspend(Context context) => SuspendCount++;
            public void OnResume(Context context) => ResumeCount++;
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
            Assert.AreEqual(State.Idle, fsm.CurrentState);
            Assert.AreEqual(1, _idleHandler.EnterCount, "Should enter initial state immediately");
            Assert.AreEqual(State.Idle, _idleHandler.LastEnteredFrom, "Initial entry 'from' state should be itself");
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
            Assert.AreEqual(State.Move, fsm.CurrentState);
            
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
        public void GoTo_UnregisteredState_ThrowsException()
        {
            // Arrange
            var fsm = Fsms.Create<State, Context>()
                .With(State.Idle, _idleHandler)
                .WithContext(_context)
                .Build();

            // Act & Assert
            var ex = Assert.Throws<InvalidOperationException>(() => fsm.GoTo(State.Dead));
            Assert.That(ex.Message, Does.Contain("not registered"));
        }

        [Test]
        public void GoTo_RemovedState_ThrowsDetailedException()
        {
            // Arrange
            var fsm = Fsms.Create<State, Context>()
                .With(State.Idle, _idleHandler)
                .With(State.Dead, new SpyHandler()) // Initially added
                .WithContext(_context)
                .Build();

            // Create a derived FSM with Dead removed
            var derivedFsmBuilder = Fsms.From(fsm)
                .Without(State.Dead);
                
            var derivedFsm = derivedFsmBuilder.Build();

            // Act & Assert
            var ex = Assert.Throws<InvalidOperationException>(() => derivedFsm.GoTo(State.Dead));
            Assert.That(ex.Message, Does.Contain("removed via .Without"), "Should provide helpful error message for removed states");
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
            fsm.Update(1f, 1f, 1f);
            fsm.FixedUpdate(1f, 1f, 1f);

            // Assert
            Assert.AreEqual(1, _idleHandler.UpdateCount);
            Assert.AreEqual(1, _idleHandler.FixedUpdateCount);
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
            fsm.Update(1f, 1f, 1f);
            
            // Assert
            Assert.AreEqual(0, _idleHandler.UpdateCount);
            Assert.IsTrue(fsm.IsPaused);

            // Act - Resume
            fsm.Resume();
            fsm.Update(1f, 1f, 1f);
            
            // Assert
            Assert.AreEqual(1, _idleHandler.UpdateCount);
            Assert.IsFalse(fsm.IsPaused);
        }

        [Test]
        public void Stack_Push_SuspendsCurrent_EntersNew()
        {
            // Arrange
            var fsm = Fsms.Create<State, Context>()
                .With(State.Idle, _idleHandler)
                .With(State.Move, _moveHandler)
                .WithContext(_context)
                .WithStack()
                .Build();

            // Act
            fsm.Push(State.Move);

            // Assert
            Assert.AreEqual(State.Move, fsm.CurrentState);
            Assert.AreEqual(1, _idleHandler.SuspendCount, "Push should Suspend previous state");
            Assert.AreEqual(0, _idleHandler.ExitCount, "Push should NOT Exit previous state if it is Suspendable");
            Assert.AreEqual(1, _moveHandler.EnterCount, "Should Enter new state");
        }

        [Test]
        public void Stack_Pop_ExitsCurrent_ResumesPrevious()
        {
            // Arrange
            var fsm = Fsms.Create<State, Context>()
                .With(State.Idle, _idleHandler)
                .With(State.Move, _moveHandler)
                .WithContext(_context)
                .WithStack()
                .Build();

            fsm.Push(State.Move); // Stack: [Idle], Current: Move

            // Act
            fsm.Pop(); // Should go back to Idle

            // Assert
            Assert.AreEqual(State.Idle, fsm.CurrentState);
            Assert.AreEqual(1, _moveHandler.ExitCount, "Should Exit popped state");
            Assert.AreEqual(1, _idleHandler.ResumeCount, "Should Resume previous state");
            Assert.AreEqual(1, _idleHandler.EnterCount, "Should NOT re-Enter previous state (Resume instead)");
        }

        [Test]
        public void Stack_WithoutStackEnabled_ThrowsException()
        {
            // Arrange
            var fsm = Fsms.Create<State, Context>()
                .With(State.Idle, _idleHandler)
                .WithContext(_context)
                .WithoutStack() // Explicitly disabled
                .Build();

            // Act & Assert
            var ex = Assert.Throws<InvalidOperationException>(() => fsm.Push(State.Idle));
            Assert.That(ex.Message, Does.Contain("Stack not enabled"));
        }
    }
}
