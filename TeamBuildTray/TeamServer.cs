namespace TeamBuildTray
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.ServiceModel;
    using System.Threading;
    using System.Xml.Serialization;

    using Microsoft.TeamFoundation.Build.Client;
    using Microsoft.TeamFoundation.Client;
    using Microsoft.TeamFoundation.Server;

    /// <summary>Class TeamServer.</summary>
    public class TeamServer
    {
        /// <summary>The interval time in seconds.</summary>
        public static readonly int IntervalTimeInSeconds = 30;
        private Timer queryTimer;
        private bool disposed;
        private string serverName;

        /// <summary>Gets the application configuration path.</summary>
        /// <value>The application configuration path.</value>
        public static string ApplicationConfigurationPath
        {
            get
            {
                string applicationDataPath =
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TeamBuildTray");
                if (!Directory.Exists(applicationDataPath))
                {
                    Directory.CreateDirectory(applicationDataPath);
                }

                return applicationDataPath;
            }
        }

        /// <summary>Gets the server configuration path.</summary>
        /// <value>The server configuration path.</value>
        public static string ServerConfigurationPath
        {
            get
            {
                return Path.Combine(ApplicationConfigurationPath, "servers.xml");
            }
        }

        /// <summary>Gets the build list configuration path.</summary>
        /// <value>The build list configuration path.</value>
        public static string BuildListConfigurationPath
        {
            get
            {
                return Path.Combine(ApplicationConfigurationPath, "hiddenbuilds.xml");
            }
        }

        /// <summary>Gets or sets the name of the server.</summary>
        /// <value>The name of the server.</value>
        public string ServerName
        {
            get
            {
                return this.serverName;
            }
            set
            {
                this.serverName = value;
            }
        }

        /// <summary>Gets or sets the port.</summary>
        /// <value>The port.</value>
        public int Port { get; set; }

        /// <summary>Gets or sets the name of the collection.</summary>
        /// <value>The name of the collection.</value>
        public string CollectionName { get; set; }

        /// <summary>Gets or sets the protocol.</summary>
        /// <value>The protocol.</value>
        public string Protocol { get; set; }

        /// <summary>Gets or sets the projects.</summary>
        /// <value>The projects.</value>
        public Collection<TeamProject> Projects { get; set; }


        /// <summary>Gets the definition by URI.</summary>
        /// <param name="uri">The URI passed in.</param>
        /// <returns>The build defintion.</returns>
        public IBuildDefinition GetDefinitionByUri(Uri uri)
        {
            foreach (TeamProject project in Projects)
            {
                if (project.BuildDefinitions.ContainsKey(uri))
                {
                    return project.BuildDefinitions[uri];
                }
            }

            return null;
        }

        private void QueryTimerElapsed(object sender)
        {
            if (Monitor.TryEnter(Projects))
            {
                Debug.WriteLine(DateTime.Now);
                try
                {
                    foreach (TeamProject project in Projects)
                    {
                        // Fire update event
                        if (OnProjectsUpdated != null)
                        {
                            var theBuilds = QueryBuilds(project.BuildDefinitions.Values);
                            OnProjectsUpdated(project, theBuilds);
                        }
                    }
                }
                finally
                {
                    Monitor.Exit(Projects);
                }
            }
        }

        /// <summary>Gets the server status.</summary>
        /// <param name="refreshServerList">if set to <see langword="true" /> [refresh server list].</param>
        /// <returns>IconColour.</returns>
        public IconColour GetServerStatus(bool refreshServerList)
        {
            var colour = IconColour.Grey;

            if (refreshServerList)
            {
                // Connect to the server and get a build list
                colour = GetBuildList();
            }

            queryTimer = new Timer(this.QueryTimerElapsed, null, new TimeSpan(0), new TimeSpan(0, 0, IntervalTimeInSeconds));

            return colour;
        }

        private IconColour GetBuildList()
        {

            bool projectsFound = false;
            var tfsCollectionUri = string.Format(CultureInfo.InvariantCulture, "{0}://{1}:{2}/tfs/{3}", Protocol, ServerName, Port, CollectionName);
            var tfsCollection = new Uri(tfsCollectionUri, UriKind.Absolute);


            using (TfsConnection tfsConnection = new TfsTeamProjectCollection(tfsCollection))
            {
                try
                {
                    var buildServer = tfsConnection.GetService<IBuildServer>();
                    foreach (TeamProject teamProject in Projects)
                    {
                        IBuildDefinitionSpec spec = buildServer.CreateBuildDefinitionSpec(teamProject.ProjectName);
                        IBuildDefinitionQueryResult result = buildServer.QueryBuildDefinitions(spec);

                        // Add the build agents
                        ////project.BuildAgents.Clear();
                        ////foreach (BuildAgent agent in result.Agents)
                        ////{
                        ////    project.BuildAgents.Add(agent);
                        ////}

                        // Add the build definitions
                        teamProject.BuildDefinitions.Clear();

                        foreach (IBuildDefinition definition in result.Definitions)
                        {
                            teamProject.BuildDefinitions.Add(definition.Uri, definition);
                        }

                        // Fire update event
                        if (OnProjectsUpdated != null)
                        {
                            OnProjectsUpdated(teamProject, QueryBuilds(teamProject.BuildDefinitions.Values));
                        }

                        if (teamProject.BuildDefinitions.Count > 0)
                        {
                            projectsFound = true;
                        }
                    }
                }
                catch
                {
                    return IconColour.Red;
                }

                if (projectsFound)
                {
                    return IconColour.Green;
                }
                return IconColour.Grey;
            }
        }

public event EventHandler<BuildQueryEventArgs> OnProjectsUpdated;

       /// <summary>Gets the build status.</summary>
        /// <param name="id">The identifier.</param>
        /// <returns>The build.</returns>
        public IBuildDetail GetBuildStatus(string id)
        {
            var tfsCollectionUri = string.Format(CultureInfo.InvariantCulture, "{0}://{1}:{2}/tfs/{3}", Protocol, ServerName, Port, CollectionName);
            var tfsCollection = new Uri(tfsCollectionUri, UriKind.Absolute);

            using (TfsConnection tfsConnection = new TfsTeamProjectCollection(tfsCollection))
            {
                var buildServer = tfsConnection.GetService<IBuildServer>();
                var css = tfsConnection.GetService<ICommonStructureService3>();
                var tfsProjects = css.ListAllProjects();

                foreach (ProjectInfo teamProject in tfsProjects)
                {
                    IBuildDetailSpec spec = buildServer.CreateBuildDetailSpec(teamProject.Name, id);
                    spec.InformationTypes = null;
                    var buildResult = buildServer.QueryBuilds(spec).Builds;

                    return buildResult[0];
                }
            }

            return null;
        }

        private BuildQueryEventArgs QueryBuilds(IEnumerable<IBuildDefinition> buildDefinitions)
        {
                Collection<IBuildQueryResult> buildResults = new Collection<IBuildQueryResult>();
                
            // Do queries
                var tfsCollectionUri = string.Format(CultureInfo.InvariantCulture, "{0}://{1}:{2}/tfs/{3}", Protocol, ServerName, Port, CollectionName);
                var tfsCollection = new Uri(tfsCollectionUri, UriKind.Absolute);

            using (TfsConnection tfsConnection = new TfsTeamProjectCollection(tfsCollection))
            {
                var buildServer = tfsConnection.GetService<IBuildServer>();

                IBuildDetailSpec spec = buildServer.CreateBuildDetailSpec(buildDefinitions.First().TeamProject);
                spec.InformationTypes = null;
                spec.MaxBuildsPerDefinition = 1;
                spec.QueryOrder = BuildQueryOrder.FinishTimeDescending;
                spec.QueryOptions = QueryOptions.None;
                DateTime startTime = DateTime.Now;
                IBuildQueryResult buildResult = buildServer.QueryBuilds(spec);
                Debug.WriteLine(CollectionName + " " + (DateTime.Now - startTime).TotalSeconds);
                buildResults.Add(buildResult);
                ////foreach (IBuildDefinition defintion in buildDefinitions)
                ////{
                ////    IBuildDetailSpec spec = buildServer.CreateBuildDetailSpec(defintion);
                ////    spec.InformationTypes = null;
                ////    spec.MaxBuildsPerDefinition = 1;
                ////    spec.QueryOrder = BuildQueryOrder.FinishTimeDescending;
                ////    spec.QueryOptions = QueryOptions.None;
                ////    DateTime startTime = DateTime.Now;
                ////    IBuildQueryResult buildResult = buildServer.QueryBuilds(spec);
                ////    Debug.WriteLine(defintion.Name + " " + (DateTime.Now - startTime).TotalSeconds);
                ////    buildResults.Add(buildResult);
                ////}
            }
            //////Generate agent specs
                ////foreach (TeamProject project in Projects)
                ////{
                ////    foreach (BuildAgent agent in project.BuildAgents)
                ////    {
                ////        buildQueueSpecs.Add(new BuildQueueSpec
                ////                                {
                ////                                    AgentSpec = new BuildAgentSpec
                ////                                                    {
                ////                                                        FullPath = agent.FullPath,
                ////                                                        MachineName = agent.MachineName,
                ////                                                        Port = agent.Port
                ////                                                    },
                ////                                    CompletedAge = 300,
                ////                                    DefinitionSpec = new BuildDefinitionSpec
                ////                                                         {
                ////                                                             FullPath =
                ////                                                                 "\\" + project.ProjectName + "\\*"
                ////                                                         },
                ////                                    Options = QueryOptions.All,
                ////                                    StatusFlags =
                ////                                        QueueStatus.Completed | QueueStatus.InProgress |
                ////                                        QueueStatus.Queued
                ////                                });
                ////    }

                    return new BuildQueryEventArgs
                               {
                                   BuildQueueQueryResults = new ReadOnlyCollection<IQueuedBuildQueryResult>(new IQueuedBuildQueryResult[0]),
                                   BuildQueryResults = new ReadOnlyCollection<IBuildQueryResult>(buildResults)
                               };


            }

        /// <summary>Queues the build.</summary>
        /// <param name="agentUri">The agent URI.</param>
        /// <param name="buildUri">The build URI.</param>
        public void QueueBuild(Uri agentUri, Uri buildUri)
        {
            ////BuildServiceSoapClient soapClient = new BuildServiceSoapClient(GetBinding(Protocol, "BuildServiceSoap"), GetBuildEndpointAddress());


            ////BuildRequest request = new BuildRequest
            ////{
            ////    BuildAgentUri = agentUri,
            ////    BuildDefinitionUri = buildUri
            ////};

            ////soapClient.QueueBuild(request, QueueOptions.None);
        }

        /// <summary>Gets the project list.</summary>
        /// <param name="protocol">The protocol.</param>
        /// <param name="serverName">Name of the server.</param>
        /// <param name="collectionName">Name of the collection.</param>
        /// <param name="port">The port.</param>
        /// <returns>The project info collection.</returns>
        public static ReadOnlyCollection<ProjectInfo> GetProjectList(string protocol, string serverName, string collectionName, int port)
        {
            var tfsCollectionUri = string.Format(CultureInfo.InvariantCulture, "{0}://{1}:{2}/tfs/{3}", protocol, serverName, port, collectionName);
            var tfsCollection = new Uri(tfsCollectionUri, UriKind.Absolute);

            using (TfsConnection tfsConnection = new TfsTeamProjectCollection(tfsCollection))
            {
                var css = tfsConnection.GetService<ICommonStructureService3>();
                var tfsProjects = css.ListAllProjects();

                return new ReadOnlyCollection<ProjectInfo>(tfsProjects);
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }


        private void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    queryTimer.Dispose();
                }

                disposed = true;
            }
        }

        /// <summary>
        /// Gets a list of configured team servers from the servers XML file.
        /// </summary>
        /// <returns></returns>
        public static List<TeamServer> GetTeamServerList()
        {
            XmlSerializer serializer = new XmlSerializer(typeof(List<TeamServer>));
            FileStream fs = File.OpenRead(ServerConfigurationPath);
            List<TeamServer> teamServers = serializer.Deserialize(fs) as List<TeamServer>;
            fs.Close();
            return teamServers;
        }


        /// <summary>Gets a list of the hidden builds from the hidden builds XML file.</summary>
        /// <returns>List of Uris.</returns>
        public static List<Uri> GetHiddenBuilds()
        {
            XmlSerializer serializer = new XmlSerializer(typeof(List<string>));
            FileStream fs = File.OpenRead(BuildListConfigurationPath);
            var hiddenFields = serializer.Deserialize(fs) as List<string>;
            fs.Close();

            if (hiddenFields == null)
            {
                return new List<Uri>();
                
            }

            List<Uri> theUris = hiddenFields.Select(build => new Uri(build)).ToList();

            return theUris;
        }
    }
}
