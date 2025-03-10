﻿// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace ActorClient
{
    using System;
    using System.Threading.Tasks;
    using Dapr.Actors;
    using Dapr.Actors.Client;
    using IDemoActorInterface;

    /// <summary>
    /// Actor Client class.
    /// </summary>
    public class Program
    {
        /// <summary>
        /// Entry point.
        /// </summary>
        /// <param name="args">Arguments.</param>
        public static void Main(string[] args)
        {
            MakeActorCalls().GetAwaiter().GetResult();
        }

        /// <summary>
        /// A wrapper method which make actual calls.
        /// </summary>
        /// <returns>A task that represents the asynchronous save operation.</returns>
        public static async Task MakeActorCalls()
        {
            var data = new MyData()
            {
                PropertyA = "ValueA",
                PropertyB = "ValueB",
            };

            // Create an actor Id.
            var actorId = new ActorId("abc");

            // Make strongly typed Actor calls with Remoting.
            // DemoACtor is the type registered with Dapr runtime in the service.
            var proxy = ActorProxy.Create<IDemoActor>(actorId, "DemoActor");
            Console.WriteLine("Making call using actor proxy to save data.");
            await proxy.SaveData(data);
            Console.WriteLine("Making call using actor proxy to get data.");
            var receivedData = await proxy.GetData();
            Console.WriteLine($"Received data is {receivedData.ToString()}");

            // Making some more calls to test methods.
            try
            {
                Console.WriteLine("Making calls to an actor method which has no argument and no return type.");
                await proxy.TestNoArgumentNoReturnType();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: Got exception while making call to method with No Argument & No Return Type. Exception: {ex.ToString()}");
            }

            try
            {
                await proxy.TestThrowException();
            }
            catch (ActorMethodInvocationException ex)
            {
                if (ex.InnerException is NotImplementedException)
                {
                    Console.WriteLine($"Got Correct Exception from actor method invocation.");
                }
                else
                {
                    Console.WriteLine($"Got Incorrect Exception from actor method invocation. Exception {ex.InnerException.ToString()}");
                }
            }
        }
    }
}
