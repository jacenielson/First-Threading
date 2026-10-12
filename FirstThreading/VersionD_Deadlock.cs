// Version D: Deadlock, and how to fix it with consistent lock ordering.
// Two accounts, each with its own lock. Main + two worker threads:
//   Alice transfers Checking -> Savings
//   Bob   transfers Savings  -> Checking   (at the same moment)
// Each transfer must lock BOTH accounts. What could go wrong?

using System;
using System.Threading;

namespace VersionD
{
    class Account
    {
        public int Id { get; }
        public string Name { get; }
        public int Balance { get; set; }
        public readonly object Lock = new object();

        public Account(int id, string name, int balance)
        {
            Id = id;
            Name = name;
            Balance = balance;
        }
    }

    class Program
    {
        static readonly TimeSpan Timeout = TimeSpan.FromSeconds(3);

        static void Main()
        {
            Thread.CurrentThread.Name = "Main";

            RunScenario("SCENARIO 1: each thread locks 'from' first, then 'to'", TransferDeadlockProne);
            Console.WriteLine();
            RunScenario("SCENARIO 2: every thread locks the lower account Id first", TransferOrdered);
        }

        // ---------- BROKEN: lock order depends on transfer direction.
        // Alice holds Checking and wants Savings; Bob holds Savings and wants Checking.
        // Neither will ever let go. That's a deadlock.
        static void TransferDeadlockProne(Account from, Account to, int amount)
        {
            Log($"wants to move ${amount} from {from.Name} to {to.Name}");

            lock (from.Lock)
            {
                Log($"LOCKED {from.Name}. Now waiting for {to.Name}...");
                Thread.Sleep(100); // Widen the window so the deadlock happens every time in class.

                lock (to.Lock)
                {
                    from.Balance -= amount;
                    to.Balance += amount;
                    Log($"transfer complete");
                }
            }
        }

        // ---------- FIXED: everyone agrees on one global order (lowest Id first).
        // Whoever gets the first lock is guaranteed to be able to get the second,
        // because nobody can be holding the second while waiting for the first.
        static void TransferOrdered(Account from, Account to, int amount)
        {
            Log($"wants to move ${amount} from {from.Name} to {to.Name}");

            Account first = from.Id < to.Id ? from : to;
            Account second = from.Id < to.Id ? to : from;

            lock (first.Lock)
            {
                Log($"LOCKED {first.Name}. Now waiting for {second.Name}...");
                Thread.Sleep(100); // Same delay as before. It no longer matters.

                lock (second.Lock)
                {
                    from.Balance -= amount;
                    to.Balance += amount;
                    Log($"transfer complete");
                }
            }
        }

        static void RunScenario(string title, Action<Account, Account, int> transfer)
        {
            Console.WriteLine(new string('=', 70));
            Console.WriteLine(title);
            Console.WriteLine(new string('=', 70));

            // Fresh accounts each scenario. (A deadlocked thread holds its locks forever!)
            var checking = new Account(1, "Checking", 500);
            var savings = new Account(2, "Savings", 500);

            // IsBackground = true lets the program exit even if these threads are stuck forever.
            var alice = new Thread(() => transfer(checking, savings, 100)) { Name = "Alice", IsBackground = true };
            var bob = new Thread(() => transfer(savings, checking, 50)) { Name = "Bob", IsBackground = true };

            alice.Start();
            bob.Start();

            // The main thread acts as a watchdog: wait a while, then check on the workers.
            bool aliceDone = alice.Join(Timeout);
            bool bobDone = bob.Join(aliceDone ? Timeout : TimeSpan.Zero);

            if (aliceDone && bobDone)
            {
                Log($"both transfers finished. Checking = ${checking.Balance}, Savings = ${savings.Balance}, " +
                    $"Total = ${checking.Balance + savings.Balance}");
            }
            else
            {
                Log($"DEADLOCK! After {Timeout.TotalSeconds} seconds, " +
                    $"Alice finished: {aliceDone}, Bob finished: {bobDone}.");
                Log("Each worker is holding one lock and waiting forever for the other.");
            }
        }

        static void Log(string message)
        {
            Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff}  [{Thread.CurrentThread.Name,-5}] {message}");
        }
    }
}
