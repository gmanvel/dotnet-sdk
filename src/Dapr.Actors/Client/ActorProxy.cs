﻿// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Actors.Client
{
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Dapr.Actors.Communication;
    using Dapr.Actors.Communication.Client;
    using Newtonsoft.Json;

    /// <summary>
    /// Provides the base implementation for the proxy to the remote actor objects implementing <see cref="IActor"/> interfaces.
    /// The proxy object can be used used for client-to-actor and actor-to-actor communication.
    /// </summary>
    public class ActorProxy : IActorProxy
    {
        internal static readonly ActorProxyFactory DefaultProxyFactory = new ActorProxyFactory();
        private ActorRemotingClient actorRemotingClient;
        private ActorNonRemotingClient actorNonRemotingClient;

        /// <summary>
        /// Initializes a new instance of the <see cref="ActorProxy"/> class.
        /// This constructor is protected so that it can be used by generated class which derives from ActorProxy when making Remoting calls.
        /// This constructor is also marked as internal so that it can be called by ActorProxyFactory when making non-remoting calls.
        /// </summary>
        protected internal ActorProxy()
        {
        }

        /// <inheritdoc/>
        public ActorId ActorId { get; private set; }

        /// <inheritdoc/>
        public string ActorType { get; private set; }

        internal IActorMessageBodyFactory ActorMessageBodyFactory { get; set; }

        /// <summary>
        /// Creates a proxy to the actor object that implements an actor interface.
        /// </summary>
        /// <typeparam name="TActorInterface">
        /// The actor interface implemented by the remote actor object.
        /// The returned proxy object will implement this interface.
        /// </typeparam>
        /// <param name="actorId">The actor ID of the proxy actor object. Methods called on this proxy will result in requests
        /// being sent to the actor with this ID.</param>
        /// <param name="actorType">
        /// Type of actor implementation.
        /// </param>
        /// <returns>Proxy to the actor object.</returns>
        public static TActorInterface Create<TActorInterface>(ActorId actorId, string actorType)
            where TActorInterface : IActor
        {
            return DefaultProxyFactory.CreateActorProxy<TActorInterface>(actorId, actorType);
        }

        /// <summary>
        /// Creates an Actor Proxy for making calls without Remoting.
        /// </summary>
        /// <param name="actorId">Actor Id.</param>
        /// <param name="actorType">Type of actor.</param>
        /// <returns>Actor proxy to interact with remote actor object.</returns>
        public static ActorProxy Create(ActorId actorId, string actorType)
        {
            return DefaultProxyFactory.Create(actorId, actorType);
        }

        /// <summary>
        /// Invokes the specified method for the actor with argument. The argument will be serialized as json.
        /// </summary>
        /// <typeparam name="T">Return type of method.</typeparam>
        /// <param name="method">Actor method name.</param>
        /// <param name="data">Object argument for actor method.</param>
        /// <param name="cancellationToken">Cancellation Token.</param>
        /// <returns>Response form server.</returns>
        public async Task<T> InvokeAsync<T>(string method, object data, CancellationToken cancellationToken = default)
        {
            // TODO: Allow users to provide a custom Serializer.
            var serializer = new JsonSerializer();
            var jsonPayload = JsonConvert.SerializeObject(data);
            var response = await this.actorNonRemotingClient.InvokeActorMethodWithoutRemotingAsync(this.ActorType, this.ActorId.ToString(), method, jsonPayload, cancellationToken);

            using var streamReader = new StreamReader(response);
            using var reader = new JsonTextReader(streamReader);
            return serializer.Deserialize<T>(reader);
        }

        /// <summary>
        /// Invokes the specified method for the actor with argument. The argument will be serialized as json.
        /// </summary>
        /// <param name="method">Actor method name.</param>
        /// <param name="data">Object argument for actor method.</param>
        /// <param name="cancellationToken">Cancellation Token.</param>
        /// <returns>Response form server.</returns>
        public Task InvokeAsync(string method, object data, CancellationToken cancellationToken = default)
        {
            var jsonPayload = JsonConvert.SerializeObject(data);
            return this.actorNonRemotingClient.InvokeActorMethodWithoutRemotingAsync(this.ActorType, this.ActorId.ToString(), method, jsonPayload, cancellationToken);
        }

        /// <summary>
        /// Invokes the specified method for the actor with argument.
        /// </summary>
        /// <typeparam name="T">Return type of method.</typeparam>
        /// <param name="method">Actor method name.</param>
        /// <param name="cancellationToken">Cancellation Token.</param>
        /// <returns>Response form server.</returns>
        public async Task<T> InvokeAsync<T>(string method, CancellationToken cancellationToken = default)
        {
            var response = await this.actorNonRemotingClient.InvokeActorMethodWithoutRemotingAsync(this.ActorType, this.ActorId.ToString(), method, null, cancellationToken);
            var serializer = new JsonSerializer();

            using var streamReader = new StreamReader(response);
            using var reader = new JsonTextReader(streamReader);
            return serializer.Deserialize<T>(reader);
        }

        /// <summary>
        /// Invokes the specified method for the actor with argument.
        /// </summary>
        /// <param name="method">Actor method name.</param>
        /// <param name="cancellationToken">Cancellation Token.</param>
        /// <returns>Response form server.</returns>
        public Task InvokeAsync(string method, CancellationToken cancellationToken = default)
        {
            return this.actorNonRemotingClient.InvokeActorMethodWithoutRemotingAsync(this.ActorType, this.ActorId.ToString(), method, null, cancellationToken);
        }

        /// <summary>
        /// Initialize whencACtorProxy is created for Remoting.
        /// </summary>
        internal void Initialize(
          ActorRemotingClient client,
          ActorId actorId,
          string actorType)
        {
            this.actorRemotingClient = client;
            this.ActorId = actorId;
            this.ActorType = actorType;
            this.ActorMessageBodyFactory = client.GetRemotingMessageBodyFactory();
        }

        /// <summary>
        /// Initialize whenc ActorProxy is created for non-Remoting calls.
        /// </summary>
        internal void Initialize(
          ActorNonRemotingClient client,
          ActorId actorId,
          string actorType)
        {
            this.actorNonRemotingClient = client;
            this.ActorId = actorId;
            this.ActorType = actorType;
        }

        /// <summary>
        /// Invokes the specified method for the actor with provided request.
        /// </summary>
        /// <param name="interfaceId">Interface ID.</param>
        /// <param name="methodId">Method ID.</param>
        /// <param name="methodName">Method Name.</param>
        /// <param name="requestMsgBodyValue">Request Message Body Value.</param>
        /// <param name="cancellationToken">Cancellation Token.</param>
        /// <returns>A <see cref="Task{TResult}"/> representing the result of the asynchronous operation.</returns>
        protected async Task<IActorResponseMessageBody> InvokeAsync(
            int interfaceId,
            int methodId,
            string methodName,
            IActorRequestMessageBody requestMsgBodyValue,
            CancellationToken cancellationToken)
        {
            var headers = new ActorRequestMessageHeader
            {
                ActorId = this.ActorId,
                ActorType = this.ActorType,
                InterfaceId = interfaceId,
                MethodId = methodId,
                CallContext = Actors.Helper.GetCallContext(),
                MethodName = methodName,
            };

            var responseMsg = await this.actorRemotingClient.InvokeAsync(
                new ActorRequestMessage(
                headers,
                requestMsgBodyValue),
                methodName,
                cancellationToken);

            return responseMsg?.GetBody();
        }

        /// <summary>
        /// Creates the Actor request message Body.
        /// </summary>
        /// <param name="interfaceName">Full Name of the service interface for which this call is invoked.</param>
        /// <param name="methodName">Method Name of the service interface for which this call is invoked.</param>
        /// <param name="parameterCount">Number of Parameters in the service interface Method.</param>
        /// <param name="wrappedRequest">Wrapped Request Object.</param>
        /// <returns>A request message body.</returns>
        protected IActorRequestMessageBody CreateRequestMessageBody(
            string interfaceName,
            string methodName,
            int parameterCount,
            object wrappedRequest)
        {
            return this.ActorMessageBodyFactory.CreateRequestMessageBody(interfaceName, methodName, parameterCount, wrappedRequest);
        }

        /// <summary>
        /// This method is used by the generated proxy type and should be used directly. This method converts the Task with object
        /// return value to a Task without the return value for the void method invocation.
        /// </summary>
        /// <param name="task">A task returned from the method that contains null return value.</param>
        /// <returns>A task that represents the asynchronous operation for remote method call without the return value.</returns>
        protected Task ContinueWith(Task<object> task)
        {
            return task;
        }

        /// <summary>
        /// This method is used by the generated proxy type and should be used directly. This method converts the Task with object
        /// return value to a Task without the return value for the void method invocation.
        /// </summary>
        /// <param name="interfaceId">Interface Id for the actor interface.</param>
        /// <param name="methodId">Method Id for the actor method.</param>
        /// <param name="responseBody">Response body.</param>
        /// <returns>Return value of method call as <see cref="object"/>.</returns>
        protected virtual object GetReturnValue(int interfaceId, int methodId, object responseBody)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Called by the generated proxy class to get the result from the response body.
        /// </summary>
        /// <typeparam name="TRetval"><see cref="System.Type"/> of the remote method return value.</typeparam>
        /// <param name="interfaceId">InterfaceId of the remoting interface.</param>
        /// <param name="methodId">MethodId of the remoting Method.</param>
        /// <param name="task">A task that represents the asynchronous operation for remote method call.</param>
        /// <returns>A task that represents the asynchronous operation for remote method call.
        /// The value of the TRetval contains the remote method return value. </returns>
        protected async Task<TRetval> ContinueWithResult<TRetval>(
            int interfaceId,
            int methodId,
            Task<IActorResponseMessageBody> task)
        {
            var responseBody = await task;
            if (responseBody is WrappedMessage wrappedMessage)
            {
                var obj = this.GetReturnValue(
                    interfaceId,
                    methodId,
                    wrappedMessage.Value);

                return (TRetval)obj;
            }

            return (TRetval)responseBody.Get(typeof(TRetval));
        }

        /// <summary>
        /// This check if we are wrapping actor message or not.
        /// </summary>
        /// <param name="requestMessageBody">Actor Request Message Body.</param>
        /// <returns>true or false. </returns>
        protected bool CheckIfItsWrappedRequest(IActorRequestMessageBody requestMessageBody)
        {
            if (requestMessageBody is WrappedMessage)
            {
                return true;
            }

            return false;
        }
    }
}
