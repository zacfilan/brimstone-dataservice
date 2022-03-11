namespace BrimstoneRecorderTestResultPersistenceService.Models
{

	public class RunStep
	{
		//public int? Id { get; set; } // the database id of this step in this table
		//public int? TestRunId { get; set; }  // the id of the testrun this step is in
		public int? Index { get; set; }  // the 0-based of this step in the testrun
		public string? Name { get; set; }  // some user defined name for this step
		public int? BaseIndex { get; set; } // where this set of steps starts
		public int? UserLatency { get; set; } // the latency in milliseconds
		public int? ClientMemory { get; set; }  // the memory in MBs used before this step is executed
		public string? Path { get; set; } // full path to the zipfile
	}
}
