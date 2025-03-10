﻿// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Actors.Runtime
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Contains methods to register actor types. Registering the types allows the runtime to create instances of the actor.
    /// </summary>
    public class ActorRuntime
    {
        /// <summary>
        /// Gets ActorRuntime.
        /// </summary>
        public static readonly ActorRuntime Instance = new ActorRuntime();

        private const string TraceType = "ActorRuntime";

        // Map of ActorType --> ActorManager.
        private static Dictionary<string, ActorManager> actorManagers = new Dictionary<string, ActorManager>();

        private ActorRuntime()
        {
        }

        /// <summary>
        /// Gets actor type names registered with the runtime.
        /// </summary>
        public static IEnumerable<string> RegisteredActorTypes => actorManagers.Keys;

        internal static IDaprInteractor DaprInteractor => new DaprHttpInteractor();

        /// <summary>
        /// Registers an actor with the runtime.
        /// </summary>
        /// <typeparam name="TActor">Type of actor.</typeparam>
        /// <param name="actorServiceFactory">An optional delegate to create actor service. This can be used for dependency injection into actors.</param>
        public void RegisterActor<TActor>(Func<ActorTypeInformation, ActorService> actorServiceFactory = null)
            where TActor : Actor
        {
            var actorTypeName = typeof(TActor).Name;
            var actorTypeInfo = ActorTypeInformation.Get(typeof(TActor));

            ActorService actorService;
            if (actorServiceFactory != null)
            {
                actorService = actorServiceFactory.Invoke(actorTypeInfo);
            }
            else
            {
                actorService = new ActorService(actorTypeInfo);
            }

            // Create ActorManagers, override existing entry if registered again.
            actorManagers[actorTypeName] = new ActorManager(actorService);
        }

        /// <summary>
        /// Activates an actor for an actor type with given actor id.
        /// </summary>
        /// <param name="actorTypeName">Actor type name to activate the actor for.</param>
        /// <param name="actorId">Actor id for the actor to be activated.</param>
        /// <returns>A <see cref="Task"/> representing the result of the asynchronous operation.</returns>
        internal static async Task ActivateAsync(string actorTypeName, string actorId)
        {
            await GetActorManager(actorTypeName).ActivateActor(new ActorId(actorId));
        }

        /// <summary>
        /// Deactivates an actor for an actor type with given actor id.
        /// </summary>
        /// <param name="actorTypeName">Actor type name to deactivate the actor for.</param>
        /// <param name="actorId">Actor id for the actor to be deactivated.</param>
        /// <returns>A <see cref="Task"/> representing the result of the asynchronous operation.</returns>
        internal static async Task DeactivateAsync(string actorTypeName, string actorId)
        {
            await GetActorManager(actorTypeName).DeactivateActor(new ActorId(actorId));
        }

        /// <summary>
        /// Invokes the specified method for the actor when used with Remoting from CSharp client.
        /// </summary>
        /// <param name="actorTypeName">Actor type name to invokde the method for.</param>
        /// <param name="actorId">Actor id for the actor for which method will be invoked.</param>
        /// <param name="actorMethodName">MEthos name on actor type which will be invoked.</param>
        /// <param name="daprActorheader">Actor Header.</param>
        /// <param name="data">Payload for the actor method.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <returns>A <see cref="Task"/> representing the result of the asynchronous operation.</returns>
        internal static Task<Tuple<string, byte[]>> DispatchWithRemotingAsync(string actorTypeName, string actorId, string actorMethodName, string daprActorheader, Stream data, CancellationToken cancellationToken = default)
        {
            return GetActorManager(actorTypeName).DispatchWithRemotingAsync(new ActorId(actorId), actorMethodName, daprActorheader, data, cancellationToken);
        }

        /// <summary>
        /// Invokes the specified method for the actor when used without remoting, this is mainly used for cross language invocation.
        /// </summary>
        /// <param name="actorTypeName">Actor type name to invokde the method for.</param>
        /// <param name="actorId">Actor id for the actor for which method will be invoked.</param>
        /// <param name="actorMethodName">MEthos name on actor type which will be invoked.</param>
        /// <param name="requestBodyStream">Payload for the actor method.</param>
        /// <param name="responseBodyStream">Response for the actor method.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <returns>A <see cref="Task"/> representing the result of the asynchronous operation.</returns>
        internal static Task DispatchWithoutRemotingAsync(string actorTypeName, string actorId, string actorMethodName, Stream requestBodyStream, Stream responseBodyStream, CancellationToken cancellationToken = default)
        {
            return GetActorManager(actorTypeName).DispatchWithoutRemotingAsync(new ActorId(actorId), actorMethodName, requestBodyStream, responseBodyStream, cancellationToken);
        }

        /// <summary>
        /// Fires a reminder for the Actor.
        /// </summary>
        /// <param name="actorTypeName">Actor type name to invokde the method for.</param>
        /// <param name="actorId">Actor id for the actor for which method will be invoked.</param>
        /// <param name="reminderName">The name of reminder provided during registration.</param>
        /// <param name="requestBodyStream">Payload for the actor method.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <returns>A <see cref="Task"/> representing the result of the asynchronous operation.</returns>
        internal static Task FireReminderAsync(string actorTypeName, string actorId, string reminderName, Stream requestBodyStream, CancellationToken cancellationToken = default)
        {
            return GetActorManager(actorTypeName).FireReminderAsync(new ActorId(actorId), reminderName, requestBodyStream, cancellationToken);
        }

        /// <summary>
        /// Fires a timer for the Actor.
        /// </summary>
        /// <param name="actorTypeName">Actor type name to invokde the method for.</param>
        /// <param name="actorId">Actor id for the actor for which method will be invoked.</param>
        /// <param name="timerName">The name of timer provided during registration.</param>
        /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
        /// <returns>A <see cref="Task"/> representing the result of the asynchronous operation.</returns>
        internal static Task FireTimerAsync(string actorTypeName, string actorId, string timerName, CancellationToken cancellationToken = default)
        {
            return GetActorManager(actorTypeName).FireTimerAsync(new ActorId(actorId), timerName, cancellationToken);
        }

        private static ActorManager GetActorManager(string actorTypeName)
        {
            if (!actorManagers.TryGetValue(actorTypeName, out var actorManager))
            {
                var errorMsg = $"Actor type {actorTypeName} is not registerd with Actor runtime.";
                ActorTrace.Instance.WriteError(TraceType, errorMsg);
                throw new InvalidOperationException(errorMsg);
            }

            return actorManager;
        }
    }
}
