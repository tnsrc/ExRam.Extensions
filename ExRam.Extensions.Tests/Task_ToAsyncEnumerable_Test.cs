﻿// (c) Copyright 2014 ExRam GmbH & Co. KG http://www.exram.de
//
// Licensed using Microsoft Public License (Ms-PL)
// Full License description can be found in the LICENSE
// file.

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ExRam.Framework.Tests
{
    [TestClass]
    public class Task_ToAsyncEnumerable_Test
    {
        [TestMethod]
        public async Task ToAsyncEnumerable_completes()
        {
            var tcs = new TaskCompletionSource<bool>();

            var task = ((Task)tcs.Task).ToAsyncEnumerable().First();

            Assert.IsFalse(task.IsCompleted);
            tcs.SetResult(true);

            await task;
        }

        [TestMethod]
        [ExpectedException(typeof(DivideByZeroException))]
        public async Task ToAsyncEnumerable_forwards_exception()
        {
            var tcs = new TaskCompletionSource<bool>();

            var task = ((Task)tcs.Task)
                .ToAsyncEnumerable()
                .First();

            Assert.IsFalse(task.IsCompleted);
            tcs.SetException(new DivideByZeroException());

            try
            {
                await task;
            }
            catch(AggregateException ex)
            {
                throw ex.GetBaseException();
            }
        }
        
    }
}