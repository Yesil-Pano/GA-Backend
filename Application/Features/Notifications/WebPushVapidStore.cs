using Microsoft.Extensions.Configuration;
using WebPush;

namespace GA.Application.Features.Notifications
{
    internal static class WebPushVapidStore
    {
        private static VapidDetails? _details;

        public static VapidDetails GetOrCreate(IConfiguration configuration)
        {
            if (_details != null) return _details;

            var publicKey = configuration["WebPush:VapidPublicKey"];
            var privateKey = configuration["WebPush:VapidPrivateKey"];
            var subject = configuration["WebPush:Subject"] ?? "mailto:admin@theobuz.com";

            if (string.IsNullOrWhiteSpace(publicKey) || string.IsNullOrWhiteSpace(privateKey))
            {
                var generated = VapidHelper.GenerateVapidKeys();
                publicKey = generated.PublicKey;
                privateKey = generated.PrivateKey;
            }

            _details = new VapidDetails(subject, publicKey, privateKey);
            return _details;
        }

        public static string GetPublicKey(IConfiguration configuration) =>
            GetOrCreate(configuration).PublicKey;
    }
}
