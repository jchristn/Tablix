namespace Test.Shared
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Handshake-era MCP session over Streamable HTTP for live-server tests.
    /// Voltaic 2.1.4 and later answer every request other than initialize without an MCP-Session-Id with HTTP 400,
    /// so tests open a session with initialize and send its ID on every later request.
    /// </summary>
    public sealed class McpTestSession
    {
        #region Public-Members

        /// <summary>
        /// Protocol revision negotiated by the session.
        /// </summary>
        public const string ProtocolVersion = "2025-11-25";

        /// <summary>
        /// MCP endpoint URL.
        /// </summary>
        public string Url { get; }

        /// <summary>
        /// Session ID issued by the server.
        /// </summary>
        public string SessionId { get; }

        #endregion

        #region Constructors-and-Factories

        private McpTestSession(string url, string sessionId)
        {
            Url = url;
            SessionId = sessionId;
        }

        /// <summary>
        /// Wait for the MCP endpoint to accept an initialize request, then complete the handshake.
        /// </summary>
        public static async Task<McpTestSession> OpenAsync(string url, CancellationToken token)
        {
            using HttpClient client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
            string initialize = "{\"jsonrpc\":\"2.0\",\"id\":0,\"method\":\"initialize\",\"params\":{\"protocolVersion\":\"" + ProtocolVersion + "\",\"capabilities\":{},\"clientInfo\":{\"name\":\"tablix-tests\",\"version\":\"1.0.0\"}}}";
            Exception lastException = null;

            for (int attempt = 0; attempt < 20; attempt++)
            {
                token.ThrowIfCancellationRequested();

                try
                {
                    using HttpRequestMessage request = CreateRequest(url, null, initialize);
                    using HttpResponseMessage response = await client.SendAsync(request, token).ConfigureAwait(false);
                    string content = await response.Content.ReadAsStringAsync(token).ConfigureAwait(false);
                    if (response.IsSuccessStatusCode && response.Headers.TryGetValues("MCP-Session-Id", out var values))
                    {
                        McpTestSession session = new McpTestSession(url, values.First());
                        await session.PostAsync("{\"jsonrpc\":\"2.0\",\"method\":\"notifications/initialized\"}", token).ConfigureAwait(false);
                        return session;
                    }

                    lastException = new InvalidOperationException("HTTP " + (int)response.StatusCode + ": " + content);
                }
                catch (Exception ex) when (!token.IsCancellationRequested && (ex is HttpRequestException || ex is TaskCanceledException || ex is IOException))
                {
                    lastException = ex;
                }

                await Task.Delay(100, token).ConfigureAwait(false);
            }

            throw new InvalidOperationException("Timed out waiting for " + url, lastException);
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// POST one message on the session and return the body regardless of HTTP status.
        /// </summary>
        public async Task<string> PostAsync(string body, CancellationToken token, string traceParent = null)
        {
            using HttpClient client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            using HttpRequestMessage request = CreateRequest(Url, SessionId, body);
            if (traceParent != null) request.Headers.TryAddWithoutValidation("traceparent", traceParent);

            using HttpResponseMessage response = await client.SendAsync(request, token).ConfigureAwait(false);
            return await response.Content.ReadAsStringAsync(token).ConfigureAwait(false);
        }

        #endregion

        #region Private-Methods

        private static HttpRequestMessage CreateRequest(string url, string sessionId, string body)
        {
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");
            request.Headers.TryAddWithoutValidation("Accept", "application/json, text/event-stream");
            request.Headers.TryAddWithoutValidation("MCP-Protocol-Version", ProtocolVersion);
            if (sessionId != null) request.Headers.TryAddWithoutValidation("MCP-Session-Id", sessionId);
            return request;
        }

        #endregion
    }
}
