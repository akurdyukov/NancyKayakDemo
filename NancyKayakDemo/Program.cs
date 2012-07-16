using System;
using Nancy;

namespace NancyKayakDemo
{
    class Program
    {
        static void Main(string[] args)
        {
            // Create a new Nancy instance
            var nancy = new Nancy.Hosting.Owin.NancyOwinHost();

            // Create a new OwinServer (based on Kayak)
            var server = new OwinServer(nancy.ProcessRequest);

            // Start the OwinServer
            server.Start(System.Net.IPAddress.Any, 8080);

            Console.WriteLine("Nancy+Owin+Kayak is listening at 8080");
            Console.ReadLine();

            server.Stop();
        }
    }

    public class MainModule : NancyModule
    {
        public MainModule()
        {
            Get["/"] = _ => "Hello, World!";
        }
    }
}
