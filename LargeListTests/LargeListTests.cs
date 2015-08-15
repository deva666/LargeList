using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MarkoDevcic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MarkoDevcic.Tests
{
    [TestClass()]
    public class LargeListTests
    {
        [TestMethod]
        public void TestAdd()
        {
            var size = 1 << 20;
            var list = CreateList(size);

            for (int i = 0; i < size; i++)
            {
                Assert.AreEqual(i, list[i]);
            }
        }


        [TestMethod]
        public void TestContains()
        {
            var size = 1 << 14;
            var list = CreateList(size);

            for (int i = 0; i < size; i++)
            {
                Assert.IsTrue(list.Contains(i));
            }

            Assert.IsFalse(list.Contains(Int32.MaxValue));
        }

        [TestMethod]
        public void TestCount()
        {
            var size = 1 << 20;
            var list = CreateList(size);

            Assert.AreEqual(list.Count, size);
        }

        [TestMethod]
        public void TestEnumerator()
        {
            var list = new LargeList<Int32>();
            var size = 1 << 20;
            var helperArray = new int[size];
            for (int i = 0; i < size; i++)
            {
                list.Add(i);
                helperArray[i] = i;
            }

            foreach (var num in list)
            {
                Assert.AreEqual(num, helperArray[num]);
            }
        }

        [TestMethod]
        public void TestClear()
        {
            var size = 1 << 20;
            var list = CreateList(size);

            list.Clear();
            Assert.AreEqual(list.Count, 0);
            foreach (var item in list)
            {
                Assert.Fail("Shouldn't get here");
            }

            for (int i = 0; i < size; i++)
            {
                list.Add(i);
            }

            for (int i = 0; i < size; i++)
            {
                Assert.AreEqual(i, list[i]);
            }
        }

        [TestMethod]
        public void TestCopyTo()
        {
            var size = 1 << 20;
            var list = CreateList(size);

            var array = new int[size];
            list.CopyTo(array, 0);

            Assert.AreEqual(array.Length, list.Count);

            for (int i = 0; i < array.Length; i++)
            {
                Assert.AreEqual(array[i], list[i]);
            }
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void TestCollectionWasModified()
        {
            var size = 1 << 20;
            var list = CreateList(size);

            Task.Run(delegate { Thread.Sleep(1); list.Add(1); });

            foreach (var item in list)
            {

            }
        }

        [TestMethod]
        public void TestSizeConstructor()
        {
            var list = new LargeList<Int32>(1);
            list.Add(1);
            Assert.AreEqual(list.Count, 1);
            foreach (var item in list)
            {
                Assert.AreEqual(item, 1);
            }

            var size = (1 << 14) + 1;
            list = new LargeList<Int32>(size);

            for (int i = 0; i < size * 2; i++)
            {
                list.Add(i);
            }

            for (int i = 0; i < size * 2; i++)
            {
                Assert.AreEqual(list[i], i);
            }
        }

        [TestMethod]
        public void TestEnumerableConsuctor()
        {
            var size = (1 << 14) + 2500;
            var array = new Int32[size];
            for (int i = 0; i < size; i++)
            {
                array[i] = i;
            }

            var list = new LargeList<Int32>(array);

            for (int i = 0; i < size; i++)
            {
                Assert.AreEqual(array[i], list[i]);
            }
        }

        [TestMethod]
        public void TestIndexOf()
        {
            var size = (1 << 14) + 2113;
            var list = CreateList(size);

            for (int i = 0; i < size; i++)
            {
                Assert.AreEqual(i, list.IndexOf(i));
            }

            Assert.AreEqual(-1, list.IndexOf(-1));
        }

        [TestMethod]
        public void TestInsert()
        {
            var list = new LargeList<Int32>();
            list.Insert(0, 0);
            list.Add(2);

            list.Insert(1, 1);

            for (int i = 0; i < 3; i++)
            {
                Assert.AreEqual(i, list[i]);
            }
        }

        [TestMethod]
        public void TestInsertInsideSecondArray()
        {
            var size = (1 << 14) + 1;
            var list = CreateList(size);
            list.Insert(size, size);
            list.Insert(size - 1, size - 1);

            Assert.AreEqual(list.Count, size + 2);

            for (int i = size + 1; i >= size; i--)
            {
                Assert.AreEqual(i - 1, list[i]);
            }
        }

        [TestMethod]
        public void TestRemoveAt()
        {
            var size = (1 << 14) + 1234;
            var list = CreateList(size);
            list.RemoveAt(size - 1);

            Assert.AreEqual(list.Count, size - 1);

            list.RemoveAt(0);

            Assert.AreEqual(list.Count, size - 2);

            for (int i = 0; i < size - 2; i++)
            {
                Assert.AreEqual(i + 1, list[i]);
            }
        }

        [TestMethod]
        public void TestReverse()
        {
            var size = 1 << 20;
            var list = CreateList(size);

            for (int i = 0; i < size; i++)
            {
                list.Add(i);
            }

            list.Reverse();

            for (int i = size - 1; i >= 0; i--)
            {
                Assert.AreEqual(list[size - 1 - i], i);
            }
        }

        [TestMethod]
        public void TestSort()
        {
            var size = (1 << 16) + 1234;
            var list = new LargeList<Int32>();
            var helperArray = new Int32[size];
            var random = new Random();

            for (int i = 0; i < size; i++)
            {
                var n = random.Next();
                helperArray[i] = n;
                list.Add(n);
            }

            Array.Sort(helperArray);
            list.Sort();

            for (int i = 0; i < size; i++)
            {
                Assert.AreEqual(helperArray[i], list[i]);
            }
        }

        [TestMethod]
        public void TestRemove()
        {
            var size = (1 << 14) + 2134;
            var list = CreateList(size);

            Assert.IsTrue(list.Remove(0));
            Assert.IsTrue(list.Remove(size - 1));
            Assert.IsFalse(list.Remove(Int32.MaxValue));
        }

        private LargeList<Int32> CreateList(int size)
        {
            var list = new LargeList<Int32>();
            for (int i = 0; i < size; i++)
            {
                list.Add(i);
            }

            return list;
        }
    }
}