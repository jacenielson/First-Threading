// Version B: Fixing the race with lock, and measuring what it costs.
// Three threads: Main (the bank manager) + two ATM worker threads.
// ATM 1 deposits $1 ten million times; ATM 2 withdraws $1 ten million times.
// The balance should end exactly where it started.

using System;
using System.Diagnostics;
using System.Threading;

class Program
{
    const int Iterations = 10_000_000;
    const int StartingBalance = 1000;

    static int balance;

    // The lock object: private, static, readonly, and used for nothing else.
    // (Never lock on 'this', a Type, or a string; outside code could lock on those too.)
    private static readonly object balanceLock = new object();

    static void Main()
    {
        Thread.CurrentThread.Name = "Main";
        Console.WriteLine($"[{Thread.CurrentThread.Name}] thread id {Thread.CurrentThread.ManagedThreadId} is opening the bank.");
        Console.WriteLine($"Each trial: start at ${StartingBalance}, {Iterations:N0} deposits vs {Iterations:N0} withdrawals.\n");

        RunTrial("1) No lock (broken)",               DepositNoLock,     WithdrawNoLock);
        RunTrial("2) Fine-grained lock (inside loop)", DepositFineLock,   WithdrawFineLock);
        RunTrial("3) Coarse-grained lock (whole loop)",DepositCoarseLock, WithdrawCoarseLock);

        Console.WriteLine("\nDiscussion: Why is #3 fast AND correct? What did we give up to get that?");
    }

    // ---------- 1) No lock: balance++ is read, add, write. Two threads interleave and lose updates.
    static void DepositNoLock()
    {
        for (int i = 0; i < Iterations; i++)
            balance++;
    }

    static void WithdrawNoLock()
    {
        for (int i = 0; i < Iterations; i++)
            balance--;
    }

    // ---------- 2) Fine-grained: lock around each single update. Correct, but pays the lock cost 2 million times.
    static void DepositFineLock()
    {
        for (int i = 0; i < Iterations; i++)
        {
            lock (balanceLock)
            {
                balance++;
            }
        }
    }

    static void WithdrawFineLock()
    {
        for (int i = 0; i < Iterations; i++)
        {
            lock (balanceLock)
            {
                balance--;
            }
        }
    }

    // ---------- 3) Coarse-grained: lock around the whole loop. Correct and fast,
    // but the ATMs now take turns completely. There is no real concurrency left.
    static void DepositCoarseLock()
    {
        lock (balanceLock)
        {
            for (int i = 0; i < Iterations; i++)
                balance++;
        }
    }

    static void WithdrawCoarseLock()
    {
        lock (balanceLock)
        {
            for (int i = 0; i < Iterations; i++)
                balance--;
        }
    }

    // ---------- The main thread starts both ATMs, waits for them, and audits the result.
    static void RunTrial(string label, ThreadStart depositWork, ThreadStart withdrawWork)
    {
        balance = StartingBalance;

        // The "starting gun": both ATMs wait at the gate so they truly run at the same time.
        // Without it, ATM 1 can finish before ATM 2 even starts, and the race hides.
        using var startingGun = new ManualResetEventSlim(false);

        var atm1 = new Thread(() => { startingGun.Wait(); depositWork(); })  { Name = "ATM 1 (deposits)" };
        var atm2 = new Thread(() => { startingGun.Wait(); withdrawWork(); }) { Name = "ATM 2 (withdrawals)" };

        var stopwatch = Stopwatch.StartNew();
        atm1.Start();
        atm2.Start();
        Thread.Sleep(50);   // let both threads reach the gate
        stopwatch.Restart();
        startingGun.Set();  // GO!
        atm1.Join();   // Main waits here until ATM 1 is done...
        atm2.Join();   // ...and here until ATM 2 is done.
        stopwatch.Stop();

        string verdict = balance == StartingBalance
            ? "correct"
            : $"WRONG, off by {balance - StartingBalance:N0}";

        Console.WriteLine($"{label,-38} balance = {balance,10:N0}   time = {stopwatch.ElapsedMilliseconds,5} ms   ({verdict})");
    }
}
