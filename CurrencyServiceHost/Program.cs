using System;
using System.ServiceModel;

namespace CurrencyServiceHost
{
    class Program
    {
        static void Main(string[] args)
        {
           var socketServer=new CurrencyService.SocketServer();
         socketServer.Start();

            using (ServiceHost host = new ServiceHost(typeof(CurrencyService.CurrencyService)))
            {
                host.Open();
                Console.WriteLine("✅ WCF Service is running...");
                Console.WriteLine("Press Enter to stop.");
                Console.ReadLine();
            }
        }
    }
}
