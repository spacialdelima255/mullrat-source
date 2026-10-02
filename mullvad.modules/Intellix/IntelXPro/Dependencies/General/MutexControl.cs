using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace IntelXPro.src.IntelXPro.Dependencies.General
{
    public static class MutexControl
    {
        public static Mutex currentApp;

        public static bool createdNew;

        public static bool CreateMutex(string mtx)
        {
            currentApp = new Mutex(initiallyOwned: false, mtx, out createdNew);
            return createdNew;
        }
    }
}
