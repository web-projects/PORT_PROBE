using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Text;
using System.Threading;

namespace PortProbe
{
    public class SerialConnection : IDisposable
    {
        string commPort;

        SerialPort serialPort;
        const int portReadTimeout = 10000;
        const int portWriteTimeout = 10000;

        bool connected;
        bool lastCDHolding;
        bool readContinue = true;

        Thread readThread;

        ResponseBytesHandlerDelegate ResponseBytesHandler;
        public delegate void ResponseBytesHandlerDelegate(byte[] msg);

        public bool Connect(string port, bool exposeExceptions = false)
        {
            commPort = port;
            connected = false;

            try
            {
                // Create a new SerialPort object with default settings.
                serialPort = new SerialPort(commPort);

                // Update the Handshake
                serialPort.Handshake = Handshake.None;

                // Set the read/write timeouts
                serialPort.ReadTimeout = portReadTimeout;
                serialPort.WriteTimeout = portWriteTimeout;

                // open serial port
                serialPort.Open();

                // monitor port changes
                lastCDHolding = serialPort.CDHolding;

                // discard any buffered bytes
                serialPort.DiscardInBuffer();
                serialPort.DiscardOutBuffer();

                // Setup read thread
                readThread = new Thread(ReadResponseBytes);

                readThread.Start();
                ResponseBytesHandler += ReadResponses;

                Console.WriteLine($"SERIAL: ON PORT={commPort} - CONNECTION OPEN");
                System.Diagnostics.Debug.WriteLine($"VIPA [{serialPort?.PortName}]: opened port.");

                connected = true;
            }
            catch (Exception e)
            {
                Console.WriteLine($"SERIAL: ON PORT={commPort} - exception=[{e.Message}]");

                if (exposeExceptions)
                {
                    throw;
                }

                Dispose();
            }

            return connected;
        }

        public bool IsConnected()
        {
            return connected;
        }

        public void Disconnect(bool exposeExceptions = false)
        {
            if (serialPort?.IsOpen ?? false)
            {
                try
                {
                    readContinue = false;
                    connected = false;
                    Thread.Sleep(1000);

                    readThread.Join(1000);
                    ResponseBytesHandler -= ReadResponses;

                    // discard any buffered bytes
                    serialPort.DiscardInBuffer();
                    serialPort.DiscardOutBuffer();

                    serialPort.Close();

                    System.Diagnostics.Debug.WriteLine($"VIPA [{serialPort?.PortName}]: closed port.");
                }
                catch (Exception)
                {
                    if (exposeExceptions)
                    {
                        throw;
                    }
                }
            }
        }

        public void WriteSingleCmd(byte[] command)
        {
            if (command?.Length > 0)
            {
                WriteBytes(command);
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            Disconnect();

            if (disposing)
            {
                serialPort?.Dispose();
                serialPort = null;
            }

            // https://docs.microsoft.com/en-us/dotnet/api/system.io.ports.serialport.open?view=dotnet-plat-ext-3.1#System_IO_Ports_SerialPort_Open
            // SerialPort has a quirk (aka bug) where needs time to let a worker thread exit:
            //    "The best practice for any application is to wait for some amount of time after calling the Close method before
            //     attempting to call the Open method, as the port may not be closed instantly".
            // The amount of time is unspecified and unpredictable.
            Thread.Sleep(250);
        }

        void WriteBytes(byte[] msg)
        {
            try
            {
                serialPort?.Write(msg, 0, msg.Length);
            }
            catch (TimeoutException e)
            {
                Console.WriteLine($"SerialConnection: exception=[{e.Message}]");
            }
        }

        [System.Diagnostics.DebuggerNonUserCode]
        private void ReadResponseBytes()
        {
            while (readContinue)
            {
                try
                {
                    if (serialPort?.IsOpen ?? false)
                    {
                        byte[] bytes = new byte[256];
                        var readLength = serialPort?.Read(bytes, 0, bytes.Length) ?? 0;
                        if (readLength > 0)
                        {
                            byte[] readBytes = new byte[readLength];
                            Array.Copy(bytes, 0, readBytes, 0, readLength);
#if DEBUG
                            System.Diagnostics.Debug.WriteLine($"VIPA-READ [{serialPort?.PortName}]: {BitConverter.ToString(readBytes)}");
#endif
                            ResponseBytesHandler(readBytes);
                        }
                    }
                }
                catch (TimeoutException)
                {
                }
                // TODO: remove unnecessary catches after POC for multi-device is shakendown
                catch (InvalidOperationException)
                {
                }
                catch (OperationCanceledException)
                {
                }
                catch (NullReferenceException)
                {
                }
                catch (IOException)
                {
                }
            }
        }

        private void ReadResponses(byte[] responseBytes)
        {
            Console.WriteLine($"VIPA-READ [{serialPort?.PortName}]: {BitConverter.ToString(responseBytes)}");
        }
    }
}
