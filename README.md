# LargeList<T> #

**.NET generic IList implementations that avoids Large Object Heap allocations.**

Currently objects larger than 85.000 bytes are allocated on the Large Object Heap, which is never compacted during Garbage collection. 

This can cause memory fragmentation and holes in the heap. 

LargeList is backed by array of arrays. 
Each array has it's max size property set based on the type it is holding, so it never grows over 85.000 bytes in size. 
And thus each array sits in regular heap.


Written by [Marko Devcic](http://www.markodevcic.com)

### License

[MIT](https://opensource.org/licenses/MIT)