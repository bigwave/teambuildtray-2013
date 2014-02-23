using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Entities
{
		/// <summary>Class TeamProject.</summary>
		public class TeamProject
		{
			/// <summary>Gets or sets the name of the project.</summary>
			/// <value>The name of the project.</value>
			public string ProjectName { get; set; }

			public List<TeamBuild> Builds { get; set; }
			public List<Uri> HiddenBuilds { get; set; }

			public string HiddenFileConfigFileName { get; set; }
		}

	}
