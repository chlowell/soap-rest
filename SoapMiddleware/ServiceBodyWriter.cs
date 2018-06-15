using System.Runtime.Serialization;
using System.ServiceModel.Channels;
using System.Xml;

namespace SoapMiddleware
{
    public class ServiceBodyWriter : BodyWriter
    {
        public string ServiceNamespace { get; }
        public string EnvelopeName { get; }
        public string ResultName { get; }
        public object Result { get; }

        public ServiceBodyWriter(string serviceNamespace, string envelopeName, string resultName, object result)
            : base(isBuffered: true)
        {
            ServiceNamespace = serviceNamespace;
            EnvelopeName = envelopeName;
            ResultName = resultName;
            Result = result;
        }

        protected override void OnWriteBodyContents(XmlDictionaryWriter writer)
        {
            writer.WriteStartElement(EnvelopeName, ServiceNamespace);
            var serializer = new DataContractSerializer(Result.GetType(), ResultName, ServiceNamespace);
            serializer.WriteObject(writer, Result);
            writer.WriteEndElement();
        }
    }
}
