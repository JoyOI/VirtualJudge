using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using JoyOI.ManagementService.Core;
using Microsoft.EntityFrameworkCore.Migrations;
using System.Net.Http;

namespace JoyOI.VirtualJudge.LeetCode.StateMachine
{
    class LeetCodeSyncProblemStateMachine : StateMachineBase
    {
        public override async Task RunAsync()
        {
            Limitation.EnableNetwork = true;
            Limitation.ExecutionTimeout = 1000 * 60 * 60 * 2;

            switch (Stage)
            {
                case "Start":
                    await SetStageAsync("Start");
                    await DeployAndRunActorAsync(new RunActorParam("LeetCodePullProblemSetActor"));
                    goto case "FetchingProblemBody";
                case "FetchingProblemBody":
                    await SetStageAsync("FetchingProblemBody");
                    var pullProblemSetActor = StartedActors.FindSingleActor("Start", "LeetCodePullProblemSetActor");
                    var problems = await pullProblemSetActor.FindSingleOutputBlob("problemset.json").ReadAsJsonAsync<IEnumerable<string>>(this);
                    var parameters = new List<RunActorParam>();
                    foreach (var x in problems)
                    {
                        parameters.Add(new RunActorParam(
                            "LeetCodePullProblemBodyActor",
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
                    var task = HttpInvokeAsync(HttpMethod.Post, "/management/problem/leetcode/" + this.Id, null);
                    break;
            }
        }
    }
}
