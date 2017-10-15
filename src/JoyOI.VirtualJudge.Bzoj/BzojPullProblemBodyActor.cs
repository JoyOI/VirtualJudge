using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Net.Http;
using System.IO;
using Newtonsoft.Json;

namespace JoyOI.VirtualJudge.Bzoj.Actor
{
    public class BzojPullProblemBodyActor
    {
        private const string baseUrl = "http://www.lydsy.com";
        private const string problemEndpoint = "/JudgeOnline/problem.php?id=";
        private Dictionary<Guid, string> imageDictionary = new Dictionary<Guid, string>();
        private List<string> returnFiles = new List<string>(1) { "problemset.json" };
        private HttpClient client = new HttpClient() { BaseAddress = new Uri(baseUrl) };
        private Regex timeLimitRegex = new Regex("(?<=<span class=green>Time Limit: </span>)([0-9]{1,})(?= Sec)"); // Unit: sec
        private Regex memoryLimitRegex = new Regex("(?<=<span class=green>Memory Limit: </span>)([0-9]{1,})(?= MB<br>)"); // Unit: MB
        private Regex bodyRegex = new Regex("(?<=Discuss</a>]</center>)([\\s\\S]*)(?=<div class=content><p><a href='problemset)"); // HTML

        public void Main()
        {
            MainAsync(Convert.ToInt32(File.ReadAllText("id.txt"))).Wait();
        }

        private async Task MainAsync(int problemId)
        {
            var retryLeftTimes = 3;
            main:
            try
            {
                File.WriteAllText("problem.json", JsonConvert.SerializeObject(await GetProblemBodyAsync(problemId)));
                File.WriteAllText("return.json", JsonConvert.SerializeObject(returnFiles));
            }
            catch (Exception ex)
            {
                --retryLeftTimes;
                if (retryLeftTimes <= 0)
                {
                    File.WriteAllText("error.txt", ex.ToString());
                    File.WriteAllText("return.json", JsonConvert.SerializeObject(new[] { "error.txt" }));
                }
                else
                {
                    await Task.Delay(3000);
                    goto main;
                }
            }
            finally
            {
                client.Dispose();
            }
        }

        private async Task<ProblemJson> GetProblemBodyAsync(int problemId)
        {
            var response = await client.GetAsync(problemEndpoint + problemId);
            var html = await response.Content.ReadAsStringAsync();
            var memory = Convert.ToInt32(memoryLimitRegex.Match(html).Value) * 1024 * 1024;
            var time = Convert.ToInt32(timeLimitRegex.Match(html).Value) * 1000;
            var body = bodyRegex.Match(html).Value.Replace("<img src=\"/JudgeOnline", "<img src=\"" + baseUrl + "/JudgeOnline");
            return new ProblemJson
            {
                Body = body,
                Id = problemId.ToString(),
                Source = ProblemSource.Bzoj,
                MemoryLimitInByte = memory,
                TimeLimitInMs = time,
                OriginUrl = problemEndpoint + problemId
            };
        }
    }
}
