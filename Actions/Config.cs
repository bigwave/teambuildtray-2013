using Entities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Actions
{
	public class Config
	{
		public static void SaveHiddenBuilds(string fileLocation, TeamServer teamServer)
		{
			Executors.Config.SaveHiddenBuilds(fileLocation, teamServer.HiddenBuilds);
		}

		public static TeamServer LoadServerFromConfigurationFile(string fileLocation)
		{
			TeamServer serverToReturn = Executors.Config.LoadServerFromConfigurationFile(fileLocation);
			if (serverToReturn == null)
			{
				return null;
			}

			//Open xml file of builds to hide
			serverToReturn.HiddenBuilds = Actions.Config.LoadHiddenBuilds(fileLocation);
			return serverToReturn;
		}

		public static List<Uri> LoadHiddenBuilds(string fileLocation)
		{
			return Executors.Config.LoadHiddenBuilds(fileLocation);
		}
	}
}

