using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using JoyOI.ManagementService.Core;
using Microsoft.EntityFrameworkCore.Migrations;

namespace JoyOI.VirtualJudge.Bzoj.StateMachine
{
    public class BzojSyncProblemStateMachine : StateMachineBase
    {
        public override async Task RunAsync()
        {
            switch (Stage)
            {
                case "Start":
                    await SetStageAsync("Start");
                    await DeployAndRunActorAsync(new RunActorParam("BzojPullProblemSetActor"));
                    goto case "FetchingProblemBody";
                case "FetchingProblemBody":
                    var pullProblemSetActor = StartedActors.FindSingleActor("Start", "BzojPullProblemSetActor");
                    var problems = await pullProblemSetActor.FindSingleOutputBlob("problemset.json").ReadAsJsonAsync<IEnumerable<string>>(this);
                    var parameters = new List<RunActorParam>();
                    foreach (var x in problems)
                    {
                        parameters.Add(new RunActorParam("BzojPullProblemBodyActor", new BlobInfo()));
                    }
                    await DeployAndRunActorsAsync();
            }
        }
    }
}
