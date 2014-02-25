using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Entities
{
	// Summary:
	//     This enumeration represents the status of builds and build steps.
	[Flags]
	public enum TeamBuildStatus
	{
		// Summary:
		//     No status available.
		None = 0,
		//
		// Summary:
		//     Build is in progress.
		InProgress = 1,
		//
		// Summary:
		//     Build succeeded.
		Succeeded = 2,
		//
		// Summary:
		//     Build failed.
		Failed = 3
	}
}
