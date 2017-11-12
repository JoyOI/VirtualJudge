using System;
using System.Linq;
using System.Threading.Tasks;
using System.Net.Http;
using JoyOI.ManagementService.Core;
using Microsoft.EntityFrameworkCore.Migrations;
using Newtonsoft.Json;

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
            Limitation.ExecutionTimeout = 180000;
            var metadata = await InitialBlobs.FindSingleBlob("metadata.json").ReadAsJsonAsync<VirtualJudgeMetadata>(this);

            switch (Stage)
            {
                case "Start":
                    await SetStageAsync("Start");
                    goto case "FetchingAccountAndSendRequest";
                case "FetchingAccountAndSendRequest":
                    await SetStageAsync("FetchingAccountAndSendRequest");
                    var result = await HttpInvokeAsync(HttpMethod.Post, "/management/virtualjudgeaccount/requestaccount", new { id = this.Id });
                    var response = JsonConvert.DeserializeObject<dynamic>(result);
                    if (response.code != 200)
                    {
                        throw new Exception($"The online judge api responsed error { response.code } \r\n{ response.msg }");
                    }
                    var accountFileId = await UploadJsonFileAsync("account.json", new { username = response.data.username, password = response.data.password });
                    if (metadata.Source == "Bzoj")
                    {
                        await DeployAndRunActorAsync(new RunActorParam("BzojJudgeActor",
                            InitialBlobs.FindSingleBlob("metadata.json"), 
                            new BlobInfo(accountFileId, "account.json")));
                    }
                    else if (metadata.Source == "LeetCode")
                    {
                        await DeployAndRunActorAsync(new RunActorParam("LeetCodeJudgeActor",
                            InitialBlobs.FindSingleBlob("metadata.json"),
                            new BlobInfo(accountFileId, "account.json")));
                    }
                    else
                    {
                        throw new NotSupportedException(metadata.Source);
                    }
                    goto case "Finally";
                case "Finally":
                    await SetStageAsync("Finally");
                    await HttpInvokeAsync(HttpMethod.Post, "/management/judge/stagechange/" + this.Id, null);
                    break;
            }
        }

        public override Task HandleErrorAsync(Exception ex)
        {
            HttpInvokeAsync(HttpMethod.Post, "/management/judge/stagechange/" + this.Id + "?se=true", null);
            return base.HandleErrorAsync(ex);
        }
    }
}
