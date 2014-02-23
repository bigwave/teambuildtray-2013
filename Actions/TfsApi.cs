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
    public class TfsApi
    {
		/// <summary>Gets the project list.</summary>
		/// <param name="protocol">The protocol.</param>
		/// <param name="serverName">Name of the server.</param>
		/// <param name="collectionName">Name of the collection.</param>
		/// <param name="port">The port.</param>
		/// <returns>The project info collection.</returns>
		public static ReadOnlyCollection<ProjectInfo> GetProjectList(string protocol, string serverName, string collectionName, int port)
		{
			var tfsCollectionUri = string.Format(CultureInfo.InvariantCulture, "{0}://{1}:{2}/{3}", protocol, serverName, port, collectionName);
			var tfsCollection = new Uri(tfsCollectionUri, UriKind.Absolute);

			using (TfsConnection tfsConnection = new TfsTeamProjectCollection(tfsCollection))
			{
				var css = tfsConnection.GetService<ICommonStructureService3>();
				var tfsProjects = css.ListAllProjects();

				return new ReadOnlyCollection<ProjectInfo>(tfsProjects);
			}
		}

		/// <summary>Gets the server status.</summary>
		/// <param name="refreshServerList">if set to <see langword="true" /> [refresh server list].</param>
		/// <returns>IconColour.</returns>
		public IconColour GetServerStatus(bool refreshServerList, TeamServer teamServer)
		{
			var colour = IconColour.Grey;

			if (refreshServerList)
			{
				// Connect to the server and get a build list
				colour = GetBuildList(teamServer);
			}


			return colour;
		}

		public static void QueryBuilds(TeamServer teamServer)
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

					foreach (IBuildDetail aBuild  in buildResult.Builds)
					{
						teamProject.Builds.First(m=>m.BuildDefinitionUri == aBuild.BuildDefinitionUri).Status = aBuild.Status;
					}
				}
			}
		}

		private IconColour GetBuildList(TeamServer teamServer)
		{
			try
			{
				QueryBuilds(teamServer);
			}
			catch
			{
				return IconColour.Red;
			}

			bool building = false;

			if (teamServer.Projects.Count == 0)
			{
				return IconColour.Grey;
			}

			foreach (TeamProject teamProject in teamServer.Projects)
			{
				if (teamProject.Builds.Count == 0)
				{
					return IconColour.Grey;
				}

				foreach (IBuildDetail aBuild in teamProject.Builds)
				{
					if (aBuild.Status == BuildStatus.Failed || aBuild.Status == BuildStatus.PartiallySucceeded)
					{
						return IconColour.Red;
					}

					if (aBuild.Status == BuildStatus.InProgress)
					{
						building = true;
					}
				}
			}

			if (building)
			{
				return IconColour.Amber;
			}

			return IconColour.Green;
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
