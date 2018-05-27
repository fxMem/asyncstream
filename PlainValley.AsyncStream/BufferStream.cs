using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace PlainValley.AsyncStream
{
    public static class BufferStream
    {
        public static AsyncBufferStreamBuilder Create(Func<byte[], int, int, Task<int>> readDelegate)
            => new AsyncBufferStreamBuilder(readDelegate);

    }

    public class AsyncBufferStreamBuilder
    {
        private Func<byte[], int, int, Task<int>> _readDelegate;

        public AsyncBufferStreamBuilder(Func<byte[], int, int, Task<int>> readDelegate) 
            => _readDelegate = readDelegate;

        public AsyncBufferStream WithMemoryBuffer()
            => WithStream(new MemoryStream());

        public AsyncBufferStream WithFileBuffer(string filename, int? bufferSize = null)
            => WithStream(new FileStream(filename, FileMode.Create, FileAccess.ReadWrite, FileShare.None, bufferSize ?? 4 * 1024, useAsync: true));

        public AsyncBufferStream WithStream(Stream stream)
            => new BufferSourceStream(_readDelegate, stream).Then(buffer => new AsyncBufferStream(buffer));
    }
}
