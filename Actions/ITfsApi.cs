using System;
namespace Actions
{
	public interface ITfsApi
	{
		System.Collections.ObjectModel.ReadOnlyCollection<Entities.TeamProject> GetProjectList(string protocol, string serverName, string collectionName, int port);
		void QueryBuilds(Entities.TeamServer teamServer);
		void QueueBuild(Uri agentUri, Uri buildUri);
	}
}
