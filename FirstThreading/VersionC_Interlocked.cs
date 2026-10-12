// Version C: Interlocked, the right tool for simple shared counters.
// Same setup: Main + two ATM threads, one depositing and one withdrawing.
// Compares no lock vs lock vs Interlocked for correctness and speed.

using System;
using System.Diagnostics;
using System.Threading;

namespace VersionC
{
    
class Program
{
    const int Iterations = 10_000_000;
    const int StartingBalance = 1000;

    static int balance;
    private static readonly object balanceLock = new object();

    static void Main()
    {
        Thread.CurrentThread.Name = "Main";
        Console.WriteLine($"[{Thread.CurrentThread.Name}] thread id {Thread.CurrentThread.ManagedThreadId} is opening the bank.\n");

        RunTrial("No lock (broken)",     DepositNoLock,       WithdrawNoLock);
        RunTrial("lock (fine-grained)",  DepositLock,         WithdrawLock);
        RunTrial("Interlocked",          DepositInterlocked,  WithdrawInterlocked);

        Console.WriteLine();
        Console.WriteLine("Interlocked turns read-add-write into ONE indivisible CPU instruction,");
        Console.WriteLine("so no other thread can sneak in between the steps. No lock object needed.");
        Console.WriteLine();
        Console.WriteLine("Limit: Interlocked protects ONE variable in ONE operation.");
        Console.WriteLine("\"Check the balance, then withdraw if there's enough\" is two steps,");
        Console.WriteLine("so that still needs a lock (or Interlocked.CompareExchange in a retry loop).");
    }

    static void DepositNoLock()       { for (int i = 0; i < Iterations; i++) balance++; }
    static void WithdrawNoLock()      { for (int i = 0; i < Iterations; i++) balance--; }

    static void DepositLock()         { for (int i = 0; i < Iterations; i++) { lock (balanceLock) { balance++; } } }
    static void WithdrawLock()        { for (int i = 0; i < Iterations; i++) { lock (balanceLock) { balance--; } } }

    static void DepositInterlocked()  { for (int i = 0; i < Iterations; i++) Interlocked.Increment(ref balance); }
    static void WithdrawInterlocked() { for (int i = 0; i < Iterations; i++) Interlocked.Decrement(ref balance); }

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
        atm1.Join();
        atm2.Join();
        stopwatch.Stop();

        string verdict = balance == StartingBalance
            ? "correct"
            : $"WRONG, off by {balance - StartingBalance:N0}";

        Console.WriteLine($"{label,-22} balance = {balance,10:N0}   time = {stopwatch.ElapsedMilliseconds,5} ms   ({verdict})");
    }
}
}
