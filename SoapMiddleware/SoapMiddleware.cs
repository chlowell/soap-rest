using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Threading.Tasks;

namespace SoapMiddleware
{
    public class SoapMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly string _path;
        private readonly MessageEncoder _encoder;
        private readonly ServiceDescription _service;
        private readonly ILogger<SoapMiddleware> _logger;

        public SoapMiddleware(RequestDelegate next, Type serviceType, string path, MessageEncoder encoder, ILogger<SoapMiddleware> logger)
        {
            _next = next;
            _path = path;
            _encoder = encoder;
            _service = new ServiceDescription(serviceType);
            _logger = logger;
        }

        public async Task Invoke(HttpContext context, IServiceProvider serviceProvider)
        {
            if (!context.Request.Path.Equals(_path, StringComparison.Ordinal))
            {
                await _next.Invoke(context);
                return;
            }

            _logger.LogInformation($"SOAP request for {context.Request.Path} ({context.Request.ContentLength ?? 0} bytes)");

            if (!context.Request.Headers.TryGetValue("SOAPAction", out var action))
            {
                return;
            }

            // read the message, extract operation and arguments
            var message = _encoder.ReadMessage(context.Request.Body, 0x10000, context.Request.ContentType);
            message.Headers.Action = action.ToString().Trim('\"');
            var operation = _service.Operations
                .FirstOrDefault(o => o.SoapAction.Equals(message.Headers.Action, StringComparison.Ordinal))
                ?? throw new InvalidOperationException($"No operation found for action {message.Headers.Action}");
            var args = GetRequestArguments(message, operation);

            // invoke the operation
            var serviceInstance = serviceProvider.GetService(_service.ServiceType);
            _logger.LogInformation("Invoking operation {operation}", operation.Name);
            var responseObject = operation.DispatchMethod.Invoke(serviceInstance, args);

            // write response message
            var resultName = operation.DispatchMethod.ReturnParameter
                .GetCustomAttribute<MessageParameterAttribute>()?.Name ?? $"{operation.Name}Result";
            var bodyWriter = new ServiceBodyWriter(operation.Contract.Namespace, $"{operation.Name}Response", resultName, responseObject);
            var responseMessage = Message.CreateMessage(_encoder.MessageVersion, operation.ReplyAction, bodyWriter);

            context.Response.ContentType = _encoder.ContentType; // context.Request.ContentType?
            context.Response.Headers["SOAPAction"] = responseMessage.Headers.Action;

            _encoder.WriteMessage(responseMessage, context.Response.Body);
        }

        /// <summary>
        /// This argument reading helper assumes the arguments are provided in order in the message body. This is true for
        /// messages coming from .NET WCF clients, but may not be true for all SOAP clients. If needed, this method could
        /// be replaced with a slightly more complex variant that allows for re-ordered arguments and fuzzier parameter
        /// name matching.
        /// </summary>
        /// <param name="requestMessage"></param>
        /// <param name="operation"></param>
        /// <returns></returns>
        private object[] GetRequestArguments(Message requestMessage, OperationDescription operation)
        {
            var parameters = operation.DispatchMethod.GetParameters();
            var arguments = new List<object>();

            // Deserialize request wrapper and object
            using (var xmlReader = requestMessage.GetReaderAtBodyContents())
            {
                // Find the element for the operation's data
                xmlReader.ReadStartElement(operation.Name, operation.Contract.Namespace);

                for (int i = 0; i < parameters.Length; i++)
                {
                    var parameterName = parameters[i].GetCustomAttribute<MessageParameterAttribute>()?.Name ?? parameters[i].Name;
                    xmlReader.MoveToStartElement(parameterName, operation.Contract.Namespace);
                    if (xmlReader.IsStartElement(parameterName, operation.Contract.Namespace))
                    {
                        var serializer = new DataContractSerializer(parameters[i].ParameterType, parameterName, operation.Contract.Namespace);
                        arguments.Add(serializer.ReadObject(xmlReader, verifyObjectName: true));
                    }
                }
            }

            return arguments.ToArray();
        }
    }
}
