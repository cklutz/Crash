using System.CodeDom;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Reflection.Emit;
using System.Runtime.InteropServices;

namespace Crash
{
    partial class Program
    {
        public static int Main(string[] args)
        {
            if (args.Length < 1)
            {
                return Usage();
            }


            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i].ToLowerInvariant())
                {
                    case "--help":
                        return Usage();

                    case "--error-mode":
                        {
                            int res = TryGetErrorMode(args, ref i, out uint mode);
                            if (res != 0)
                            {
                                return res;
                            }
                            if (!SetErrorMode(mode, IntPtr.Zero))
                            {
                                throw new Win32Exception();
                            }
                            break;
                        }
                    case "--thread-error-mode":
                        {
                            int res = TryGetErrorMode(args, ref i, out uint mode);
                            if (res != 0)
                            {
                                return res;
                            }
                            if (!SetThreadErrorMode(mode, IntPtr.Zero))
                            {
                                throw new Win32Exception();
                            }
                            break;
                        }

                    case "--uncaught-exception":
                        Console.WriteLine("MODE: Throwing uncaught exception ...");
                        DumpState();
                        throw new Exception("Uncaught");
                    case "--fail-fast":
                        Console.WriteLine("MODE: Causing Environment.FailFast() ...");
                        DumpState();
                        Environment.FailFast("Environment.FailFast() called");
                        break;
                    case "--fail-fast-exception":
                        Console.WriteLine("MODE: Causing Environment.FailFast() with exception ...");
                        DumpState();
                        try
                        {
                            throw new Exception("FailFast cause");
                        }
                        catch (Exception e)
                        {
                            Environment.FailFast("Environment.FailFast() called", e);
                        }
                        break;
                    case "--stack-overflow":
                        Console.WriteLine("MODE: Causing stack overflow ...");
                        DumpState();
                        CauseStackOverflow();
                        break;
                    case "--debug-assert":
                        Console.WriteLine("MODE: Causing Debug.Assert() ...");
                        DumpState();
                        Debug.Assert(false, "Debug.Assert()");
                        break;
                    case "--raise-av-exception":
                        Console.WriteLine("MODE: Invoking native RaiseException() for access violation ...");
                        DumpState();
                        InvokeNativeRaiseErrorAccessViolation();
                        break;
                    case "--invalid-unsafe":
                        Console.WriteLine("MODE: Performing invalid memory access ...");
                        DumpState();
                        UnsafeInvalidMemoryAccess();
                        break;
                    case "--crt-abort":
                        Console.WriteLine("MODE: Invoking C runtime abort() ...");
                        DumpState();
                        abort();
                        break;
                    case "--terminate-process":
                        Console.WriteLine("MODE: Invoking TerminateProcess() ...");
                        DumpState();
                        if (!TerminateProcess(Process.GetCurrentProcess().Handle, 0xdeadbeaf))
                        {
                            throw new Win32Exception();
                        }
                        break;
                    default:
                        Console.Error.WriteLine("error: invalid option.");
                        return Usage($"error: invalid option '${args[i]}'");
                }
            }



            Console.WriteLine("error: shouldn't be here, didn't crash as expected!");
            return 1;
        }

        private static int TryGetErrorMode(string[] args, ref int i, out uint errorMode)
        {
            string arg = args[i];
            errorMode = 0;

            if (i + 1 >= args.Length)
            {
                return Usage($"error: missing value for {arg}");
            }

            string modeStr = args[++i];
            if (modeStr.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                modeStr = modeStr.Substring(2);
            }

            if (!uint.TryParse(modeStr, NumberStyles.HexNumber, null, out errorMode))
            {
                return Usage($"error: invalid value '{modeStr}' for {arg}.");
            }

            return 0;
        }

        private static int Usage(string message = null)
        {
            if (message != null)
            {
                Console.Error.WriteLine(message);
                Console.Error.WriteLine();
            }

            Console.Error.WriteLine(
                $"Usage: {typeof(Program).Assembly.GetName().Name} [OPTIONS] MODE\r\n" +
                "\r\n" +
                "Options:\r\n" +
                "--error-mode VALUE        set the error mode (see SetErrorMode() Win32 API)\r\n" +
                "--thread-error-mode VALUE set the error mode (see SetThreadErrorMode() Win32 API)\r\n" +
                "\r\n" +
                "Modes:\r\n" +
                "--uncaught-exception      cause an uncaught System.Exception\r\n" +
                "--fail-fast               invoke Environment.FailFast()\r\n" +
                "--fail-fast-exception     invoke Environment.FailFast() with exception parameter\r\n" +
                "--stack-overflow          cause a System.StackOverflowException\r\n" +
#if DEBUG                                  
                "--debug-assert            invoke Debug.Assert()\r\n" +
#endif                                     
                "--raise-av-exception      invoke native RaiseException() with code 0xC0000005\r\n" +
                "--invalid-unsafe          perform invalid memory access in unsafe block\r\n" +
                "\r\n");

            return 99;
        }

        private static void DumpState()
        {
#if NETCOREAPP
            string runtimeType = "CoreCLR";
#else
            string runtimeType = "DesktopCLR";
#endif
            Console.WriteLine("State:");
            Console.WriteLine("\tRuntime        : {0}", runtimeType);
            Console.WriteLine("\tErrorMode      : {0:X}", GetErrorMode());
            Console.WriteLine("\tThreadErrorMode: {0:X}", GetThreadErrorMode());


#if NETCOREAPP
            bool header = false;
            foreach (string variable in Environment.GetEnvironmentVariables().Keys)
            {
                if (variable.StartsWith("DOTNET_", StringComparison.OrdinalIgnoreCase) ||
                    variable.StartsWith("COMPlus_", StringComparison.OrdinalIgnoreCase))
                {
                    if (!header)
                    {
                        Console.WriteLine("\tDOTNET environment variables:");
                        header = true;
                    }

                    Console.WriteLine($"\t\t{variable}={Environment.GetEnvironmentVariable(variable)}");
                }
            }
#endif
        }

        private static void InvokeNativeRaiseErrorAccessViolation()
        {
            RaiseException(0xC0000005, 0, 0, new IntPtr(1));
        }

        [DllImport("kernel32.dll")]
        private static extern void RaiseException(uint dwExceptionCode, uint dwExceptionFlags, uint nNumberOfArguments, IntPtr lpArguments);

        static void CauseStackOverflow()
        {
            CauseStackOverflow();
        }

        static unsafe void UnsafeInvalidMemoryAccess()
        {
            uint[] array = new uint[1];
            fixed (uint* ptr = array)
            {
                // Attempt to write as far as possible outside the valid range...
                // Actual value we set is irrelevant, but we want a nice one.
                ptr[int.MaxValue - 1] = 0xdeadbeaf;
            }
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetErrorMode(uint dwNewMode, IntPtr oldMode);

        [DllImport("kernel32.dll")]
        private static extern uint GetErrorMode();

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetThreadErrorMode(uint dwNewMode, IntPtr oldMode);

        [DllImport("kernel32.dll")]
        private static extern uint GetThreadErrorMode();

        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool TerminateProcess(IntPtr hProcess, uint uExitCode);

        [DllImport("msvcrt.dll")]
        private static extern void abort();
    }
}
