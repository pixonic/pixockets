using System.Threading;
using UnitTests.Mock;

namespace UnitTests
{
    class Utils
    {
        public static void WaitOnReceive(MockCallbacks cbs)
        {
            for (int i = 0; i < 1000; ++i)
            {
                Thread.Sleep(1);
                if (cbs.OnReceiveCalls.Count > 0)
                    break;
            }
        }
    }
}
