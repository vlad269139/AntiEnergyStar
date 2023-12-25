using AntiEnergyStar.Interop;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace AntiEnergyStar
{
    public unsafe class EnergyManager
    {
        public static readonly HashSet<string> KeepingProcessList = new HashSet<string>
        {
#if DEBUG
            // Visual Studio
            "devenv.exe",
#endif
        };

        public static readonly uint WaitingDelay;


        // Speical handling needs for UWP to get the child window process
        public const string UWPFrameHostApp = "ApplicationFrameHost.exe";

        //private static uint pendingProcPid = 0;
        //private static string pendingProcName = "";

        private static IntPtr pThrottleOn = IntPtr.Zero;
        private static IntPtr pThrottleOff = IntPtr.Zero;
        private static int szControlBlock = 0;

        static EnergyManager()
        {
            var settings = Settings.Instance;


            KeepingProcessList.UnionWith(
                settings.Processes.Select(s => s.ToLowerInvariant())
                );
            KeepingProcessList.UnionWith(
                new string[] { AppDomain.CurrentDomain.FriendlyName }
                );

            Console.WriteLine($"Delay: {settings.Delay} seconds.\r\nList:");
            
            foreach (var n in KeepingProcessList)
                Console.WriteLine($"  > {n}");



            szControlBlock = Marshal.SizeOf(typeof(Win32Api.PROCESS_POWER_THROTTLING_STATE));
            pThrottleOn = Marshal.AllocHGlobal(szControlBlock);
            pThrottleOff = Marshal.AllocHGlobal(szControlBlock);

            var throttleState = new Win32Api.PROCESS_POWER_THROTTLING_STATE
            {
                Version = Win32Api.PROCESS_POWER_THROTTLING_STATE.PROCESS_POWER_THROTTLING_CURRENT_VERSION,
                ControlMask = Win32Api.ProcessorPowerThrottlingFlags.PROCESS_POWER_THROTTLING_EXECUTION_SPEED,
                StateMask = Win32Api.ProcessorPowerThrottlingFlags.PROCESS_POWER_THROTTLING_EXECUTION_SPEED,
            };

            var unthrottleState = new Win32Api.PROCESS_POWER_THROTTLING_STATE
            {
                Version = Win32Api.PROCESS_POWER_THROTTLING_STATE.PROCESS_POWER_THROTTLING_CURRENT_VERSION,
                ControlMask = Win32Api.ProcessorPowerThrottlingFlags.PROCESS_POWER_THROTTLING_EXECUTION_SPEED,
                StateMask = Win32Api.ProcessorPowerThrottlingFlags.None,
            };

            Marshal.StructureToPtr(throttleState, pThrottleOn, false);
            Marshal.StructureToPtr(unthrottleState, pThrottleOff, false);
        }

        private static void ToggleEfficiencyMode(IntPtr hProcess, bool enable)
        {
            Win32Api.SetProcessInformation(hProcess, Win32Api.PROCESS_INFORMATION_CLASS.ProcessPowerThrottling,
                enable ? pThrottleOn : pThrottleOff, (uint)szControlBlock);
            Win32Api.SetPriorityClass(hProcess, enable ? Win32Api.PriorityClass.IDLE_PRIORITY_CLASS : Win32Api.PriorityClass.NORMAL_PRIORITY_CLASS);
        }

        private static string GetProcessNameFromHandle(IntPtr hProcess)
        {
            int capacity = 1024;
            var sb = new StringBuilder(capacity);

            if (Win32Api.QueryFullProcessImageName(hProcess, 0, sb, ref capacity))
            {
                //System.IO.Path.GetFileName(sb);
                return System.IO.Path.GetFileName(sb.ToString());
            }

            return "";
        }

        public static unsafe void HandleForegroundEvent(IntPtr hwnd)
        {
            var windowThreadId = Win32Api.GetWindowThreadProcessId(hwnd, out uint procId);
            // This is invalid, likely a process is dead, or idk
            if (windowThreadId == 0 || procId == 0) return;

            var procHandle = Win32Api.OpenProcess(
                (uint) (Win32Api.ProcessAccessFlags.QueryLimitedInformation | Win32Api.ProcessAccessFlags.SetInformation), false, procId);
            if (procHandle == IntPtr.Zero) return;

            // Get the process
            var appName = GetProcessNameFromHandle(procHandle);
            
            // UWP needs to be handled in a special case
            if (appName == UWPFrameHostApp)
            {
                var found = false;
                Win32Api.EnumChildWindows(hwnd, (innerHwnd, lparam) =>
                {
                    if (found) return true;
                    if (Win32Api.GetWindowThreadProcessId(innerHwnd, out uint innerProcId) > 0)
                    {
                        if (procId == innerProcId) return true;

                        var innerProcHandle = Win32Api.OpenProcess((uint)(Win32Api.ProcessAccessFlags.QueryLimitedInformation |
                            Win32Api.ProcessAccessFlags.SetInformation), false, innerProcId);
                        if (innerProcHandle == IntPtr.Zero) return true;

                        // Found. Set flag, reinitialize handles and call it a day
                        found = true;
                        Win32Api.CloseHandle(procHandle);
                        procHandle = innerProcHandle;
                        procId = innerProcId;
                        appName = GetProcessNameFromHandle(procHandle);
                    }

                    return true;
                }, IntPtr.Zero);
            }

            // Boost the current foreground app, and then impose EcoQoS for previous foreground app
            var bypass = false; //KeepingProcessList.Contains(appName.ToLowerInvariant());
            if (!bypass)
            {
                Console.WriteLine($"Boosting foregrounded '{appName}' (PID: {procId})");
                ToggleEfficiencyMode(procHandle, false);
            }

            //if (pendingProcPid != 0)
            //{
            //    Console.WriteLine($"Throttle {pendingProcName} ({pendingProcPid})");

            //    var prevProcHandle = Win32Api.OpenProcess((uint)Win32Api.ProcessAccessFlags.SetInformation, false, pendingProcPid);
            //    if (prevProcHandle != IntPtr.Zero)
            //    {
            //        ToggleEfficiencyMode(prevProcHandle, true);
            //        Win32Api.CloseHandle(prevProcHandle);
            //        pendingProcPid = 0;
            //        pendingProcName = "";
            //    }
            //}
            

            //if (!bypass)
            //{
            //    pendingProcPid = procId;
            //    pendingProcName = appName;
            //}

            Win32Api.CloseHandle(procHandle);
        }

        public static void BoostAllUserBackgroundProcesses()
        {
            Console.WriteLine("B: Boosting backgrounds.");


            var runningProcesses = Process.GetProcesses();
            var currentSessionID = Process.GetCurrentProcess().SessionId;

            var sameAsThisSession = runningProcesses.Where(
                p => {
                    ConsoleColor c1 = ConsoleColor.Red;
                    ConsoleColor c2 = ConsoleColor.Red;

                    bool mustBoost = KeepingProcessList.Contains($"{p.ProcessName.ToLowerInvariant()}.exe");
                    //if (mustBoost)
                    //    c1 = ConsoleColor.Green;

                    bool sameSession = p.SessionId == currentSessionID;
                    //if (sameSession)
                    //    c2 = ConsoleColor.Green;

                    //Console.ForegroundColor = c1;
                    //Console.Write($"{p.ProcessName}\t");
                    //Console.ForegroundColor = c2;
                    //Console.WriteLine($"{sameSession}");
                    //Console.ForegroundColor = ConsoleColor.White;

                    return mustBoost && sameSession;
                });


            foreach (var proc in sameAsThisSession)
            {
                var hProcess = Win32Api.OpenProcess((uint)Win32Api.ProcessAccessFlags.SetInformation, false, (uint) proc.Id);
                ToggleEfficiencyMode(hProcess, false);
                Win32Api.CloseHandle(hProcess);
            }

            Console.WriteLine($"B: Done. {sameAsThisSession.Count()} processes affected.");
        }
    }
}
