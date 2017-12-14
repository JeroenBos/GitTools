using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests.CI
{
    [TestClass]
    public class UnitTests
    {
        [TestMethod]
        public void Test()
        {

        }
        [TestMethod, ExpectedException(typeof(DivideByZeroException))]
        public void CorrectExpectedException()
        {
            throw new DivideByZeroException();
        }
        [TestMethod, ExpectedException(typeof(DivideByZeroException))]
        public void IncorrectExpectedException()
        {
            throw new NotImplementedException();
        }
    }
}
