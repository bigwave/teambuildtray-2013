using Microsoft.TeamFoundation.Build.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Entities
{
	public class TeamBuild
	{
		public Uri BuildDefinitionUri { get; set; }
		public string Name { get; set; }
		public string RequestedBy { get; set; }

		public BuildStatus Status { get; set; }
	}
}
