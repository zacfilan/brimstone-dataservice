using BrimstoneRecorderTestResultPersistenceService;
using BrimstoneRecorderTestResultPersistenceService.Models;
using Microsoft.AspNetCore.Mvc;
using System.Data.SqlClient;
using System.Data;
using System.Text.RegularExpressions;

namespace BrimstonDataService.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class TestRunsController : ControllerBase
    {
        private readonly IConfiguration _config;
        private readonly ILogger<TestRunsController> _logger;

        public TestRunsController(IConfiguration config, ILogger<TestRunsController> logger)
        {
            _config = config; // get values from appsettings.json
            _logger = logger;
        }

        [HttpPost(Name = "PostTestRun")]
        public object Post([FromBody] TestRun test)
        {
            string connectionString = _config.GetValue<string>("ConnectionStrings:ReportHostDatabase");
            var columns = new List<string>() {
                "WorkflowName",
                "WorkflowStartDate",
                "WorkflowEndDate",
                "WorkflowWallTime",
                "WorkflowUserTime",
                "WorkflowStatus",
                "WorkflowErrorMessage",
                "BrimstoneBranch",
                "BrimstoneBrowser",
                "BrimstoneComputerName",

                "TouchWorksBuild",
                "TouchworksDatabaseServer",
                "TouchworksDatabaseVersion",
                // { "Structured Content: ",  }
                "TouchWorksWebserver",
                "TouchWorksUser",
                "TouchWorksWebserverVersion",
                "Description",
                "optionsUsedJson"
            };

            using (SqlConnection connection = new SqlConnection(
               connectionString))
            {
                connection.Open();
                string queryString;
                SqlCommand command;
                // POST api/testruns
                // body paramters will create a new testrun
                // will return the test ID for this
                if (test.Id != null)
                {
                    // update/overwrite it
                    queryString = $"update testrun set {String.Join(",", columns.Select(c => $"{c}=@{c}"))} where id=@Id";
                    command = new SqlCommand(queryString, connection);
                    setSqlParameters(command, test);
                    command.Parameters.Add("@Id", SqlDbType.Int).Value = test.Id;

                    command.ExecuteNonQuery();

                    command = new SqlCommand($"delete from runsteps where testrunid=${test.Id}", connection);
                    command.ExecuteNonQuery();
                }
                else
                {
                    // creating
                    queryString = $"insert testrun({String.Join(",", columns)}) output inserted.id values ({String.Join(",", columns.Select(c => $"@{c}"))})";
                    command = new SqlCommand(queryString, connection);
                    setSqlParameters(command, test);
                    test.Id = (int)command.ExecuteScalar();
                }

                // not doing screenshot presently.
                queryString = "insert runsteps(TestRunId, Step, Text, UserLatency, ClientMemory) values ";
                const int batchSize = 100; // this is the max size allowed;
                int count = 0;
                command = new SqlCommand("", connection);
                for (var i = 0; i < test.Steps.Count; ++i)
                {
                    RunStep step = test.Steps[i];
                    this.AddValues(ref command, ref test, ref step, count, ref queryString);
                    if (++count == batchSize)
                    {
                        command.CommandText = queryString;
                        command.ExecuteNonQuery(); // dump the full batch

                        // reset
                        queryString = "insert runsteps(TestRunId, Step, Text, UserLatency, ClientMemory) values ";
                        count = 0;
                        command = new SqlCommand("", connection);
                    }
                    else if (i != test.Steps.Count - 1)
                    {
                        queryString += ",";
                    }
                }
                if (count > 0)
                {
                    // dump the remainder/partial batch
                    command.CommandText = queryString;
                    command.ExecuteNonQuery();
                }

                connection.Close();
            }

            return Ok(new { Id = test.Id });
        }

        private void addNullableParameter(SqlCommand command, string parameter, string? value)
        {
            if (value == null)
            {
                command.Parameters.Add(parameter, SqlDbType.VarChar).Value = DBNull.Value;
            }
            else

                command.Parameters.Add(parameter, SqlDbType.VarChar).Value = value;
        }
    
        private void setSqlParameters(SqlCommand command, TestRun test)
        {
            command.Parameters.Add("@WorkflowName", SqlDbType.VarChar).Value = test.Name;
            command.Parameters.Add("@WorkflowStartDate", SqlDbType.DateTime2).Value = test.StartDate;
            command.Parameters.Add("@WorkflowEndDate", SqlDbType.DateTime2).Value = test.EndDate;
            command.Parameters.Add("@WorkflowWallTime", SqlDbType.Int).Value = test.WallTime;
            command.Parameters.Add("@WorkflowUserTime", SqlDbType.Int).Value = test.UserTime;
            command.Parameters.Add("@WorkflowStatus", SqlDbType.VarChar).Value = test.Status;
            this.addNullableParameter(command, "@WorkflowErrorMessage", test.ErrorMessage);
            this.addNullableParameter(command, "@optionsUsedJson", test.Options);
            this.addNullableParameter(command, "@Description", test.Description);

            command.Parameters.Add("@BrimstoneBranch", SqlDbType.VarChar).Value = test.BrimstoneVersion;
            command.Parameters.Add("@BrimstoneBrowser", SqlDbType.VarChar).Value = test.ChromeVersion;
            command.Parameters.Add("@BrimstoneComputerName", SqlDbType.VarChar).Value = test.BrimstoneComputerAlias;

            var d = new List<(Regex, string, bool)> {
                ( new Regex(@"^TouchWorks EHR\s+(.*)$"), "TouchWorksBuild", false),
                ( new Regex(@"^Database Server:\s+(.*)$"), "TouchworksDatabaseServer", false ),
                ( new Regex(@"^Database Version:\s+(.*)$"),  "TouchworksDatabaseVersion", false),
                // { "Structured Content: ",  }
                ( new Regex(@"^Web Server Name:\s+(.*)$"), "TouchWorksWebserver", false ),
                ( new Regex(@"^User Account Id:\s+(.*)$"), "TouchWorksUser", false ),
                ( new Regex(@"^Build Info:\s+(.*)$"), "TouchWorksWebserverVersion", false)
            };

            if (test.ApplicationVersion != null)
            {
                // this is application specific. For TouchWorks this will be a string that needs some parsing.
                // "\n        \n        \n            \n TouchWorks EHR 22.1.0.1\n Database Server: TWB10DEVSQL2.rd.allscripts.com\n Database Version: 22.1.0.1\n Structured Content: 3.2.0.38\n Web Server Name:  TWB10DEVWEB2\n User Account Id: twprovider \n Build Info: Rebar branch build no 1, changeset 2659381\n            \n            \n    \n        \n        \n    \n\n\n        \n"
                var lines = test.ApplicationVersion.Split("\n");
                foreach (var line in lines)
                {
                    var trimLine = line.Trim();
                    if (string.IsNullOrWhiteSpace(trimLine))
                    {
                        continue;
                    }
                    for (var i=0; i < d.Count; ++i)
                    {
                        var entry = d[i];
                        var match = entry.Item1.Match(trimLine);
                        if (match.Success)
                        {
                            command.Parameters.Add($"@{entry.Item2}", SqlDbType.VarChar).Value = match.Groups[1].Value;
                            entry.Item3 = true;
                            d[i] = entry;
                        }
                    }
                }
                foreach (var entry in d)
                {
                    if(entry.Item3 == false)
                    {
                        command.Parameters.Add($"@{entry.Item2}", SqlDbType.VarChar).Value = DBNull.Value;
                    }
                }
            }
        }

        private void AddValues(ref SqlCommand command, ref TestRun test, ref RunStep step, int count, ref string queryString)
        {
            queryString = queryString + $"(@ID{count}, @INDEX{count}, @NAME{count}, @LATENCY{count}, @MEMORY{count})";
            command.Parameters.Add($"@ID{count}", SqlDbType.Int).Value = test.Id;
            command.Parameters.Add($"@INDEX{count}", SqlDbType.Int).Value = step.Index + 1;
            command.Parameters.Add($"@NAME{count}", SqlDbType.VarChar).Value = step.Name;
            command.Parameters.Add($"@LATENCY{count}", SqlDbType.Int).Value = step.UserLatency;
            command.Parameters.Add($"@MEMORY{count}", SqlDbType.Int).Value = step.ClientMemory;
        }
    }
}