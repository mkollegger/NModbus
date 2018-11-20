using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.SerialCommunication;
using Windows.Storage.Streams;
using NModbus.IO;

namespace NModbus.Serial
{
    /// <summary>
    /// UWP SerialDevice Modbus Adapter
    /// </summary>
    /// <remarks>Contributed by https://github.com/LGinC </remarks>
    public class SerialDeviceAdapter : IStreamResource
    {
        private readonly SerialDevice _serialDevice;
        private readonly DataReader inputStream;
        private readonly DataWriter outputStream;

        private int _readTimeOutMs;
        private int _writeTimeOutMs;


        public int InfiniteTimeout => Timeout.Infinite;

        public int ReadTimeout
        {
            get => _readTimeOutMs;
            set
            {
                if (_readTimeOutMs == value)
                    return;
                _readTimeOutMs = value;
                _serialDevice.ReadTimeout = TimeSpan.FromMilliseconds(_readTimeOutMs);
            }
        }

        public int WriteTimeout
        {
            get => _writeTimeOutMs;
            set
            {
                if (value == _writeTimeOutMs)
                    return;
                _writeTimeOutMs = value;
                _serialDevice.WriteTimeout = TimeSpan.FromMilliseconds(_writeTimeOutMs);
            }
        }

        public SerialDeviceAdapter(SerialDevice serialDevice)
        {
            Debug.Assert(serialDevice != null, "Argument serialDevice cannot be null");
            _serialDevice = serialDevice;
            inputStream = new DataReader(_serialDevice.InputStream);
            inputStream.InputStreamOptions = InputStreamOptions.Partial;
            outputStream = new DataWriter(_serialDevice.OutputStream);
        }

        public void DiscardInBuffer()
        {
            inputStream.ReadBytes(new byte[inputStream.UnconsumedBufferLength]);
        }

        public void Dispose()
        {
            _serialDevice.Dispose();
        }

        public int Read(byte[] buffer, int offset, int count)
        {
            bool timeout = false;

            var readToken = new ManualResetEventSlim(false); 
            var cts = new CancellationTokenSource(ReadTimeout);

            Task.Run(async () =>
            {
                try
                {
                    await inputStream.LoadAsync((uint) (count + offset)).AsTask(cts.Token);
                }
                catch 
                {
                    timeout = true;
                }
                finally
                {
                    readToken.Set();
                }
                
            });
            readToken.Wait();
            if (timeout)
            {
                throw new TimeoutException();
            }
            else
            {
                int result = 0;
                if (inputStream.UnconsumedBufferLength > 0)
                {
                    inputStream.ReadBytes(new byte[offset]);
                    inputStream.ReadBytes(buffer);
                    result = buffer.Length;
                }
                return result;
            }
        }

        public void Write(byte[] buffer, int offset, int count)
        {
            bool timeout = false;

            var writeToken = new ManualResetEventSlim(false);
            var cts = new CancellationTokenSource(WriteTimeout);

            Task.Run(async () =>
            {
                try
                {
                    outputStream.WriteBytes(buffer);
                    await outputStream.StoreAsync().AsTask(cts.Token);
                }
                catch
                {
                    timeout = true;
                }
                finally
                {
                    writeToken.Set();
                }

            });
            writeToken.Wait();
            if (timeout)
            {
                throw new TimeoutException();
            }
        }
    }
}
