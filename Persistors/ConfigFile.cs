using Entities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Persistors
{
    public class ConfigFile
    {
		public static TeamServer LoadServerFromConfigurationFile(string fileLocation)
		{
			TeamServer servers = new TeamServer();
			if (File.Exists(fileLocation))
			{
					XmlSerializer serializer = new XmlSerializer(typeof(TeamServer));
					FileStream fs = File.OpenRead(fileLocation);
					TeamServer teamServers = serializer.Deserialize(fs) as TeamServer;
					fs.Close();
					return teamServers;
			}
			return servers;
		}

		/// <summary>Gets a list of the hidden builds from the hidden builds XML file.</summary>
		/// <returns>List of Uris.</returns>
		public static List<Uri> GetHiddenBuilds(string buildListConfigurationPath)
		{
			XmlSerializer serializer = new XmlSerializer(typeof(List<string>));
			FileStream fs = File.OpenRead(buildListConfigurationPath);
			var hiddenFields = serializer.Deserialize(fs) as List<string>;
			fs.Close();

			if (hiddenFields == null)
			{
				return new List<Uri>();

			}

			List<Uri> theUris = hiddenFields.Select(build => new Uri(build)).ToList();

			return theUris;
		}


		public static void SaveHiddenBuilds(string configFileName, List<Uri> theBuilds)
		{
			List<string> stringUris = theBuilds.Select(build => build.ToString()).ToList<string>();

			var serializer = new XmlSerializer(typeof(List<string>));
			FileStream fs = File.Open(
										configFileName,
										FileMode.Create,
										FileAccess.Write,
										FileShare.ReadWrite);
			serializer.Serialize(fs, stringUris);
			fs.Close();
		}
	}
}
