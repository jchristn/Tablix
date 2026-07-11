namespace Test.Shared
{
    /// <summary>
    /// Dashboard-to-server API route contract.
    /// </summary>
    internal class ApiRouteContract
    {
        /// <summary>
        /// HTTP method.
        /// </summary>
        public string Method { get; }

        /// <summary>
        /// Route template.
        /// </summary>
        public string RouteTemplate { get; }

        /// <summary>
        /// Server registration fragment.
        /// </summary>
        public string ServerRegistrationFragment { get; }

        /// <summary>
        /// Required dashboard fragment.
        /// </summary>
        public string RequiredDashboardFragment { get; }

        /// <summary>
        /// Dashboard route prefix.
        /// </summary>
        public string DashboardRoutePrefix { get; }

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="method">HTTP method.</param>
        /// <param name="routeTemplate">Route template.</param>
        /// <param name="serverRegistrationFragment">Server registration fragment.</param>
        /// <param name="requiredDashboardFragment">Required dashboard fragment.</param>
        /// <param name="dashboardRoutePrefix">Dashboard route prefix.</param>
        public ApiRouteContract(
            string method,
            string routeTemplate,
            string serverRegistrationFragment,
            string requiredDashboardFragment,
            string dashboardRoutePrefix)
        {
            Method = method;
            RouteTemplate = routeTemplate;
            ServerRegistrationFragment = serverRegistrationFragment;
            RequiredDashboardFragment = requiredDashboardFragment;
            DashboardRoutePrefix = dashboardRoutePrefix;
        }
    }
}
