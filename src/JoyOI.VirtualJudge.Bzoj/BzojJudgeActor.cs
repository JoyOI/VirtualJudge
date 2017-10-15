using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Net.Http;
using Newtonsoft.Json;

namespace JoyOI.VirtualJudge.Bzoj.Actor
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

    public class VirtualJudgeResult
    {
        public string Result { get; set; }
        public long TimeUsedInMs { get; set; }
        public long MemoryUsedInByte { get; set; }
        public string Hint { get; set; }
    }

    public class PollResult
    {
        public string Result { get; set; }
        public long TimeUsedInMs { get; set; }
        public long MemoryUsedInByte { get; set; }
    }

    public class BzojJudgeActor
    {
        private const string baseUrl = "http://www.lydsy.com";
        private const string loginEndpoint = "/JudgeOnline/login.php";
        private const string submitEndpoint = "/JudgeOnline/submit.php";
        private static string statusEndpoint = "/JudgeOnline/status.php?user_id=";
        private static string ceinfoEndpoint = "/JudgeOnline/ceinfo.php?sid=";
        private static string statusListEndpoint = "/JudgeOnline/status.php?problem_id={PROBLEM_ID}&user_id=joyoivjudge1&language={LANGUAGE}&jresult=-1";
        private const string statusRegexString = "(?<=<td>{STATUSID}<td><a href='userinfo.php\\?user=[a-zA-Z0-9]{0,}'>[a-zA-Z0-9]{0,}</a><td><a href='problem.php\\?id=[0-9]{4,8}'>[0-9]{4,8}</a><td><font color=([a-zA-Z]{1,8}|#[0-9]{6})>)[a-zA-Z_ ]{1,}(?=</font>)";
        private const string memoryUsedRegexString = "(?<=<td>{STATUSID}<td><a href='userinfo.php\\?user=[a-zA-Z0-9]{0,}'>[a-zA-Z0-9]{0,}</a><td><a href='problem.php\\?id=[0-9]{4,8}'>[0-9]{4,8}</a><td><font color=([a-zA-Z]{1,8}|#[0-9]{6})>{STATUS}</font><td>)[0-9]{1,}(?= <font color=red>kb</font><td>)";
        private const string timeUsedRegexString = "(?<=<td>{STATUSID}<td><a href='userinfo.php\\?user=[a-zA-Z0-9]{0,}'>[a-zA-Z0-9]{0,}</a><td><a href='problem.php\\?id=[0-9]{4,8}'>[0-9]{4,8}</a><td><font color=([a-zA-Z]{1,8}|#[0-9]{6})>{STATUS}</font><td>[0-9]{1,} <font color=red>kb</font><td>)[0-9]{1,}(?= <font color=red>ms</font><td>)";
        private static Regex statusIdRegex = new Regex("(?<=<a target=_blank href=showsource.php\\?id=)\\d+");
        private static Regex compileErrorInformationRegex = new Regex("(?<=<pre>)([\\d\\D]*)(?=</pre>)");
        private static System.Net.CookieContainer container = new System.Net.CookieContainer();
        private static HttpClient client = new HttpClient(new HttpClientHandler() { CookieContainer = container }) { BaseAddress = new Uri(baseUrl) };

        public static void Main()
        {
            var metadata = JsonConvert.DeserializeObject<VirtualJudgeMetadata>(File.ReadAllText("metadata.json"));
            statusEndpoint += metadata.Username;
            MainAsync(metadata).Wait();
        }

        private static async Task MainAsync(VirtualJudgeMetadata metadata)
        {
            var retryLeftTimes = 3;

            trySubmit:

            try
            {
                await GetCookieAsync(metadata.Username, metadata.Password);
                var statusId = await SubmitCodeAsync(metadata.ProblemId, metadata.Code, metadata.Language);
                if (string.IsNullOrEmpty(statusId))
                {
                    throw new Exception("Failed to submit user code to bzoj.");
                }
                ceinfoEndpoint += statusId;
                PollResult pollResult;
                do
                {
                    await Task.Delay(300);
                    pollResult = await PollResultAsync(statusId);
                }
                while (pollResult.Result == "Pending" || pollResult.Result == "Compiling" || pollResult.Result == "Running_&_Judging");
                WriteResultFile(new VirtualJudgeResult
                {
                    Hint = pollResult.Result == "Compile_Error" ? await GetCompileErrorInformationAsync(statusId) : "",
                    Result = ParseResult(pollResult.Result),
                    MemoryUsedInByte = pollResult.MemoryUsedInByte,
                    TimeUsedInMs = pollResult.TimeUsedInMs
                });
            }
            catch(Exception ex)
            {
                --retryLeftTimes;
                if (retryLeftTimes <= 0)
                {
                    WriteResultFile(new VirtualJudgeResult
                    {
                        Hint = ex.ToString(),
                        Result = "System Error",
                        MemoryUsedInByte = 0,
                        TimeUsedInMs = 0
                    });
                }
                else
                {
                    await Task.Delay(3000);
                    goto trySubmit;
                }
            }
        }

        private static async Task GetCookieAsync(string username, string password)
        {
            var response = await client.PostAsync(loginEndpoint, new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "user_id", username },
                { "password", password }
            }));
        }

        private static async Task<string> SubmitCodeAsync(string problemId, string code, string language)
        {
            int langId = -1;
            switch (language)
            {
                case "C":
                    langId = 0;
                    break;
                case "C++":
                    langId = 1;
                    break;
                case "Pascal":
                    langId = 2;
                    break;
                case "Java":
                    langId = 3;
                    break;
                case "Python":
                    langId = 6;
                    break;
            }
            var body = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "id", problemId },
                { "language", langId.ToString() },
                { "source", code }
            });
            var msg = new HttpRequestMessage(HttpMethod.Post, submitEndpoint) { Content = body };
            var response2 = await client.SendAsync(msg);
            var response3 = await client.GetAsync(statusListEndpoint.Replace("{PROBLEM_ID}", problemId).Replace("{LANGUAGE}", langId.ToString()));
            var html = await response3.Content.ReadAsStringAsync();
            return statusIdRegex.Match(html).Value;
        }

        private static async Task<PollResult> PollResultAsync(string statusId)
        {
            var response = await client.GetAsync(statusEndpoint);
            var html = await response.Content.ReadAsStringAsync();
            var statusRegex = new Regex(statusRegexString.Replace("{STATUSID}", statusId));
            var ret = new PollResult();
            ret.Result = statusRegex.Match(html).Value;
            if (ret.Result != "Pending")
            {
                var memoryUsedRegex = new Regex(memoryUsedRegexString.Replace("{STATUSID}", statusId).Replace("{STATUS}", ret.Result));
                var timeUsedRegex = new Regex(timeUsedRegexString.Replace("{STATUSID}", statusId).Replace("{STATUS}", ret.Result));
                try
                {
                    ret.MemoryUsedInByte = Convert.ToInt64(memoryUsedRegex.Match(html).Value) * 1024;
                    ret.TimeUsedInMs = Convert.ToInt64(timeUsedRegex.Match(html).Value);
                }
                finally
                {
                }
            }
            return ret;
        }

        private static string ParseResult(string bzojResult)
        {
            var ret = bzojResult.Replace("_", " ").Replace(" Limit", "");
            if (ret.EndsWith("Exceed"))
                ret += "ed";
            return ret;
        }

        private static void WriteResultFile(VirtualJudgeResult result)
        {
            File.WriteAllText("result.json", JsonConvert.SerializeObject(result));
            var returnFiles = new[] { "result.json" };
            File.WriteAllText("return.json", JsonConvert.SerializeObject(new { Outputs = returnFiles }));
        }

        private static async Task<string> GetCompileErrorInformationAsync(string statusId)
        {
            var response = await client.GetAsync(ceinfoEndpoint);
            var html = await response.Content.ReadAsStringAsync();
            return compileErrorInformationRegex.Match(html).Value;
        }
    }
}
