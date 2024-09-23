
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

class Program
{
    public static int Main(string[] args)
    {
        if (args.Length < 1)
        {
            Console.Error.WriteLine($"Usage: {typeof(Program).Assembly.GetName().Name} MODE");
            Console.Error.WriteLine();
            Console.Error.WriteLine("Modes:");
            Console.Error.WriteLine("--uncaught-exception");
            Console.Error.WriteLine("--fail-fast");
            Console.Error.WriteLine("--stack-overflow");
            Console.Error.WriteLine("--debug-assert");
            Console.Error.WriteLine("--raise-exception");
            Console.Error.WriteLine("--invalid-unsafe");
            Console.Error.WriteLine();
            return 99;
        }

        switch (args[0].ToLowerInvariant())
        {
            case "--uncaught-exception":
                throw new Exception("Uncaught");
            case "--fail-fast":
                Environment.FailFast("Environment.FailFast() called");
                break;
            case "--stack-overflow":
                PerformOverflow();
                break;
            case "--debug-assert":
                Debug.Assert(false, "Debug.Assert()");
                break;
            case "--raise-exception":
                RaiseException(0xC0000005, 0, 0, new IntPtr(1));
                break;
            case "--invalid-unsafe":
                PerformMemoryAccessError();
                break;
            default:
                Console.Error.WriteLine("error: invalid mode.");
                break;
        }

        Console.WriteLine("Shouldn't be here, didn't crash!");
        return 1;
    }

    [DllImport("kernel32.dll")]
    static extern void RaiseException(uint dwExceptionCode, uint dwExceptionFlags,  uint nNumberOfArguments, IntPtr lpArguments);

    static void PerformOverflow()
    {
        PerformOverflow();
    }

    static unsafe void PerformMemoryAccessError()
    {
        int[] array = new int[1];
        fixed (int* ptr = array)
        {
            ptr[int.MaxValue - 1] = 42;
        }
    }
}
