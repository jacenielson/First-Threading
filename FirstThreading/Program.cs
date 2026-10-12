using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace TicketSystem
{
    class Seat
    {
        public int Id {get;}
        public string Name{get;}
        public bool IsReserved {get; set;}
        public readonly object Lock = new object();

        public Seat (int id, string name)
        {
            Id = id;
            Name = name;
            IsReserved = false;
        }
    }

    class StadiumSales
    {
        static int totalTickets = 1000;
        static int ticketsSoldNoLock = 0;
        static int ticketsSoldLocked = 0;
        static int ticketsSoldInterLocked = 0;

        private static readonly object salesLock = new object();

        static void Main()
        {
            Console.WriteLine("=== LESSON 1: Race Condition (Unsafe Sales) ===");
            SimulateUnsafeSales();

            Console.WriteLine("\n=== LESSON 2: Fixing with lock vs Interlocked ===");
            SimulateSafeSales();

            Console.WriteLine("\n=== LESSON 3: Multi-Seat Reservations (Deadlock Prevention) ===");
            SimulateMultiSeatReservation();
        }

        // Race Condition Demo: Overselling tickets due to bad concurrency
        static void SimulateUnsafeSales()
        {
            ticketsSoldNoLock = 0;
            Task[] customers = new Task[10];

            for(int i = 0;i<customers.Length; i++)
            {
                customers[i] = Task.Run(() =>
                {
                    for(int j=0; j<100; j++)
                    {
                        // READ - MODIFY - WRITE race condition
                        ticketsSoldNoLock++;
                    }
                });
            }
            Task.WaitAll(customers);
            Console.WriteLine($"Expected Tickets Sold: 1,000");
            Console.WriteLine($"Actual Tickets Sold:   {ticketsSoldNoLock:N0} (Race condition lost updates!)");
        }

        // Lock vs Interlocked
        static void SimulateSafeSales()
        {
            ticketsSoldLocked = 0;
            ticketsSoldInterLocked = 0;

            //lock
            var sw = Stopwatch.StartNew();
            Parallel.For(0, 1000, i =>
            {
                lock (salesLock)
                {
                    ticketsSoldLocked++;
                }
            });
            sw.Stop();
            Console.WriteLine($"lock result:        {ticketsSoldLocked} sold in {sw.ElapsedTicks} ticks");

            //Interlocked
            sw.Restart();
            Parallel.For(0, 1000, i =>
            {
                //gurantees read, modify, write are done in one instruction
                Interlocked.Increment(ref ticketsSoldInterLocked);
            });
            sw.Stop();
            Console.WriteLine($"Interlocked result: {ticketsSoldInterLocked} sold in {sw.ElapsedTicks} ticks");
        }

        static void SimulateMultiSeatReservation()
        {
            var SeatA = new Seat(1, "Section 1, Seat A");
            var SeatB = new Seat(2, "Section 1, Seat B");

            // Two customers trying to reserve the exact same two adjacent seats at once
            Task customer1 = Task.Run(() => ReserveTwoSeatsOrdered(SeatA, SeatB, "Customer 1"));
            Task customer2 = Task.Run(() => ReserveTwoSeatsOrdered(SeatB, SeatA, "Customer 2"));

            Task.WaitAll(customer1, customer2);
            Console.WriteLine("Both multi-seat reservations completed without deadlock!"); 
        }

        static void ReserveTwoSeatsOrdered(Seat seat1, Seat seat2, string customerName)
        {
            //always lock lowest ID first
            Seat firstLock = seat1.Id < seat2.Id ? seat1 :seat2;
            Seat secondLock = seat1.Id < seat2.Id ? seat2 :seat1;

            lock (firstLock.Lock)
            {
                Thread.Sleep(10);
                lock (secondLock.Lock)
                {
                    if(!seat1.IsReserved && !seat2.IsReserved)
                    {
                        seat1.IsReserved = true;
                        seat2.IsReserved = true;
                        Console.WriteLine($"[{customerName}] Successfully reserved {seat1.Name} and {seat2.Name}!");
                    }
                    else
                    {
                        Console.WriteLine($"[{customerName}] Reservation failed — seats taken.");
                    }
                }
            }
            
        }



    }
}
