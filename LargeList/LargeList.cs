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

        private readonly List<T[]> internalArrays;
        private readonly List<Int32> internalArraySizes;


        public LargeList()
        {
            if (typeof(T).IsValueType)
            {
                if (typeof(T) == typeof(Double))
                {
                    //CLR alocates arrays of double larger then 1000 on LOH
                    arrayMaxSize = FirstSmallerPowerOf2(1000);
                }
                else
                {
                    var typeSize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(T));
                    var maxSize = LARGE_HEAP_LIMIT / typeSize;
                    arrayMaxSize = FirstSmallerPowerOf2(maxSize);
                }
            }
            else
            {
                var maxSize = LARGE_HEAP_LIMIT / IntPtr.Size;
                arrayMaxSize = FirstSmallerPowerOf2(maxSize);
            }

            internalArrays = new List<T[]>();
            internalArraySizes = new List<Int32>();
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
                internalArrays.Add(new T[arrayMaxSize]);
                internalArraySizes.Add(0);
                size -= arrayMaxSize;
            }
            if (size > 0)
            {
                var lastSize = FirstLargerPowerOf2(size);
                lastSize = lastSize < DEFAULT_ARRAY_SIZE ? DEFAULT_ARRAY_SIZE : lastSize;
                internalArrays.Add(new T[lastSize]);
                internalArraySizes.Add(0);
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
            for (int i = 0; i < internalArrays.Count; i++)
            {
                var index = Array.IndexOf(internalArrays[i], item, 0, internalArraySizes[i]);
                if (index >= 0)
                    return index + (i * arrayMaxSize);
            }
            return -1;
        }

        public void Insert(int index, T item)
        {
            if (index > size)
                throw new ArgumentOutOfRangeException();

            var arrayIndex = GetInternalArrayIndex(index);

            EnsureTotalCapacity(arrayIndex);

            var array = internalArrays[arrayIndex];

            var arraySizeToUpdate = arrayIndex;

            //inserting to array that is not last means that that array has hit its max size, so we have to make room for the inserted item
            //we do that by shifting array's last item to the next array's first position, repeating until we hit last array
            if (arrayIndex != internalArrays.Count - 1 && internalArraySizes[arrayIndex] == arrayMaxSize)
            {
                var indexCount = arrayIndex;
                var lastItem = array[internalArraySizes[indexCount] - 1];
                while (indexCount < internalArrays.Count - 1)
                {
                    var nextIndex = indexCount + 1;
                    EnsureArrayCapacity(nextIndex);
                    var nextArray = internalArrays[nextIndex];
                    if (internalArraySizes[nextIndex] > 0)
                    {
                        var nextArrayLastItem = nextArray[internalArraySizes[nextIndex] - 1];
                        Array.Copy(nextArray, 0, nextArray, 1,
                            internalArraySizes[nextIndex] == arrayMaxSize ? internalArraySizes[nextIndex] - 1 : internalArraySizes[nextIndex]);
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
            if (position < internalArraySizes[arrayIndex])
            {
                Array.Copy(array, position, array, position + 1,
                    internalArraySizes[arrayIndex] == arrayMaxSize ? internalArraySizes[arrayIndex] - position - 1 : internalArraySizes[arrayIndex] - position);
            }

            array[position] = item;
            internalArraySizes[arraySizeToUpdate]++;
            size++;
            version++;
        }

        public void RemoveAt(int index)
        {
            if (index >= size)
                throw new ArgumentOutOfRangeException();

            var arrayIndex = GetInternalArrayIndex(index);
            var array = internalArrays[arrayIndex];
            var position = index - arrayIndex * arrayMaxSize;

            if (position < internalArraySizes[arrayIndex])
            {
                Array.Copy(array, position + 1, array, position, internalArraySizes[arrayIndex] - 1 - position);
            }

            //if removing from last array then just insert default item to that position
            //otherwise, we have to copy each first item from the next array to previous array last position
            if (arrayIndex == internalArrays.Count - 1)
            {
                array[internalArraySizes[arrayIndex]] = default(T);
                internalArraySizes[arrayIndex]--;
            }
            else
            {
                var indexCount = arrayIndex;

                while (indexCount < internalArrays.Count - 1)
                {
                    var currentArray = internalArrays[indexCount];
                    var nextArrayFirstItem = internalArrays[indexCount + 1][0];
                    if (indexCount != arrayIndex)
                    {
                        Array.Copy(currentArray, 1, currentArray, 0, (internalArraySizes[indexCount] - 1));
                    }
                    currentArray[internalArraySizes[indexCount] - 1] = nextArrayFirstItem;
                    indexCount++;
                }

                var lastArrayIndex = internalArrays.Count - 1;
                var lastArray = internalArrays[lastArrayIndex];
                Array.Copy(lastArray, 1, lastArray, 0, (internalArraySizes[lastArrayIndex] - 1));
                internalArraySizes[lastArrayIndex]--;
            }

            size--;
            version++;
        }

        public T this[int index]
        {
            get
            {
                if (index >= size)
                    throw new ArgumentOutOfRangeException();

                var arrayIndex = GetInternalArrayIndex(index);
                var position = index - arrayIndex * arrayMaxSize;
                var array = internalArrays[arrayIndex];
                return array[position];
            }
            set
            {
                if (index >= size)
                    throw new ArgumentOutOfRangeException();

                var arrayIndex = GetInternalArrayIndex(index);
                var position = index - arrayIndex * arrayMaxSize;
                var array = internalArrays[arrayIndex];
                array[position] = value;
                version++;
            }
        }

        public void Add(T item)
        {
            var index = GetInternalArrayIndex(size);
            var array = GetDestinationArray(ref index);
            var position = internalArraySizes[index];
            array[position] = item;
            internalArraySizes[index]++;
            size++;
            version++;
        }

        private Int32 GetInternalArrayIndex(Int32 index)
        {
            var ratio = (double)index / (double)arrayMaxSize;
            return (Int32)Math.Floor(ratio);
        }

        private T[] GetDestinationArray(ref Int32 arrayIndex)
        {
            T[] array;
            if (internalArrays.Count == arrayIndex)
            {
                array = CreateArrayAtIndex(arrayIndex);
                return array;
            }
            else
            {
                array = internalArrays[arrayIndex];
                if (array.Length == internalArraySizes[arrayIndex])
                {
                    var resizeIndex = Resize(arrayIndex);
                    if (arrayIndex == resizeIndex)
                    {
                        array = internalArrays[arrayIndex];
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
            if (size % arrayMaxSize == 0 || internalArrays.Count == arrayIndex)
            {
                CreateArrayAtIndex(internalArrays.Count);
            }

            EnsureArrayCapacity(arrayIndex);
        }

        private void EnsureArrayCapacity(Int32 arrayIndex)
        {
            if (internalArrays[arrayIndex].Length == internalArraySizes[arrayIndex])
            {
                Resize(arrayIndex);
            }
        }

        private T[] CreateArrayAtIndex(Int32 arrayIndex)
        {
            var array = new T[DEFAULT_ARRAY_SIZE];
            internalArrays.Insert(arrayIndex, array);
            internalArraySizes.Insert(arrayIndex, 0);
            return array;
        }

        private Int32 Resize(Int32 arrayindex)
        {
            var oldSize = internalArraySizes[arrayindex];
            if (arrayMaxSize >> 1 >= oldSize)
            {
                var newSize = oldSize << 1;
                var oldArray = internalArrays[arrayindex];
                var newArray = new T[newSize];
                Array.Copy(oldArray, 0, newArray, 0, oldArray.Length);
                internalArrays[arrayindex] = newArray;

                return arrayindex;
            }
            else
            {
                return ++arrayindex;
            }
        }

        public void Clear()
        {
            internalArrays.Clear();
            internalArraySizes.Clear();
            size = 0;
            version++;
        }

        public bool Contains(T item)
        {
            var comparer = EqualityComparer<T>.Default;
            for (int i = 0; i < internalArrays.Count; i++)
            {
                var array = internalArrays[i];
                for (int j = 0; j < internalArraySizes[i]; j++)
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

            for (int i = 0; i < internalArrays.Count; i++)
            {
                Array.Copy(internalArrays[i], 0, array, i * internalArraySizes[i], internalArraySizes[i]);
            }
        }

        public void ForEach(Action<T> action)
        {
            for (int i = 0; i < internalArrays.Count; i++)
            {
                var array = internalArrays[i];
                for (int j = 0; j < internalArraySizes[i]; j++)
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
            foreach (var array in internalArrays)
            {
                Array.Reverse(array);
            }
            internalArrays.Reverse();

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
