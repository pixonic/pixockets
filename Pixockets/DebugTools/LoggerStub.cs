using System;

namespace Pixockets.DebugTools
{
    public class LoggerStub : ILogger
    {
        public void Info(string message)
        {
            Console.WriteLine($"INFO|{message}");
        }

        public void Warning(string message)
        {
            Console.WriteLine($"WARN|{message}");
        }

        public void Error(string message)
        {
            Console.WriteLine($"ERROR|{message}");
        }

        public void Exception(Exception exception)
        {
            Console.WriteLine($"ERROR|{exception}");
        }
    }
}
