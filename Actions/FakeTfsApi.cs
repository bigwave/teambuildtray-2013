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
	public class FakeTfsApi : Actions.ITfsApi
	{
		private bool goingUp = true;

		/// <summary>Gets the project list.</summary>
		/// <param name="protocol">The protocol.</param>
		/// <param name="serverName">Name of the server.</param>
		/// <param name="collectionName">Name of the collection.</param>
		/// <param name="port">The port.</param>
		/// <returns>The project info collection.</returns>
		public ReadOnlyCollection<TeamProject> GetProjectList(string protocol, string serverName, string collectionName, int port)
		{
			Collection<TeamProject> projectsToReturn = new Collection<TeamProject>();

			for (int i = 0; i < 15; i++)
			{
				var aNewProject = new TeamProject()
				{
					ProjectName = "Fake Project " + (Char)(65 + i),
				};

				projectsToReturn.Add(aNewProject);

			}

			return new ReadOnlyCollection<TeamProject>(projectsToReturn);
		}

		public void QueryBuilds(TeamServer teamServer)
		{
			Collection<TeamBuildStatus> buildResults = new Collection<TeamBuildStatus>();

			foreach (TeamProject teamProject in teamServer.Projects)
			{
				if (teamProject.Builds == null || teamProject.Builds.Count == 0)
				{
					teamProject.Builds = new List<TeamBuild>();

					for (int i = 0; i < 4; i++)
					{
						TeamBuild theNewBuild = new TeamBuild()
						{
							BuildDefinitionUri = new Uri("http://tfs/build/" + teamProject.ProjectName + "/" + i),
							ProjectName = teamProject.ProjectName,
							BuildName =  " build " + i,
							Status = TeamBuildStatus.Succeeded
						};
						teamProject.Builds.Add(theNewBuild);
					}
				}
				else
				{
					if (goingUp)
					{
						if (!ChangeOneBuildsStatus(teamProject, TeamBuildStatus.Succeeded, TeamBuildStatus.InProgress))
						{
							if (!ChangeOneBuildsStatus(teamProject, TeamBuildStatus.InProgress, TeamBuildStatus.Failed))
							{
								goingUp = !goingUp;
							}
						}
					}
					if (!goingUp)
					{
						if (!ChangeOneBuildsStatus(teamProject, TeamBuildStatus.Failed, TeamBuildStatus.InProgress))
						{
							if (!ChangeOneBuildsStatus(teamProject, TeamBuildStatus.InProgress, TeamBuildStatus.Succeeded))
							{
								goingUp = !goingUp;
							}
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

		private bool ChangeOneBuildsStatus(TeamProject projects, TeamBuildStatus from, TeamBuildStatus to)
		{
			Debug.WriteLine(projects.ProjectName + " : " + from + " to " + to);
			bool changedOne = false;
			foreach (TeamBuild aBuild in projects.Builds)
			{
				if (aBuild.Status != from)
				{
					continue;
				}

				Debug.WriteLine(aBuild.Name + " from " + ((TeamBuildStatus)from).ToString() + " to " + ((TeamBuildStatus)to).ToString());
				changedOne = true;
				aBuild.Status = to;
				break;
			}

			return changedOne;
		}
	}
}
