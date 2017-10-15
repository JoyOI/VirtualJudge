using System;
using System.IO;
using JoyOI.ManagementService.SDK;

namespace Deploy
{
    class Program
    {
        static void Main(string[] args)
        {
            var client = new ManagementServiceClient("https://mgmtsvc.1234.sh", @"C:\Users\Yuko\Documents\webapi-client.pfx", "123456");

            /* Judge Related */
            client.PatchActorAsync("BzojJudgeActor", File.ReadAllText(@"C:\Users\Yuko\Documents\GitHub\VirtualJudge\src\JoyOI.VirtualJudge.Bzoj\BzojJudgeActor.cs")).Wait();
            client.PatchStateMachineDefinitionAsync("VirtualJudgeStateMachine", File.ReadAllText(@"C:\Users\Yuko\Documents\GitHub\VirtualJudge\src\Shared\JoyOI.VirtualJudge.StateMachine\VirtualJudgeStateMachine.cs"), null).Wait();

            /* Problem Related */
            client.PatchActorAsync("BzojPullProblemSetActor", File.ReadAllText(@"C:\Users\Yuko\Documents\GitHub\VirtualJudge\src\JoyOI.VirtualJudge.Bzoj\BzojPullProblemSetActor.cs")).Wait();
            client.PatchActorAsync("BzojPullProblemBodyActor", File.ReadAllText(@"C:\Users\Yuko\Documents\GitHub\VirtualJudge\src\JoyOI.VirtualJudge.Bzoj\BzojPullProblemBodyActor.cs")).Wait();
            client.PatchStateMachineDefinitionAsync("BzojSyncProblemStateMachine", File.ReadAllText(@"C:\Users\Yuko\Documents\GitHub\VirtualJudge\src\JoyOI.VirtualJudge.Bzoj\BzojSyncProblemStateMachine.cs"), null).Wait();

            /* Trigger Pulling BZOJ */
            var stateMachineId = client.PutStateMachineInstanceAsync("BzojSyncProblemStateMachine", "http://api.oj.joyoi.net").Result;
            Console.WriteLine(stateMachineId);

            Console.Read();
        }
    }
}
