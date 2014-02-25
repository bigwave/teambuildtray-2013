using Entities;
using Microsoft.TeamFoundation.Client;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Xml.Serialization;
using System.Collections.ObjectModel;
using Microsoft.TeamFoundation.Build.Client;
using System.Diagnostics;
using Microsoft.TeamFoundation.Server;

namespace Actions
{
    public class TfsApi : Actions.ITfsApi
    {
		/// <summary>Gets the project list.</summary>
		/// <param name="protocol">The protocol.</param>
		/// <param name="serverName">Name of the server.</param>
		/// <param name="collectionName">Name of the collection.</param>
		/// <param name="port">The port.</param>
		/// <returns>The project info collection.</returns>
		public ReadOnlyCollection<TeamProject> GetProjectList(string protocol, string serverName, string collectionName, int port)
		{
			var tfsCollectionUri = string.Format(CultureInfo.InvariantCulture, "{0}://{1}:{2}/{3}", protocol, serverName, port, collectionName);
			var tfsCollection = new Uri(tfsCollectionUri, UriKind.Absolute);

			using (TfsConnection tfsConnection = new TfsTeamProjectCollection(tfsCollection))
			{
				var css = tfsConnection.GetService<ICommonStructureService3>();
				var tfsProjects = css.ListAllProjects();

				Collection<TeamProject> projectsToReturn = new Collection<TeamProject>();
				foreach (ProjectInfo projectInfo in tfsProjects)
				{
					TeamProject aNewProject = new TeamProject()
					{
						ProjectName = projectInfo.Name,
					};

					projectsToReturn.Add(aNewProject);
				}

				return new ReadOnlyCollection<TeamProject>(projectsToReturn);
			}
		}

		public void QueryBuilds(TeamServer teamServer)
		{
			Collection<TeamBuildStatus> buildResults = new Collection<TeamBuildStatus>();

			// Do queries
			var tfsCollectionUri = string.Format(
				CultureInfo.InvariantCulture,
				"{0}://{1}:{2}/{3}",
				teamServer.Protocol,
				teamServer.ServerName,
				teamServer.Port,
				teamServer.CollectionName);
			var tfsCollection = new Uri(tfsCollectionUri, UriKind.Absolute);

			using (TfsConnection tfsConnection = new TfsTeamProjectCollection(tfsCollection))
			{
				var buildServer = tfsConnection.GetService<IBuildServer>();

				foreach (TeamProject teamProject in teamServer.Projects)
				{
					IBuildDetailSpec spec = buildServer.CreateBuildDetailSpec(teamProject.ProjectName);
					spec.InformationTypes = null;
					spec.MaxBuildsPerDefinition = 1;
					spec.QueryOrder = BuildQueryOrder.FinishTimeDescending;
					spec.QueryOptions = QueryOptions.None;
					DateTime startTime = DateTime.Now;
					IBuildQueryResult buildResult = buildServer.QueryBuilds(spec);
					Debug.WriteLine(teamServer.CollectionName + " " + (DateTime.Now - startTime).TotalSeconds);

					foreach (IBuildDetail aBuild in buildResult.Builds)
					{
						if (!teamProject.Builds.Any(m => m.BuildDefinitionUri == aBuild.BuildDefinitionUri))
						{
							List<IBuildDefinition> buildDefinitions = new List<IBuildDefinition>(buildServer.QueryBuildDefinitionsByUri(new Uri[] { aBuild.BuildDefinitionUri }));
							TeamBuild theNewBuild = new TeamBuild()
							{
								BuildDefinitionUri = aBuild.BuildDefinitionUri,
								Name = buildDefinitions[0].Name,
							};

							teamProject.Builds.Add(theNewBuild);
						}

						TeamBuild theBuild = teamProject.Builds.First(m => m.BuildDefinitionUri == aBuild.BuildDefinitionUri);
						switch (aBuild.Status)
						{
							case BuildStatus.InProgress:
								theBuild.Status = TeamBuildStatus.InProgress;
								break;
							case BuildStatus.Succeeded:
								theBuild.Status = TeamBuildStatus.Succeeded;
								break;
							default:
								theBuild.Status = TeamBuildStatus.Failed;
								break;
						}

					}
				}
			}
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

    }
}
