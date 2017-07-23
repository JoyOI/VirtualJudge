using System;
using System.Linq;
using System.Threading.Tasks;
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
            var metadata = await InitialBlobs.FindSingleBlob("metadata.json").ReadAsJsonAsync<VirtualJudgeMetadata>(this);

            switch (Stage)
            {
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
                    // TODO: feedback
                    break;
            }
        }

        public override Task HandleErrorAsync(Exception ex)
        {
            return base.HandleErrorAsync(ex);
        }
    }
}
