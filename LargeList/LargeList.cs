using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace MarkoDevcic
{
    [DebuggerDisplay("Count = {Count}")]
    [Serializable]
    public class LargeList<T> : IList<T>
    {
        private const Int32 LARGE_HEAP_LIMIT = 85000;
        private const Int32 DEFAULT_ARRAY_SIZE = 16;

        private readonly Int32 arrayMaxSize;

        private Int32 size = 0;
        private Int32 version = 0;

        private readonly List<T[]> arrayHolder;
        private readonly List<Int32> arraySizes;


        public LargeList()
        {
            if (typeof(T).IsValueType)
            {
                var typeSize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(T));
                var maxSize = LARGE_HEAP_LIMIT / typeSize;
                arrayMaxSize = FirstSmallerPowerOf2(maxSize);
            }
            else
            {
                var maxSize = LARGE_HEAP_LIMIT / IntPtr.Size;
                arrayMaxSize = FirstSmallerPowerOf2(maxSize);
            }

            arrayHolder = new List<T[]>();
            arraySizes = new List<Int32>();
        }

        public LargeList(Int32 size)
            : this()
        {
            if (size < 0)
                throw new ArgumentOutOfRangeException("Size must be 0 or larger");

            CreateInternalArrays(size);
        }

        private void CreateInternalArrays(Int32 size)
        {
            while (size >= arrayMaxSize)
            {
                arrayHolder.Add(new T[arrayMaxSize]);
                arraySizes.Add(0);
                size -= arrayMaxSize;
            }
            if (size > 0)
            {
                var lastSize = FirstLargerPowerOf2(size);
                lastSize = lastSize < DEFAULT_ARRAY_SIZE ? DEFAULT_ARRAY_SIZE : lastSize;
                arrayHolder.Add(new T[lastSize]);
                arraySizes.Add(0);
            }
        }

        public LargeList(IEnumerable<T> source)
            : this()
        {
            if (source == null)
                throw new ArgumentNullException("source");

            CreateInternalArrays(source.Count());

            foreach (var item in source)
            {
                Add(item);
            }
        }

        private Int32 FirstSmallerPowerOf2(Int32 num)
        {
            if (num <= 0)
                return 0;

            var count = 0;
            while (num > 1)
            {
                count++;
                num >>= 1;
            }
            return 1 << count;
        }

        private Int32 FirstLargerPowerOf2(Int32 num)
        {
            if (num < 0)
                return 0;

            if (num == 0)
                return 2;

            var count = 0;
            while (num > 0)
            {
                count++;
                num >>= 1;
            }
            return 1 << count;
        }

        public int IndexOf(T item)
        {
            for (int i = 0; i < arrayHolder.Count; i++)
            {
                var index = Array.IndexOf(arrayHolder[i], item, 0, arraySizes[i]);
                if (index >= 0)
                    return index + (i * arrayMaxSize);
            }
            return -1;
        }

        public void Insert(int index, T item)
        {
            if (index > size)
                throw new ArgumentOutOfRangeException();

            var arrayIndex = GetArrayIndex(index);

            EnsureTotalCapacity(arrayIndex);

            var array = arrayHolder[arrayIndex];

            var arraySizeToUpdate = arrayIndex;
            bool last = false;
            if (arrayIndex != arrayHolder.Count - 1 && arraySizes[arrayIndex] == arrayMaxSize)
            {
                var indexCount = arrayIndex;
                last = true;
                var lastItem = array[arraySizes[indexCount] - 1];
                while (indexCount < arrayHolder.Count - 1)
                {
                    var nextIndex = indexCount + 1;
                    EnsureArrayCapacity(nextIndex);
                    var nextArray = arrayHolder[nextIndex];
                    if (arraySizes[nextIndex] > 0)
                    {
                        var nextArrayLastItem = nextArray[arraySizes[nextIndex] - 1];
                        Array.Copy(nextArray, 0, nextArray, 1, arraySizes[nextIndex] == arrayMaxSize ? arraySizes[nextIndex] -1 : arraySizes[nextIndex]);
                        nextArray[0] = lastItem;
                        lastItem = nextArrayLastItem;
                    }
                    else
                    {
                        nextArray[0] = lastItem;
                    }
                    indexCount++;
                }
                arraySizeToUpdate = indexCount;
            }

            var position = index - arrayIndex * arrayMaxSize;
            if (position < arraySizes[arrayIndex])
            {
                Array.Copy(array, position, array, position + 1, last ? arraySizes[arrayIndex] - position - 1 : arraySizes[arrayIndex] - position);
            }

            array[position] = item;
            arraySizes[arraySizeToUpdate]++;
            size++;
            version++;
        }

        public void RemoveAt(int index)
        {
            if (index >= size)
                throw new ArgumentOutOfRangeException();

            var arrayIndex = GetArrayIndex(index);
            var array = arrayHolder[arrayIndex];
            var position = index - arrayIndex * arrayMaxSize;
            size--;

            if (position < arraySizes[arrayIndex])
            {
                Array.Copy(array, position + 1, array, position, arraySizes[arrayIndex] - 1 - position);
            }

            if (arrayIndex == arrayHolder.Count - 1)
            {
                array[arraySizes[arrayIndex]] = default(T);
                arraySizes[arrayIndex]--;
            }
            else
            {
                var indexCount = arrayIndex;

                while (indexCount < arrayHolder.Count - 1)
                {
                    var currentArray = arrayHolder[indexCount];
                    var nextArrayFirstItem = arrayHolder[indexCount + 1][0];
                    if (indexCount != arrayIndex)
                        Array.Copy(currentArray, 1, currentArray, 0, (arraySizes[indexCount] - 1));
                    currentArray[arraySizes[indexCount] - 1] = nextArrayFirstItem;
                    indexCount++;
                }
                var lastArrayIndex = arrayHolder.Count - 1;
                var lastArray = arrayHolder[lastArrayIndex];
                Array.Copy(lastArray, 1, lastArray, 0, (arraySizes[lastArrayIndex] - 1));
            }
            version++;
        }

        public T this[int index]
        {
            get
            {
                if (index >= size)
                    throw new ArgumentOutOfRangeException();

                var arrayIndex = GetArrayIndex(index);
                var position = index - arrayIndex * arrayMaxSize;
                var array = arrayHolder[arrayIndex];

                if (position >= array.Length)
                    throw new ArgumentOutOfRangeException();

                return array[position];
            }
            set
            {
                if (index >= size)
                    throw new ArgumentOutOfRangeException();

                var arrayIndex = GetArrayIndex(index);
                var position = index - arrayIndex * arrayMaxSize;
                var array = arrayHolder[arrayIndex];

                if (position >= array.Length)
                    throw new ArgumentOutOfRangeException();

                array[position] = value;
                version++;
            }
        }

        public void Add(T item)
        {
            var index = GetArrayIndex(size);
            var array = GetDestinationArray(ref index);
            var position = arraySizes[index];
            array[position] = item;
            arraySizes[index]++;
            size++;
            version++;
        }

        private Int32 GetArrayIndex(Int32 index)
        {
            var ratio = (double)index / (double)arrayMaxSize;
            return (Int32)Math.Floor(ratio);
        }

        private T[] GetDestinationArray(ref Int32 arrayIndex)
        {
            T[] array;
            if (arrayHolder.Count == arrayIndex)
            {
                array = CreateArrayAtIndex(arrayIndex);
                return array;
            }
            else
            {
                array = arrayHolder[arrayIndex];
                if (array.Length == arraySizes[arrayIndex])
                {
                    var resizeIndex = Resize(arrayIndex);
                    if (arrayIndex == resizeIndex)
                    {
                        array = arrayHolder[arrayIndex];
                    }
                    else
                    {
                        arrayIndex = resizeIndex;
                        return GetDestinationArray(ref arrayIndex);
                    }
                }
                return array;
            }
        }

        private void EnsureTotalCapacity(Int32 arrayIndex)
        {
            if (size % arrayMaxSize == 0 || arrayHolder.Count == arrayIndex)
            {
                CreateArrayAtIndex(arrayHolder.Count);
            }
            EnsureArrayCapacity(arrayIndex);
        }

        private void EnsureArrayCapacity(Int32 arrayIndex)
        {
            if (arrayHolder[arrayIndex].Length == arraySizes[arrayIndex])
            {
                Resize(arrayIndex);
            }
        }

        private T[] CreateArrayAtIndex(Int32 arrayIndex)
        {
            var array = new T[DEFAULT_ARRAY_SIZE];
            arrayHolder.Insert(arrayIndex, array);
            arraySizes.Insert(arrayIndex, 0);
            return array;
        }

        private Int32 Resize(Int32 arrayindex)
        {
            var oldSize = arraySizes[arrayindex];
            if (arrayMaxSize >> 1 >= oldSize)
            {
                var newSize = oldSize << 1;
                var oldArray = arrayHolder[arrayindex];
                var newArray = new T[newSize];
                Array.Copy(oldArray, 0, newArray, 0, oldArray.Length);
                arrayHolder[arrayindex] = newArray;

                return arrayindex;
            }
            else
            {
                return ++arrayindex;
            }
        }

        public void Clear()
        {
            arrayHolder.Clear();
            arraySizes.Clear();
            size = 0;
            version++;
        }

        public bool Contains(T item)
        {
            var comparer = EqualityComparer<T>.Default;
            for (int i = 0; i < arrayHolder.Count; i++)
            {
                var array = arrayHolder[i];
                for (int j = 0; j < arraySizes[i]; j++)
                {
                    if (comparer.Equals(item, array[j]))
                        return true;
                }
            }

            return false;
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            if (array == null)
                throw new ArgumentNullException("array");

            if (arrayIndex < 0 || arrayIndex > array.Length)
                throw new IndexOutOfRangeException();

            if (array.Length - arrayIndex < size)
                throw new IndexOutOfRangeException();

            for (int i = 0; i < arrayHolder.Count; i++)
            {
                Array.Copy(arrayHolder[i], 0, array, i * arraySizes[i], arraySizes[i]);
            }
        }

        public void ForEach(Action<T> action)
        {
            for (int i = 0; i < arrayHolder.Count; i++)
            {
                var array = arrayHolder[i];
                for (int j = 0; j < arraySizes[i]; j++)
                {
                    action(array[j]);
                }
            }
        }

        public int Count
        {
            get { return size; }
        }

        public bool IsReadOnly
        {
            get { return false; }
        }

        public void InsertRange(Int32 index, IEnumerable<T> source)
        {
            if (index > size || index < 0)
                throw new ArgumentOutOfRangeException();

            if (source == null)
                throw new ArgumentNullException("source");

            foreach (var item in source)
            {
                Insert(index, item);
                index++;
            }
        }

        public bool Remove(T item)
        {
            var position = IndexOf(item);
            if (position < 0)
                return false;

            RemoveAt(position);

            return true;
        }

        public void Reverse()
        {
            foreach (var array in arrayHolder)
            {
                Array.Reverse(array);
            }
            arrayHolder.Reverse();

            version++;
        }

        public void Sort()
        {
            var comparer = Comparer<T>.Default;
            HeapSort(comparer);
        }

        public void Sort(IComparer<T> comparer)
        {
            if (comparer == null)
                throw new ArgumentNullException("comparer");

            HeapSort(comparer);
        }

        private void HeapSort(IComparer<T> comparer)
        {
            var middle = size / 2;
            var heapSize = size;
            for (int i = middle; i >= 0; i--)
            {
                BuildMaxHeap(i, comparer, heapSize);
            }

            for (int i = size - 1; i > 0; i--)
            {
                Swap(i, 0);
                heapSize--;
                BuildMaxHeap(0, comparer, heapSize);
            }

            version++;
        }

        private void BuildMaxHeap(Int32 index, IComparer<T> comparer, Int32 heapSize)
        {
            while (index * 2 + 1 < heapSize)
            {
                int next;

                var left = index * 2 + 1;
                if (comparer.Compare(this[left], this[index]) > 0)
                {
                    next = left;
                }
                else
                {
                    next = index;
                }

                var right = index * 2 + 2;
                if (right < heapSize && comparer.Compare(this[right], this[next]) > 0)
                {
                    next = right;
                }

                if (next != index)
                {
                    Swap(next, index);
                    index = next;
                }
                else
                {
                    break;
                }
            }
        }

        private void Swap(Int32 left, Int32 right)
        {
            var temp = this[left];
            this[left] = this[right];
            this[right] = temp;
        }

        public IEnumerator<T> GetEnumerator()
        {
            return new Enumerator(this);
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return new Enumerator(this);
        }

        [Serializable]
        internal struct Enumerator : IEnumerator<T>
        {
            private readonly LargeList<T> list;
            private readonly Int32 listVersion;
            private Int32 index;
            private T current;

            internal Enumerator(LargeList<T> _list)
            {
                list = _list;
                listVersion = _list.version;
                current = default(T);
                index = 0;
            }

            public T Current
            {
                get { return current; }
            }

            public void Dispose()
            {

            }

            object System.Collections.IEnumerator.Current
            {
                get { return current; }
            }

            public bool MoveNext()
            {
                CheckVersion();

                if (index < list.size)
                {
                    current = list[index];
                    index++;
                    return true;
                }
                else
                {
                    return false;
                }
            }

            private void CheckVersion()
            {
                if (listVersion != list.version)
                    throw new InvalidOperationException("Collection was modified");
            }

            public void Reset()
            {
                index = 0;
                current = default(T);
            }
        }

    }
}
