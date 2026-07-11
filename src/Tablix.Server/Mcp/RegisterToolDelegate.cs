namespace Tablix.Server.Mcp
{
    using System;
    using System.Threading.Tasks;

    /// <summary>
    /// Delegate used by Tablix MCP tool registration.
    /// </summary>
    /// <param name="name">Tool name.</param>
    /// <param name="description">Tool description.</param>
    /// <param name="inputSchema">Tool input schema.</param>
    /// <param name="handler">Tool handler.</param>
    public delegate void RegisterToolDelegate(
        string name,
        string description,
        object inputSchema,
        Func<object, Task<object>> handler);
}
