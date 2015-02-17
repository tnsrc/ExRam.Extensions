﻿using System;
using NUnit.Framework;

namespace ExRam.Framework.Tests
{
    public class ExceptionExtensionsTest
    {
        [Test]
        public void ExceptionMessages_are_concatenated_by_GetSafeMessage()
        {
            var inner = new InvalidOperationException();
            var outer = new ArgumentNullException("Eine Message", inner);

            Assert.AreEqual(outer.Message + " ---> " + inner.Message, outer.GetSafeMessage());
        }

        [Test]
        public void GetSafeMessage_can_be_called_on_null_reference()
        {
            Exception ex = null;
            Assert.AreEqual(string.Empty, ex.GetSafeMessage());
        }
    }
}