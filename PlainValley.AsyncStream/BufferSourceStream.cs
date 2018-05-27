using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PlainValley.AsyncStream
{
    public class BufferSourceStream: IDisposable
    {
        private TaskCompletionSource<object> _dataWaiter;
        private Exception _bufferingException;
        private CancellationToken _bufferingCancellationToken;

        private int _bufferingStarted;
        private int _bufferingPosition;
        private int _bufferSize;
        private byte[] _buffer;

        private Func<byte[], int, int, Task<int>> _reader;
        private Stream _internalBuffer;

        private int _disposed;

        public BufferSourceStream
            (
                Func<byte[], int, int, Task<int>> reader, 
                Stream internalBuffer, 
                int? bufferSize = null
            )
        {
            _reader = reader;
            _internalBuffer = internalBuffer;
            _bufferSize = bufferSize ?? 4 * 1024;

            _buffer = new byte[_bufferSize];
        }
        /// <summary>
        /// Starts buffering data to internal buffer stream. This method is invoked
        /// automatically when data is read from this object via Read method. If you want to invoke it
        /// manually, consider invoking on thread pool (Task.Run and likes)
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public Task BufferData(CancellationToken cancellationToken) 
            => StartBufferingOnlyOnce()
                ? DoBuffering(cancellationToken)
                : Task.CompletedTask;

        private bool StartBufferingOnlyOnce()
            => Interlocked.CompareExchange(ref _bufferingStarted, 1, 0) == 0;

        async private Task DoBuffering(CancellationToken cancellationToken)
        {
            _bufferingCancellationToken = cancellationToken;

            while (true)
            {
                try
                {
                    var haveMoreData = await ReadNextChunkToInternalBuffer().ConfigureAwait(false);
                    if (!haveMoreData)
                    {
                        break;
                    }
                }
                catch (Exception e)
                {
                    Volatile.Write(ref _bufferingException, e);
                    throw;
                }

                cancellationToken.ThrowIfCancellationRequested();
            }
        }

        /// <summary>
        /// Reads data from internal buffer, automatically waiting for
        /// data to arrive there.
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        async public Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (Volatile.Read(ref _bufferingException) != null)
                throw new AggregateException("Exception occured during buffering data, see inner exception for details", _bufferingException);

            if (StartBufferingOnlyOnce())
            {
                // By default we want to be as robust, as we can, so we start buffering
                // data on the thread pool thread (though user still can, if he wants, to invoke it
                // however he likes via BufferData method)

                var fireAndForget = Task.Run(() => BufferData(CancellationToken.None));
            }

            _bufferingCancellationToken.ThrowIfCancellationRequested();
            await WaitDataToBeAvailable(offset, count, cancellationToken).ConfigureAwait(false);

            return await _internalBuffer.ReadAsync(buffer, offset, count, cancellationToken).ConfigureAwait(false);
        }

        public Task WaitDataToBeAvailable(int position, int length, CancellationToken cancellationToken)
            => CheckDataToBeAvailable(position, length)
                ? Task.CompletedTask
                : DoWaitDataToBeAvailable(position, length, cancellationToken);

        async private Task DoWaitDataToBeAvailable(int position, int length, CancellationToken cancellationToken)
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (CheckDataToBeAvailable(position, length))
                {
                    return;
                }

                // There is race possibiltiy here. Imagine thread 1 checks and sees that no data
                // is available for him, then before he waits again, thread 2 sets new CTS, waits on 
                // it, then data-loading thread signals CTS, thread 2 kicks in, waits again (imagine his 
                // data range still not loaded). If after all that thread 1 procceeds to wait he will wait
                // on new CTS, though it is possible that data range he requested was already downloaded.
                // I guess it is not very likely scenario (and not very dangerous), and we don't expect to have many threads 
                // reading simultaniously anyway.

                Interlocked.CompareExchange(ref _dataWaiter, new TaskCompletionSource<object>(), null);
                await _dataWaiter.Task;
            }
        }

        private bool CheckDataToBeAvailable(int position, int length)
            => Volatile.Read(ref _bufferingPosition) >= position + length;

        private void PulseAllReaders()
            => _dataWaiter.SetResult(null);

        async private Task<bool> ReadNextChunkToInternalBuffer()
        {
            var bytesRead = await _reader(_buffer, _bufferingPosition, _bufferSize).ConfigureAwait(false);
            if (bytesRead == 0)
            {
                return false;
            }

            await _internalBuffer.WriteAsync(_buffer, 0, bytesRead).ConfigureAwait(false);
            Volatile.Write(ref _bufferingPosition, _bufferingPosition + bytesRead);

            PulseAllReaders();
            return true;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected void Dispose(bool disposing)
        {
            if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
                return;

            _internalBuffer.Dispose();
        }

        ~BufferSourceStream()
        {
            Dispose(false);
        }
    }
}
