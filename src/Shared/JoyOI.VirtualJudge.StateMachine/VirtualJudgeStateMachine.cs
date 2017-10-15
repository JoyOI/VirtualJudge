using System;
using System.Linq;
using System.Threading.Tasks;
using System.Net.Http;
using JoyOI.ManagementService.Core;
using Microsoft.EntityFrameworkCore.Migrations;

namespace JoyOI.VirtualJudge.StateMachine
{
    public class VirtualJudgeMetadata
    {
        public string Source { get; set; }
        public string ProblemId { get; set; }
        public string Language { get; set; }
        public string Code { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
    }

    public class VirtualJudgeStateMachine : StateMachineBase
    {
        public override async Task RunAsync()
        {
            Limitation.EnableNetwork = true;
            var metadata = await InitialBlobs.FindSingleBlob("metadata.json").ReadAsJsonAsync<VirtualJudgeMetadata>(this);

            switch (Stage)
            {
                case "Start":
                    goto case "SendRequest";
                case "SendRequest":
                    if (metadata.Source == "Bzoj")
                    {
                        await DeployAndRunActorAsync(new RunActorParam("BzojJudgeActor",
                            InitialBlobs.FindSingleBlob("metadata.json")));
                    }
                    else
                    {
                        throw new NotSupportedException(metadata.Source);
                    }
                    break;
                case "Finally":
                    await HttpInvokeAsync(HttpMethod.Post, "/management/judge/stagechange/" + this.Id, null);
                    break;
            }
        }

        public override Task HandleErrorAsync(Exception ex)
        {
            return base.HandleErrorAsync(ex);
        }
    }
}
