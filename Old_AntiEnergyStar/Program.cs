using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Runtime;
using System.Threading.Tasks;
using AntiEnergyStar.Interop;

namespace AntiEnergyStar
{
    internal class Program
    {
        static CancellationTokenSource cts = new CancellationTokenSource();

        static void HouseKeepingThreadProc()
        {
            Console.WriteLine("House keeping thread started.");

            var delay = Settings.Instance.Delay;

            while (!cts.IsCancellationRequested)
            {
                try
                {

                    Task.Delay(TimeSpan.FromSeconds(delay), cts.Token).Wait();


                    //var houseKeepingTimer = new PeriodicTimer(TimeSpan.FromMinutes(5));
                    // await houseKeepingTimer.WaitForNextTickAsync(cts.Token);
                    EnergyManager.BoostAllUserBackgroundProcesses();
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }
        }

        static void Main(string[] args)
        {
            // Well, this program only works for Windows Version starting with Cobalt...
            // Nickel or higher will be better, but at least it works in Cobalt
            //
            // In .NET 5.0 and later, System.Environment.OSVersion always returns the actual OS version.

            //if (Environment.OSVersion.Version.Build < 22000)
            //{
            //    Console.WriteLine("E: You are too poor to use this program.");
            //    Console.WriteLine("E: Please upgrade to Windows 11 22H2 for best result, and consider ThinkPad Z13 as your next laptop.");
            //    // ERROR_CALL_NOT_IMPLEMENTED
            //    Environment.Exit(120);
            //}

            HookManager.SubscribeToWindowEvents();
            EnergyManager.BoostAllUserBackgroundProcesses();

            var houseKeepingThread = new Thread(new ThreadStart(HouseKeepingThreadProc));
            houseKeepingThread.Start();

            while (true)
            {
                if (Event.GetMessage(out Win32WindowForegroundMessage msg, IntPtr.Zero, 0, 0))
                {
                    if (msg.Message == Event.WM_QUIT)
                    {
                        cts.Cancel();
                        break;
                    }

                    Event.TranslateMessage(ref msg);
                    Event.DispatchMessage(ref msg);
                }
            }

            cts.Cancel();
            HookManager.UnsubscribeWindowEvents();


            Console.WriteLine("EXIT");
            Console.ReadLine();

            Environment.Exit(0);
        }
    }
}
