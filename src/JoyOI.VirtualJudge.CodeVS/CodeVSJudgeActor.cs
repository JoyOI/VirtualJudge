﻿using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Text;
using System.Net.Http;
using Newtonsoft.Json;

namespace JoyOI.VirtualJudge.CodeVS
{
    public class VirtualJudgeMetadata
    {
        public string Source { get; set; }
        public string ProblemId { get; set; }
        public string Language { get; set; }
        public string Code { get; set; }
    }

    public class VirtualJudgeAccount
    {
        public string username { get; set; }

        public string password { get; set; }
    }

    public class VirtualJudgeResult
    {
        public string Result { get; set; }
        public long TimeUsedInMs { get; set; }
        public long MemoryUsedInByte { get; set; }
        public string Hint { get; set; }
        public IEnumerable<VirtualJudgeSubStatus> SubStatuses { get; set; } = new List<VirtualJudgeSubStatus>(10);
    }

    public class VirtualJudgeSubStatus
    {
        public int SubId { get; set; }

        public string Result { get; set; }

        public long TimeUsedInMs { get; set; }

        public long MemoryUsedInByte { get; set; }

        public string Hint { get; set; }
    }

    public class ResultPoll
    {
        public string status { get; set; }
        public int submission_id { get; set; }
        public string outputname { get; set; }
        public int memory_cost { get; set; }

        public int time_cost { get; set; }
        public string inputname { get; set; }
        public string input { get; set; }
        public object rightoutput { get; set; }
        public object useroutput { get; set; }
        public string results { get; set; }
    }

    public static class CodeVSJudgeActor
    {
        private const string BaseUrl = "http://codevs.cn";
        private const string LoginBaseUrl = "https://login.codevs.com";
        private const string FirstLoginEndpoint = "/api/auth/login";
        private const string SecondLoginEndpoint = "/api/auth/token";
        private const string ThirdLoginEndpoint = "/accounts/token/login/?token={TOKEN}";
        private const string ProblemEndpoint = "/problem/{PROBLEMID}";
        private const string JudgeEndpoint = "/judge/";
        private const string ResultEndpoint = "/submission/api/refresh/?id={STATUSID}";
        private static Regex CsrfTokenRegex = new Regex("(?<=<input type='hidden' name='csrfmiddlewaretoken' value=')[a-zA-Z0-9]*");
        private static Regex ResultRegex = new Regex("[a-zA-Z ]{2,}");
        private static Regex SubResultRegex = new Regex("[A-Z]{1,6}(?=</label>)");
        private static Regex SubTimeRegex = new Regex("[0-9]{1,}(?=ms)");
        private static Regex SubMemoryRegex = new Regex("[0-9]{1,}(?=kB)");
        private static Regex InputIdRegex = new Regex("[0-9]*(?=.in)");

        private static System.Net.CookieContainer container = new System.Net.CookieContainer();
        private static HttpClient _client = new HttpClient(new HttpClientHandler() { CookieContainer = container }) { BaseAddress = new Uri(BaseUrl) };

        public static void Main()
        {
            var metadata = JsonConvert.DeserializeObject<VirtualJudgeMetadata>(File.ReadAllText("metadata.json"));
            var account = JsonConvert.DeserializeObject<VirtualJudgeAccount>(File.ReadAllText("account.json"));
            MainAsync(metadata, account).Wait();
        }

        private static async Task MainAsync(VirtualJudgeMetadata metadata, VirtualJudgeAccount account)
        {
            var token = await GetOnlineJudgeLoginToken(account);
            await GetCredantial(token);
            var csrf = await GetCsrfToken(metadata.ProblemId);
            var statusId = await SendToJudge(metadata, csrf);
            if (statusId.Item1 == -1)
            {
                WriteResultFile(new VirtualJudgeResult
                {
                    Hint = statusId.Item2,
                    Result = "CompileError"
                });
            }
            else
            {
                var result = await PollResult(statusId.Item1);
                WriteResultFile(result);
            }
        }

        private static async Task<string> GetOnlineJudgeLoginToken(VirtualJudgeAccount account)
        {
            using (var client = new HttpClient() { BaseAddress = new Uri(LoginBaseUrl) })
            {
                var content = new StringContent(JsonConvert.SerializeObject(new { account.username, account.password }), Encoding.UTF8, "application/json");
                using (var response = await client.PostAsync(FirstLoginEndpoint, content))
                {
                    var text = await response.Content.ReadAsStringAsync();
                    var jwt = JsonConvert.DeserializeObject<dynamic>(text).jwt;
                    client.DefaultRequestHeaders.Add("Authorization", "JWT " + jwt);
                    using (var response2 = await client.GetAsync(SecondLoginEndpoint))
                    {
                        return JsonConvert.DeserializeObject<dynamic>(await response2.Content.ReadAsStringAsync()).token;
                    }
                }
            }
        }

        private static async Task GetCredantial(string token)
        {
            await _client.GetAsync(ThirdLoginEndpoint.Replace("{TOKEN}", token));
        }

