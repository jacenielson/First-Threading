// Version A: The race condition. No lock at all.
// Three threads: Main (the bank manager) + two ATM worker threads.
// ATM 1 deposits $1 ten million times; ATM 2 withdraws $1 ten million times.
// The balance should end exactly where it started. Does it?
//
// BEFORE RUNNING: write down what you predict the final balance will be.

using System;
using System.Diagnostics;
using System.Threading;

class Program
{
    const int Iterations = 10_000_000;
    const int StartingBalance = 1000;
    const int Trials = 5;

    static int balance;

    static void Main()
    {
        Thread.CurrentThread.Name = "Main";
        Console.WriteLine($"[{Thread.CurrentThread.Name}] thread id {Thread.CurrentThread.ManagedThreadId} is opening the bank.");
        Console.WriteLine($"Each trial: start at ${StartingBalance}, {Iterations:N0} deposits vs {Iterations:N0} withdrawals.\n");

        // Run the same code several times. Watch whether the answer is even consistent.
        for (int trial = 1; trial <= Trials; trial++)
        {
            RunTrial($"Trial {trial} (no lock)", DepositNoLock, WithdrawNoLock);
        }

        Console.WriteLine("\nDiscussion: Where did the money go? Why is the answer different every time?");
        Console.WriteLine("Hint: balance++ looks like one step, but it is really three: READ, ADD, WRITE.");
    }

    // ---------- No lock: two threads read, add, and write the same variable at the same time.
    // If both read the same old value, one thread's update overwrites the other's and is lost.
    static void DepositNoLock()
    {
        for (int i = 0; i < Iterations; i++)
            balance++;   // deposit $1
    }

    static void WithdrawNoLock()
    {
        for (int i = 0; i < Iterations; i++)
            balance--;   // withdraw $1
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
        atm1.Join();        // Main waits here until ATM 1 is done...
        atm2.Join();        // ...and here until ATM 2 is done.
        stopwatch.Stop();

        string verdict = balance == StartingBalance
            ? "correct"
            : $"WRONG, off by {balance - StartingBalance:N0}";

        Console.WriteLine($"{label,-22} balance = {balance,12:N0}   time = {stopwatch.ElapsedMilliseconds,5} ms   ({verdict})");
    }
}
