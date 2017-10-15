using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using JoyOI.ManagementService.Core;
using Microsoft.EntityFrameworkCore.Migrations;

namespace JoyOI.VirtualJudge.Bzoj.StateMachine
{
    public class BzojSyncProblemStateMachine : StateMachineBase
    {
        public override async Task RunAsync()
        {
            Limitation.EnableNetwork = true;
            Limitation.ExecutionTimeout = 1000 * 60 * 60 * 2;

            switch (Stage)
            {
                case "Start":
                    await SetStageAsync("Start");
                    await DeployAndRunActorAsync(new RunActorParam("BzojPullProblemSetActor"));
                    goto case "FetchingProblemBody";
                case "FetchingProblemBody":
                    await SetStageAsync("FetchingProblemBody");
                    var pullProblemSetActor = StartedActors.FindSingleActor("Start", "BzojPullProblemSetActor");
                    var problems = await pullProblemSetActor.FindSingleOutputBlob("problemset.json").ReadAsJsonAsync<IEnumerable<string>>(this);
                    var parameters = new List<RunActorParam>();
                    foreach (var x in problems)
                    {
                        parameters.Add(new RunActorParam(
                            "BzojPullProblemBodyActor",
                            new BlobInfo[] {
                                new BlobInfo(
                                await UploadTextFileAsync("id.txt", x.ToString()),
                                "id.txt"
                                )
                            }, 
                            x.ToString()));
                    }
                    await DeployAndRunActorsAsync(parameters.ToArray());
                    goto case "CollectingResult";
                case "CollectingResult":
                    await SetStageAsync("CollectingResult");
                    await HttpInvokeAsync(HttpMethod.Post, "/management/problem/bzoj/" + this.Id, null);
                    break;
            }
        }
    }
}
