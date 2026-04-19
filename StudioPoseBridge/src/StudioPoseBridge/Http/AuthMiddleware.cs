using System.Net;

namespace StudioPoseBridge.Http
{
    internal static class AuthMiddleware
    {
        public static bool IsPublicPath(string path)
        {
            return path == "/v1/health";
        }

        public static bool Validate(HttpListenerRequest request, string expectedToken)
        {
            var header = request.Headers["X-Pose-Token"];
            return header != null && header == expectedToken;
        }
    }
}
