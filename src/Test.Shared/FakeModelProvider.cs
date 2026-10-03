namespace Test.Shared
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Minimal OpenAI-compatible model provider for tests. Answers every request with a fixed non-streaming chat
    /// completion (or a configured error status), and records the W3C traceparent header of each request so tests can
    /// verify outbound trace propagation.
    /// </summary>
    public sealed class FakeModelProvider : IDisposable
    {
        #region Public-Members

        /// <summary>
        /// Base URL of the fake provider (for example http://127.0.0.1:50123/v1).
        /// </summary>
        public string BaseUrl
        {
            get { return "http://127.0.0.1:" + _Port + "/v1"; }
        }

        /// <summary>
        /// Root URL of the fake provider, for health checks.
        /// </summary>
        public string RootUrl
        {
            get { return "http://127.0.0.1:" + _Port + "/"; }
        }

        /// <summary>
        /// HTTP status code returned for every request. Default 200.
        /// </summary>
        public int StatusCode { get; set; } = 200;

        /// <summary>
        /// Number of requests received.
        /// </summary>
        public int RequestCount
        {
            get { return Volatile.Read(ref _RequestCount); }
        }

        /// <summary>
        /// traceparent header values received, in order (null entries for requests without one).
        /// </summary>
        public List<string> TraceParents
        {
            get { return new List<string>(_TraceParents); }
        }

        #endregion

        #region Private-Members

        private readonly HttpListener _Listener = new HttpListener();
        private readonly CancellationTokenSource _Stop = new CancellationTokenSource();
        private readonly ConcurrentQueue<string> _TraceParents = new ConcurrentQueue<string>();
        private readonly int _Port;
        private readonly Task _Loop;
        private int _RequestCount = 0;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Start the fake provider on a free loopback port.
        /// </summary>
        public FakeModelProvider()
        {
            _Port = FreePort();
            _Listener.Prefixes.Add("http://127.0.0.1:" + _Port + "/");
            _Listener.Start();
            _Loop = Task.Run(LoopAsync);
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Stop the fake provider.
        /// </summary>
        public void Dispose()
        {
            _Stop.Cancel();
            try { _Listener.Stop(); } catch (Exception) { }
            try { _Listener.Close(); } catch (Exception) { }
            try { _Loop.Wait(TimeSpan.FromSeconds(2)); } catch (Exception) { }
            _Stop.Dispose();
        }

        #endregion

        #region Private-Methods

        private async Task LoopAsync()
        {
            while (!_Stop.IsCancellationRequested)
            {
                HttpListenerContext context;
                try
                {
                    context = await _Listener.GetContextAsync().ConfigureAwait(false);
                }
                catch (Exception)
                {
                    return;
                }

                _ = Task.Run(() => HandleAsync(context));
            }
        }

        private async Task HandleAsync(HttpListenerContext context)
        {
            try
            {
                Interlocked.Increment(ref _RequestCount);
                _TraceParents.Enqueue(context.Request.Headers["traceparent"]);

                if (context.Request.HasEntityBody)
                {
                    using (System.IO.StreamReader reader = new System.IO.StreamReader(context.Request.InputStream))
                    {
                        await reader.ReadToEndAsync().ConfigureAwait(false);
                    }
                }

                int status = StatusCode;
                string body = status == 200
                    ? "{\"id\":\"chatcmpl-test\",\"object\":\"chat.completion\",\"created\":1,\"model\":\"fake-model\",\"choices\":[{\"index\":0,\"message\":{\"role\":\"assistant\",\"content\":\"There are 3 users.\"},\"finish_reason\":\"stop\"}],\"usage\":{\"prompt_tokens\":11,\"completion_tokens\":5,\"total_tokens\":16}}"
                    : "{\"error\":{\"message\":\"simulated provider failure\",\"type\":\"server_error\"}}";

                byte[] bytes = Encoding.UTF8.GetBytes(body);
                context.Response.StatusCode = status;
                context.Response.ContentType = "application/json";
                context.Response.ContentLength64 = bytes.Length;
                await context.Response.OutputStream.WriteAsync(bytes, 0, bytes.Length).ConfigureAwait(false);
                context.Response.Close();
            }
            catch (Exception)
            {
                try { context.Response.Abort(); } catch (Exception) { }
            }
        }

        private static int FreePort()
        {
            TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        #endregion
    }
}
