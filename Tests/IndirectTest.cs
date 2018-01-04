using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CI.UI.Tests
{
    // the purpose of this attribute is to be a test attribute in one project (the project being invoked as test), while the main project doesn't recognize tests attributed with this attribute
    // (because the main project shouldn't test them, but the test RunTest runs them indirectly)

#if !NOT_DUMMY_TESTS
    public class IndirectTestAttribute : TestAttribute { }
#else
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class IndirectTestAttribute : Attribute { }
#endif
}
