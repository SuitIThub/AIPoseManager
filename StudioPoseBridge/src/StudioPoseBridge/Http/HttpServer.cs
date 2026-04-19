using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using StudioPoseBridge.Config;
using StudioPoseBridge.Threading;

namespace StudioPoseBridge.Http
{
    public sealed class HttpServer : IDisposable
    {
        private readonly int _port;
        private readonly string _token;
        private readonly PluginConfig _config;
        private HttpListener _listener;
        private Thread _thread;
        private volatile bool _running;

        public HttpServer(int port, string token, PluginConfig config)
        {
            _port = port;
            _token = token;
            _config = config;
        }

        public void Start()
        {
            if (_running) return;
            _listener = new HttpListener();
            _listener.Prefixes.Add("http://127.0.0.1:" + _port + "/");
            _listener.Start();
            _running = true;
            _thread = new Thread(AcceptLoop)
            {
                IsBackground = true,
                Name = "StudioPoseBridge.Http"
            };
            _thread.Start();
        }

        private void AcceptLoop()
        {
            while (_running && _listener != null && _listener.IsListening)
            {
                HttpListenerContext ctx = null;
                try
                {
                    ctx = _listener.GetContext();
                }
                catch (HttpListenerException)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    StudioPoseBridgePlugin.Log.LogError(ex);
                    continue;
                }
                if (ctx == null) continue;
                Task.Run(() => ProcessRequest(ctx));
            }
        }

        private async void ProcessRequest(HttpListenerContext ctx)
        {
            try
            {
                var req = ctx.Request;
                var path = req.Url.AbsolutePath;
                if (_config.IsDebug)
                    StudioPoseBridgePlugin.Log.LogDebug(req.HttpMethod + " " + path);

                if (!AuthMiddleware.IsPublicPath(path) && !AuthMiddleware.Validate(req, _token))
                {
                    await WriteJson(ctx, ApiResponse.Fail("unauthorized", "E_UNAUTHORIZED", 401), 401).ConfigureAwait(false);
                    return;
                }

                var response = await Router.Dispatch(req, _config).ConfigureAwait(false);
                var status = response.HttpStatus > 0 ? response.HttpStatus : (response.ok ? 200 : 400);
                await WriteJson(ctx, response, status).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                StudioPoseBridgePlugin.Log.LogError("HTTP handler: " + ex);
                try
                {
                    await WriteJson(ctx, ApiResponse.Fail("internal error", "E_INTERNAL", 500), 500).ConfigureAwait(false);
                }
                catch
                {
                    // ignored
                }
            }
            finally
            {
                try { ctx.Response.OutputStream.Close(); } catch { }
            }
        }

        private static Task WriteJson(HttpListenerContext ctx, ApiResponse response, int status)
        {
            var bytes = JsonHelper.SerializeToUtf8(response);
            ctx.Response.StatusCode = status;
            ctx.Response.ContentType = "application/json; charset=utf-8";
            ctx.Response.ContentEncoding = Encoding.UTF8;
            return ctx.Response.OutputStream.WriteAsync(bytes, 0, bytes.Length);
        }

        public void Stop()
        {
            _running = false;
            try
            {
                _listener?.Stop();
            }
            catch
            {
                // ignored
            }
            try
            {
                _listener?.Close();
            }
            catch
            {
                // ignored
            }
            _listener = null;
            if (_thread != null && _thread.IsAlive)
            {
                if (!_thread.Join(1000))
                {
                    try { _thread.Interrupt(); } catch { }
                }
            }
            _thread = null;
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
