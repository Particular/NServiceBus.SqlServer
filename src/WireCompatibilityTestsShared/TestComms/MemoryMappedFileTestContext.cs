namespace TestComms
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.IO.MemoryMappedFiles;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    public class MemoryMappedFileTestContext : ITestContextAccessor, IDisposable
    {
        readonly bool owner;
        readonly MemoryMappedFile file;
        readonly Mutex mutex;
        readonly MemoryMappedViewAccessor accessor;
        readonly Dictionary<string, int> cache = new Dictionary<string, int>();

        public MemoryMappedFileTestContext(string name, bool create = false)
        {
            owner = create;
            if (owner)
            {
                file = MemoryMappedFile.CreateFromFile(name, FileMode.CreateNew, name, 1024, MemoryMappedFileAccess.ReadWrite);
                mutex = new Mutex(false, Path.GetFileName(name) + "mutex", out _);
            }
            else
            {
#pragma warning disable CA1416
                file = MemoryMappedFile.OpenExisting(name, MemoryMappedFileRights.ReadWrite);
#pragma warning restore CA1416
                mutex = Mutex.OpenExisting(name + "mutex");
            }

            accessor = file.CreateViewAccessor(0, 0);
        }

        public void SetFlag(string name, bool value)
        {
            Set(name, value ? 1 : 0);
        }

        public bool GetFlag(string name)
        {
            return Get(name) > 0;
        }

        public void Set(string name, int value)
        {
            lock (this)
            {
                try
                {
                    mutex.WaitOne();

                    if (cache.TryGetValue(name, out var cachedPointer))
                    {
                        accessor.Write(cachedPointer, value);
                        accessor.Flush();
                        return;
                    }

                    var numberOfEntries = accessor.ReadInt32(0);

                    var currentPointer = 4;

                    for (var i = 0; i < numberOfEntries; i++)
                    {
                        var nameLength = accessor.ReadInt32(currentPointer);
                        currentPointer += 4;
                        var nameArray = new byte[nameLength];
                        accessor.ReadArray(nameLength, nameArray, 0, nameLength);
                        currentPointer += nameLength;
                        var entryName = Encoding.UTF8.GetString(nameArray);

                        //Cache the property pointer
                        cache[name] = currentPointer;

                        if (entryName == name)
                        {
                            accessor.Write(currentPointer, value);
                            accessor.Flush();
                            return;
                        }

                        //Skip the value
                        currentPointer += 4;
                    }

                    //Write the name
                    var encodedName = Encoding.UTF8.GetBytes(name);
                    accessor.Write(currentPointer, encodedName.Length);
                    currentPointer += 4;
                    accessor.WriteArray(currentPointer, encodedName, 0, encodedName.Length);
                    currentPointer += encodedName.Length;

                    accessor.Write(currentPointer, value);

                    //Increment the number of entries
                    accessor.Write(0, numberOfEntries + 1);
                    accessor.Flush();

                    //Cache the property pointer
                    cache[name] = currentPointer;
                }
                finally
                {
                    mutex.ReleaseMutex();
                }
            }
        }

        public int Get(string name)
        {
            lock (this)
            {
                try
                {
                    mutex.WaitOne();

                    if (cache.TryGetValue(name, out var cachedPointer))
                    {
                        return accessor.ReadInt32(cachedPointer);
                    }

                    var numberOfEntries = accessor.ReadInt32(0);

                    var currentPointer = 4;

                    for (var i = 0; i < numberOfEntries; i++)
                    {
                        var nameLength = accessor.ReadInt32(currentPointer);
                        currentPointer += 4;
                        var nameArray = new byte[nameLength];
                        accessor.ReadArray(nameLength, nameArray, 0, nameLength);
                        currentPointer += nameLength;
                        var entryName = Encoding.UTF8.GetString(nameArray);

                        //Cache the property pointer
                        cache[name] = currentPointer;

                        if (entryName == name)
                        {
                            return accessor.ReadInt32(currentPointer);
                        }
                    }

                    return int.MinValue; //TODO?

                }
                finally
                {
                    mutex.ReleaseMutex();
                }
            }
        }

        public void Increment(string name, int value)
        {
            throw new NotImplementedException();
        }

        public async Task<bool> WaitUntilTrue(string flagName, CancellationToken cancellationToken = default)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var value = GetFlag(flagName);
                if (value)
                {
                    return true;
                }

                // Do not forward to prevent OperationCancelledException
#pragma warning disable CA2016 // Forward the 'CancellationToken' parameter to methods
                await Task.Delay(100).ConfigureAwait(false);
#pragma warning restore CA2016 // Forward the 'CancellationToken' parameter to methods
            }
            return false;
        }

        public void Success()
        {
            SetFlag("Success", true);
        }

        public void Failure()
        {
            SetFlag("Failure", true);
        }

        public Dictionary<string, object> ToDictionary()
        {
            var result = new Dictionary<string, object>();

            lock (this)
            {
                try
                {
                    mutex.WaitOne();

                    var numberOfEntries = accessor.ReadInt32(0);

                    var currentPointer = 4;

                    for (var i = 0; i < numberOfEntries; i++)
                    {
                        var nameLength = accessor.ReadInt32(currentPointer);
                        currentPointer += 4;
                        var nameArray = new byte[nameLength];
                        accessor.ReadArray(currentPointer, nameArray, 0, nameLength);
                        currentPointer += nameLength;
                        var entryName = Encoding.UTF8.GetString(nameArray);

                        result[entryName] = accessor.ReadInt32(currentPointer);
                        currentPointer += 4;
                    }
                }
                finally
                {
                    mutex.ReleaseMutex();
                }
            }

            return result;
        }

        public void Dispose()
        {
            if (owner)
            {
                mutex.Dispose();
                file.Dispose();
                //File.Delete(name);
            }
            else
            {
                file.Dispose();
            }
        }
    }
}