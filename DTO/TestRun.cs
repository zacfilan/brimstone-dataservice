using BrimstoneRecorderTestResultPersistenceService.Models;
using System;
using System.Collections.Generic;

namespace BrimstoneRecorderTestResultPersistenceService
{
	/**
	 * brimstone-recorder post data structure
	 * */
	public class TestRun
	{
		public int? Id {get; set; } // database id of this row in this table
		public string? Name { get; set; } // usually the name of the testfile
		public string? Status { get; set; } // pass/fail
		public string? ErrorMessage { get; set; }
		public DateTime? StartDate { get; set; }
		public DateTime? EndDate { get; set; }
		public int? WallTime { get; set; }
		public int? UserTime { get; set; }
		public List<RunStep>? Steps { get; set; }
		public string? StartingServer { get; set; }
		/** The version of brimstone that was used */
		public string? BrimstoneVersion { get; set; }
		public string? ChromeVersion { get; set; }
		public string? BrimstoneComputerAlias { get; set; }
		public string? ApplicationVersion { get; set; }

		public string? Options { get; set; }
		public string? Description { get; set; }
    }
}

