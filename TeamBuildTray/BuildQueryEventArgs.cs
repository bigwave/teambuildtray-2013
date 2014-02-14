namespace TeamBuildTray
{
    using System;
    using System.Collections.ObjectModel;

    using Microsoft.TeamFoundation.Build.Client;

    /// <summary>Class BuildQueryEventArgs. This class cannot be inherited.</summary>
    [Serializable]
    public sealed class BuildQueryEventArgs : EventArgs
    {
        /// <summary>Gets or sets the build query results.</summary>
        /// <value>The build query results.</value>
        public ReadOnlyCollection<IBuildQueryResult> BuildQueryResults { get; set; }
        public ReadOnlyCollection<IQueuedBuildQueryResult> BuildQueueQueryResults { get; set; }
    }
}