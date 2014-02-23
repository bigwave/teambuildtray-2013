using Entities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Executors
{
    public class Config
    {
		public void SaveConfig()
		{

		}

		public static TeamServer LoadServerFromConfigurationFile(string fileLocation)
		{
			return Persistors.ConfigFile.LoadServerFromConfigurationFile(fileLocation);
		}

		public static List<Uri> LoadHiddenBuilds(string buildListConfigurationPath)
		{
			//Load hidden builds
			if (File.Exists(buildListConfigurationPath))
			{
				try
				{
					return Persistors.ConfigFile.GetHiddenBuilds(buildListConfigurationPath) ?? new List<Uri>();
				}
				catch (Exception)
				{
					return new List<Uri>();
				}
			}
			else
			{
				return new List<Uri>();
			}
		}



		public static void SaveHiddenBuilds(string configFileName, List<Uri> theBuilds)
		{
			Persistors.ConfigFile.SaveHiddenBuilds(configFileName, theBuilds);
		}
	}
}
