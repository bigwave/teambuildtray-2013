using Microsoft.TeamFoundation.Build.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Entities
{
	public class TeamBuild
	{
		[XmlIgnore()]
		public Uri BuildDefinitionUri { get; set; }

		/// <summary>
		///  From http://stackoverflow.com/questions/1594042/system-uri-implements-iserializable-but-gives-error
		/// </summary>
		[XmlElement("URI")]
		public string _URI // Unfortunately this has to be public to be xml serialized.
		{
			get { return BuildDefinitionUri.ToString(); }
			set { BuildDefinitionUri = new Uri(value); }
		}
	
		public string Name { get; set; }
		public string RequestedBy { get; set; }

		public BuildStatus Status { get; set; }
	}
}