        private static async Task<string> GetCsrfToken(string problemId)
        {
            using (var response = await _client.GetAsync(ProblemEndpoint.Replace("{PROBLEMID}", problemId)))
            {
                var html = await response.Content.ReadAsStringAsync();
                return CsrfTokenRegex.Match(html).Value;
            }
        }

        private static string ParseLanguage(string lang)
        {
            switch (lang)
            {
                case "C":
                    return "c";
                case "C++":
                    return "cpp";
                case "Pascal":
                    return "pas";
                default:
                    throw new NotSupportedException(lang + " has not been supported in this problem source");
            }
        }

        private static async Task<(int, string)> SendToJudge(VirtualJudgeMetadata metadata, string csrf)
        {
            var problemPageUri = ProblemEndpoint.Replace("{PROBLEMID}", metadata.ProblemId);
            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "id", metadata.ProblemId },
                { "code", metadata.Code },
                { "format", ParseLanguage(metadata.Language) },
                { "csrfmiddlewaretoken", csrf }
            });
            var req = new HttpRequestMessage(HttpMethod.Post, JudgeEndpoint)
            {
                Content = content,
            };
            // the following headers are just for redundant in case of future changes
            // it's not necessary, for now
            req.Headers.TryAddWithoutValidation("Referer", BaseUrl + problemPageUri + "/");
            req.Headers.TryAddWithoutValidation("X-Requested-With", "XMLHttpRequest");
            req.Headers.TryAddWithoutValidation("Host", "codevs.cn");
            req.Headers.TryAddWithoutValidation("Origin", BaseUrl);

            send:
            try
            {
                using (var response = await _client.SendAsync(req))
                {
                    var text = await response.Content.ReadAsStringAsync();
                    var json = JsonConvert.DeserializeObject<dynamic>(text);
                    if (json.success == false)
                    {
                        return (-1, json.message);
                    }
                    else
                    {
                        return (json.id, null);
                    }
                }
            }
            catch (Exception ex)
            {
                await Task.Delay(1000);
                goto send;
            }
        }

        private static async Task<VirtualJudgeResult> PollResult(int statusId)
        {
            main:
            using (var response = await _client.GetAsync(ResultEndpoint.Replace("{STATUSID}", statusId.ToString())))
            {
                var text = await response.Content.ReadAsStringAsync();
                var result = JsonConvert.DeserializeObject<ResultPoll>(text);
                if (result.status == "等待测试 Pending")
                {
                    await Task.Delay(1000);
                    goto main;
                }

                var totalResult = ResultRegex.Match((string)result.status).Value.Trim().Replace("Limit", "").Replace(" ", "");
                IEnumerable<VirtualJudgeSubStatus> subStatuses = ParseSubStatuses(result.results);
                if (totalResult == "WrongAnswer")
                    subStatuses.Single(x => x.SubId == FindHintId(result)).Hint =
                        $"{ result.input } \n\n " +
                        $"{ (result.useroutput is string ? result.useroutput : ((dynamic)result.useroutput).LastOrDefault()) } \n\n " +
                        $"{ (result.rightoutput is string ? result.rightoutput : ((dynamic)result.rightoutput).LastOrDefault()) }";

                return new VirtualJudgeResult
                {
                    MemoryUsedInByte = result.memory_cost,
                    TimeUsedInMs = result.time_cost,
                    Result = totalResult,
                    SubStatuses = ParseSubStatuses(result.results),
                    Hint = totalResult == "CompileError" ? result.results : ""
                };
            }
        }

        private static IEnumerable<VirtualJudgeSubStatus> ParseSubStatuses(string html)
        {
            var results = SubResultRegex.Matches(html).Select(x => ShortResultToFull(x.Value)).ToList();
            var time = SubTimeRegex.Matches(html).Select(x => Convert.ToInt32(x.Value)).ToList();
            var memory = SubMemoryRegex.Matches(html).Select(x => Convert.ToInt32(x.Value)).ToList();
            for (var i = 0; i < results.Count(); i++)
            {
                yield return new VirtualJudgeSubStatus
                {
                    SubId = i + 1,
                    MemoryUsedInByte = memory[i],
                    TimeUsedInMs = time[i],
                    Result = results[i]
                };
            }
        }

        private static string ShortResultToFull(string result)
        {
            switch (result)
            {
                case "AC":
                    return "Accepted";
                case "WA":
                    return "WrongAnswer";
                case "TLE":
                    return "TimeExceeded";
                case "MLE":
                    return "MemoryExceeded";
                case "RE":
                    return "RuntimeError";
                default:
                    return "SystemError";
            }
        }

        private static int FindHintId(ResultPoll result)
        {
            var id = Convert.ToInt32(InputIdRegex.Match(result.inputname).Value);
            return id;
        }

        private static void WriteResultFile(VirtualJudgeResult result)
        {
            File.WriteAllText("result.json", JsonConvert.SerializeObject(result));
            var returnFiles = new[] { "result.json" };
            File.WriteAllText("return.json", JsonConvert.SerializeObject(new { Outputs = returnFiles }));
        }
    }
}