using System;
using System.Threading.Tasks;

namespace PortProbe
{
    class Program
    {
        static readonly byte[] resetDisplay = new byte[]{ 0x01, 0x00, 0x04, 0xD2, 0x01, 0x01, 0x00, 0xD7 };

        static async Task Main(string[] args)
        {
            string comPort = "COM9";
            SerialConnection connection = new SerialConnection();    
            Console.WriteLine($"CONNECTING TO PORT {comPort}");
            connection.Connect(comPort, true);
            if (connection.IsConnected())
            {
                Console.WriteLine($"CONNECTED TO PORT {comPort}");

                
                Console.WriteLine($"SENDING MESSAGE [{BitConverter.ToString(resetDisplay)}] TO PORT {comPort}...");
                connection.WriteSingleCmd(resetDisplay);
                await Task.Delay(5000);
                Console.WriteLine($"DISCONNECTING FROM PORT {comPort}");
                connection.Disconnect();
            }
            Console.WriteLine("DONE.");
        }
    }
}
