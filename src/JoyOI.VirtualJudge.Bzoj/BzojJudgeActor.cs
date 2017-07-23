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
        private string statusEndpoint = "/JudgeOnline/status.php?user_id=";
        private string ceinfoEndpoint = "/JudgeOnline/ceinfo.php?sid=";
        private const string statusRegexString = "(?<=<td>{STATUSID}<td><a href='userinfo.php\\?user=[a-zA-Z0-9]{0,}'>[a-zA-Z0-9]{0,}</a><td><a href='problem.php\\?id=[0-9]{4,8}'>[0-9]{4,8}</a><td><font color=([a-zA-Z]{1,8}|#[0-9]{6})>)[a-zA-Z_ ]{1,}(?=</font>)";
        private const string memoryUsedRegexString = "(?<=<td>{STATUSID}<td><a href='userinfo.php\\?user=[a-zA-Z0-9]{0,}'>[a-zA-Z0-9]{0,}</a><td><a href='problem.php\\?id=[0-9]{4,8}'>[0-9]{4,8}</a><td><font color=([a-zA-Z]{1,8}|#[0-9]{6})>{STATUS}</font><td>)[0-9]{1,}(?= <font color=red>kb</font><td>)";
        private const string timeUsedRegexString = "(?<=<td>{STATUSID}<td><a href='userinfo.php\\?user=[a-zA-Z0-9]{0,}'>[a-zA-Z0-9]{0,}</a><td><a href='problem.php\\?id=[0-9]{4,8}'>[0-9]{4,8}</a><td><font color=([a-zA-Z]{1,8}|#[0-9]{6})>{STATUS}</font><td>[0-9]{1,} <font color=red>kb</font><td>)[0-9]{1,}(?= <font color=red>ms</font><td>)";
        private Regex statusIdRegex = new Regex("(?<=<a target=_blank href=showsource.php\\?id=)\\d+");
        private Regex compileErrorInformationRegex = new Regex("(?<=<pre>)([\\d\\D]*)(?=</pre>)");
        private HttpClient client = new HttpClient() { BaseAddress = new Uri(baseUrl) };

        public void Main()
        {
            var metadata = JsonConvert.DeserializeObject<VirtualJudgeMetadata>(File.ReadAllText("metadata.json"));
            statusEndpoint += metadata.Username;
            MainAsync(metadata).Wait();
        }

        private async Task MainAsync(VirtualJudgeMetadata metadata)
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
            finally
            {
                client.Dispose();
            }
        }

        private async Task GetCookieAsync(string username, string password)
        {
            var response = await client.PostAsync(loginEndpoint, new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "user_id", username },
                { "password", password }
            }));
            var cookie = response.Headers.Single(x => x.Key == "Set-Cookie").Value.Last().Replace(" path=/", "");
            client.DefaultRequestHeaders.Remove("Cookie");
            client.DefaultRequestHeaders.Add("Cookie", cookie);
        }

        private async Task<string> SubmitCodeAsync(string problemId, string code, string language)
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
            var html = await response2.Content.ReadAsStringAsync();
            return statusIdRegex.Match(html).Value;
        }

        private async Task<PollResult> PollResultAsync(string statusId)
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

        private string ParseResult(string bzojResult)
        {
            return bzojResult.Replace("_", " ");
        }

        private void WriteResultFile(VirtualJudgeResult result)
        {
            File.WriteAllText("result.json", JsonConvert.SerializeObject(result));
            var returnFiles = new[] { "result.json" };
            File.WriteAllText("return.json", JsonConvert.SerializeObject(returnFiles));
        }

        private async Task<string> GetCompileErrorInformationAsync(string statusId)
        {
            var response = await client.GetAsync(ceinfoEndpoint);
            var html = await response.Content.ReadAsStringAsync();
            return compileErrorInformationRegex.Match(html).Value;
        }
    }
}
