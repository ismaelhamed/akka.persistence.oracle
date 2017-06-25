using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Configuration;
using Akka.Pattern;

namespace Benchmark
{
    internal class Program
    {
        // if you want to benchmark your persistent storage provides, paste the configuration in string below
        // by default we're checking against in-memory journal
        private static readonly Config Config = ConfigurationFactory.ParseString(@"
            akka {
                #loglevel = DEBUG
                suppress-json-serializer-warning = true
                persistence.journal {
                    plugin = ""akka.persistence.journal.oracle""
                    oracle {
                        class = ""Akka.Persistence.Oracle.Journal.BatchingOracleJournal, Akka.Persistence.Oracle""
                        plugin-dispatcher = ""akka.actor.default-dispatcher""
                        table-name = EVENTJOURNAL
                        schema-name = AKKA_PERSISTENCE_TEST
                        auto-initialize = on
                        connection-string-name = ""TestDb""
			            connection-timeout = 30s
                        refresh-interval = 1s
                        max-concurrent-operations = 64
                        max-batch-size = 100
                        circuit-breaker {
                            max-failures = 10
                            call-timeout = 30s
                            reset-timeout = 30s
                        }
                    }
                }
            }");

        public const int ActorCount = 1000;
        public const int MessagesPerActor = 100;

        static void Main(string[] args)
        {
            using (var system = ActorSystem.Create("persistent-benchmark", Config.WithFallback(ConfigurationFactory.Default())))
            {
                Console.WriteLine("Performance benchmark starting...");

                var stopwatch = new Stopwatch();

                var actors = new IActorRef[ActorCount];
                for (int i = 0; i < ActorCount; i++)
                {
                    var pid = "a-" + i;
                    actors[i] = system.ActorOf(Props.Create(() => new PerformanceTestActor(pid)));
                }

                stopwatch.Start();

                Task.WaitAll(actors.Select(a => a.Ask<Done>(Init.Instance)).Cast<Task>().ToArray());

                stopwatch.Stop();

                Console.WriteLine($"Initialized {ActorCount} eventsourced actors in {stopwatch.ElapsedMilliseconds / 1000.0} sec...");

                stopwatch.Start();

                for (int i = 0; i < MessagesPerActor; i++)
                    for (int j = 0; j < ActorCount; j++)
                    {
                        actors[j].Tell(new Store(1));
                    }

                var finished = new Task[ActorCount];
                for (int i = 0; i < ActorCount; i++)
                {
                    finished[i] = actors[i].Ask<Finished>(Finish.Instance);
                }

                Task.WaitAll(finished);

                stopwatch.Stop();
                var elapsed = stopwatch.ElapsedMilliseconds;

                Console.WriteLine($"{ActorCount} actors stored {MessagesPerActor} events each in {elapsed / 1000.0} sec. Average: {ActorCount * MessagesPerActor * 1000.0 / elapsed} events/sec");

                foreach (Task<Finished> task in finished)
                {
                    if (!task.IsCompleted || task.Result.State != MessagesPerActor)
                        throw new IllegalStateException("Actor's state was invalid");
                }
            }

            Console.WriteLine("Press Enter to exit...");
            Console.ReadLine();
        }
    }
}
