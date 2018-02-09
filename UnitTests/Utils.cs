using System.Collections.Generic;
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

        public static void WaitOnList<T>(List<T> list)
        {
            for (int i = 0; i < 1000; ++i)
            {
                Thread.Sleep(1);
                if (list.Count > 0)
                    break;
            }
        }

        public static void WaitOnSet<T>(HashSet<T> set)
        {
            for (int i = 0; i < 1000; ++i)
            {
                Thread.Sleep(1);
                if (set.Count > 0)
                    break;
            }
        }
    }
}
