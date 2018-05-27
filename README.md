# asyncstream
Async Stream Buffer

This simple implementation allows to buffer some generic byte stream into another stream, and at the same time allows to consume this buffer stream by multiple readers in asyncronous fashion. Any requests to read data outside of what have been buffered so far will wait (asyncronously) until data becomes available.

Usage example:

```csharp
Stream inputStream = GetDataStream();
AsyncBufferStream bufferStream = BufferStream.Create
	(
		// Reads data from source
		(buffer, offset, count) => inputStream.ReadAsync(buffer, offset, count)
	)
	
	// ...And buffers it to file. If fact, you can buffer it to any stream with read-write access
	.WithFileBuffer("C:\\temp\\tempfile.bin");

/* Read from bufferStream using ReadAsync. If you request data range that wasn't buffered yet, 
   returned task will wait until data becomes available */
```