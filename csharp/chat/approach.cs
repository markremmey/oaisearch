using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace chat
{
    internal abstract class Approach
    {
        public abstract Task<string> run(dynamic history, dynamic overrides);
    }
}
