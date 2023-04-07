using Akka.Actor;
using Akka.Persistence;

namespace Benchmark
{
    public sealed class Init
    {
        public static readonly Init Instance = new Init();
        private Init() { }
    }

    public sealed class Finish
    {
        public static readonly Finish Instance = new Finish();
        private Finish() { }
    }

    public sealed class Done
    {
        public static readonly Done Instance = new Done();
        private Done() { }
    }

    public sealed class Finished
    {
        public readonly long State;

        public Finished(long state)
        {
            State = state;
        }
    }

    public sealed class Store
    {
        public readonly int Value;

        public Store(int value)
        {
            Value = value;
        }
    }

    public sealed class Stored
    {
        public readonly int Value;

        public Stored(int value)
        {
            Value = value;
        }
    }

    public class PerformanceTestActor : PersistentActor
    {
        private long state;

        public PerformanceTestActor(string persistenceId)
        {
            PersistenceId = persistenceId;
        }

        public sealed override string PersistenceId { get; }

        protected override bool ReceiveRecover(object message)
        {
            if (message is Stored stored)
            {
                state += stored.Value;
                return true;
            }
            return false;
        }

        protected override bool ReceiveCommand(object message)
        {
            switch (message)
            {
                case Store store:
                    Persist(new Stored(store.Value), stored => state += stored.Value);
                    return true;
                case Init _:
                    Persist(new Stored(0), stored =>
                    {
                        state += stored.Value;
                        Sender.Tell(Done.Instance);
                    });
                    return true;
                case Finish _:
                    Sender.Tell(new Finished(state));
                    return true;
            }
            return false;
        }
    }
}