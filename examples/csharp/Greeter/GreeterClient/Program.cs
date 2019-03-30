using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using GossipMesh.Core;
using GossipMesh.LoadBalancing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Grpc.Core;
using Greeter;
using Helloworld;
using System.Diagnostics;
using System.Collections.Generic;

namespace GreeterClient
{
    class Program
    {
        public static async Task Main(string[] args)
        {
            var listenPort = ushort.Parse(args[0]);
            var seeds = args.Skip(1).Select(IPEndPointFromString).ToArray();

            var logger = CreateLogger();

            var loadBalancer = CreateLoadBalancer();
            var gossiper = await StartGossiper(listenPort, seeds, new IListener[] { loadBalancer }, logger);

            var stopwatch = new Stopwatch();
            while (true)
            {
                try
                {
                    Console.Write("Please enter your name: ");
                    var name = Console.ReadLine();
                    stopwatch.Restart();

                    var serviceClient = loadBalancer.GetServiceClient<GreeterServiceClient>(2);

                    var request = new HelloRequest{ Name = name};
                    var response = await serviceClient.Client.SayHelloAsync(request).ResponseAsync.ConfigureAwait(false);

                    stopwatch.Stop();
                    Console.WriteLine($"Response: {response.Message} From: {serviceClient.ServiceEndPoint} TimeTaken: {stopwatch.Elapsed.TotalMilliseconds}ms");
                }

                catch (Exception ex)
                {
                    logger.LogError(ex.Message);
                }
            }
        }

        private static ILogger CreateLogger()
        {
            var loggerFactory = new LoggerFactory();
            loggerFactory.AddProvider(new ConsoleLoggerProvider());
            return loggerFactory
                .CreateLogger<Program>();
        }

        private static RandomLoadBalancer CreateLoadBalancer()
        {
            var serviceClientFactories = new Dictionary<byte, IServiceClientFactory>
            {
                { 2, new GreeterServiceClientFactory()}
            };

            return new RandomLoadBalancer(serviceClientFactories);
        }

        private static async Task<Gossiper> StartGossiper(ushort listenPort, IPEndPoint[] seeds, IListener[] listeners, ILogger logger)
        {
            var options = new GossiperOptions
            {
                SeedNodes = seeds,
                Listeners = listeners
            };

            var gossiper = new Gossiper(listenPort, 0x03, listenPort, options, logger);
            await gossiper.StartAsync();

            return gossiper;
        }

        private static IPEndPoint IPEndPointFromString(string ipEndPointString)
        {
            var endpoint = ipEndPointString.Split(":");
            return new IPEndPoint(IPAddress.Parse(endpoint[0]), int.Parse(endpoint[1]));
        }
    }
}