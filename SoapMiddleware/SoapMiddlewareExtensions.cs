using System.ServiceModel.Channels;

namespace Microsoft.AspNetCore.Builder
{
    public static class SoapMiddlewareExtensions
    {
        public static IApplicationBuilder UseSoapMiddleware<T>(this IApplicationBuilder builder, string path, MessageEncoder encoder) =>
            builder.UseMiddleware<SoapMiddleware.SoapMiddleware>(typeof(T), path, encoder);

        public static IApplicationBuilder UseSoapMiddleware<T>(this IApplicationBuilder builder, string path, Binding binding)
        {
            var encoder = binding.CreateBindingElements().Find<MessageEncodingBindingElement>()?.CreateMessageEncoderFactory().Encoder;

            return builder.UseMiddleware<SoapMiddleware.SoapMiddleware>(typeof(T), path, encoder);
        }
    }
}
