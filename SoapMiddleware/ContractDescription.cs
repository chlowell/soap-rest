using System;
using System.Collections.Generic;
using System.Reflection;
using System.ServiceModel;

namespace SoapMiddleware
{
    /// <summary>
    /// Collects a type's members decorated with OperationContractAttribute as
    /// <see cref="OperationDescription"/>.
    /// </summary>
    public class ContractDescription
    {
        public Type ContractType { get; private set; }
        public string Name { get; private set; }
        public string Namespace { get; private set; }
        public IEnumerable<OperationDescription> Operations { get; private set; }
        public ServiceDescription Service { get; private set; }

        public ContractDescription(ServiceDescription service, Type contractType, ServiceContractAttribute attribute)
        {
            Service = service;
            ContractType = contractType;
            Namespace = attribute.Namespace ?? "http://tempuri.org/"; // Namespace defaults to http://tempuri.org/
            Name = attribute.Name ?? ContractType.Name; // Name defaults to the type name

            var operations = new List<OperationDescription>();
            foreach (var operationMethodInfo in ContractType.GetTypeInfo().DeclaredMethods)
            {
                foreach (var operationContract in operationMethodInfo.GetCustomAttributes<OperationContractAttribute>())
                {
                    operations.Add(new OperationDescription(this, operationMethodInfo, operationContract));
                }
            }
            Operations = operations;
        }
    }
}
