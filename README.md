# LargeList<T> #

**.NET generic IList implementations that avoids Large Object Heap allocations.**

Currently objects larger than 85.000 bytes are allocated on the Large Object Heap, which is never compacted during Garbage collection. 

This can cause memory fragmentation and holes in the heap. 

OutOfMemory Exceptions can be thrown even if requested memory is available in the address space.

LargeList is backed by array of arrays. 
Each array has it's max size property set based on the type it is holding, so it never grows over 85.000 bytes in size. 
And thus is each array sits in regular heap.