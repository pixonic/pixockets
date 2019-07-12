using System;

namespace Pixockets.DebugTools
{
    public interface ILogger
    {
        void Info(string message);
        void Warning(string message);
        void Error(string message);
        void Exception(Exception exception);
    }
}
