using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Net.Http;
using System;
using System.Text;
using System.Linq;
using System.Net;

namespace JoyOI.VirtualJudge.LeetCode.Actor
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

        public IEnumerable<VirtualJudgeSubStatus> SubStatuses { get; set; }
    }

    public class VirtualJudgeSubStatus
    {
        public int SubId { get; set; }

        public string Result { get; set; }

        public long TimeUsedInMs { get; set; }

        public long MemoryUsedInByte { get; set; }

        public string Hint { get; set; }
    }

    class SubmissionResult
    {
        public int submission_id { get; set; }
    }

    class SubmissionStatus
    {
        public string state { get; set; }
        public string status_msg { get; set; }
        public string display_runtime { get; set; }
        public string compile_error { get; set; }
        public bool run_success { get; set; }
        public string input { get; set; }
        public string expected_output { get; set; }
        public string code_output { get; set; }
        public int status_code { get; set; }
        public string last_testcase { get; set; }
        public int? total_correct { get; set; }
        public int? total_testcases { get; set; }
        public string runtime_error { get; set; }
        public string status_runtime { get; set; }
        public string compare_result { get; set; }
    }

    class LeetCodeJudgeActor
    {
        private const string baseUrl = "https://leetcode.com";
        private const string loginEndpoint = "/accounts/login/";
        private const string problemEndpoint = "/problems/{PROBLEM-NAME}/description/";
        private const string submitEndpoint = "/problems/{PROBLEM-NAME}/submit/";
        private const string checkEndpoint = "/submissions/detail/{SUBMISSION-ID}/check/";

        private static Regex csrfTokenRegex = new Regex("(?<=<input type='hidden' name='csrfmiddlewaretoken' value=')[a-zA-Z0-9]{0,}");
        private static Regex questionId = new Regex("(?<=questionId: \')[0-9]{0,}");

        private static System.Net.CookieContainer container = new System.Net.CookieContainer();
        private static HttpClient client = new HttpClient(new HttpClientHandler() { CookieContainer = container }) { BaseAddress = new Uri(baseUrl) };

        public static void Main()
        {
            var metadata = JsonConvert.DeserializeObject<VirtualJudgeMetadata>(File.ReadAllText("metadata.json"));
            var account = JsonConvert.DeserializeObject<VirtualJudgeAccount>(File.ReadAllText("account.json"));
            MainAsync(metadata, account).Wait();
        }

        private static async Task MainAsync(VirtualJudgeMetadata metadata, VirtualJudgeAccount account)
        {
            var retryLeftTimes = 3;
            trySubmit:
            try
            {
                await GetCredantial(account.username, account.password);
                var submissionId = await SubmitCodeAsync(metadata.ProblemId, metadata.Code, metadata.Language);
                if (submissionId < 1)
                {
                    throw new Exception("Failed to submit user code to leetcode");
                }
                VirtualJudgeResult pollResult;
                do
                {
                    await Task.Delay(1000);
                    pollResult = await PollResultAsync(metadata.ProblemId, submissionId);
                } while (pollResult.Result == "WAITING");
                WriteResultFile(pollResult);
            }
            catch (Exception ex)
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

        private static async Task<HttpResponseMessage> GetCredantial(string username, string password)
        {
            var logingPageRes = await client.GetAsync(loginEndpoint);
            var loginPaheHTML = await logingPageRes.Content.ReadAsStringAsync();
            var csrfToken = csrfTokenRegex.Match(loginPaheHTML).Value;
            var loginContent = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "csrfmiddlewaretoken", csrfToken },
                { "login", username },
                { "password", password },
                { "remember", "" }
            });
            var loginReq = new HttpRequestMessage(HttpMethod.Post, loginEndpoint)
            {
                Content = loginContent,
            };
            loginReq.Headers.Referrer = new Uri(baseUrl + loginEndpoint);
            var result = await client.SendAsync(loginReq);
            var resultContent = await result.Content.ReadAsStringAsync();
            return result;
        }

        private static void DecorateCSRFRequest(HttpRequestMessage req, string problemName, string reqUri)
        {
            var problemUri = problemEndpoint.Replace("{PROBLEM-NAME}", problemName);
            IEnumerable<Cookie> responseCookies = container.GetCookies(new Uri(baseUrl)).Cast<Cookie>();
            req.Headers.Referrer = new Uri(baseUrl + problemUri);
            req.Headers.TryAddWithoutValidation(Uri.EscapeDataString(":authority"), "leetcode.com");
            req.Headers.TryAddWithoutValidation(Uri.EscapeDataString(":method"), "POST");
            req.Headers.TryAddWithoutValidation(Uri.EscapeDataString(":path"), reqUri);
            req.Headers.TryAddWithoutValidation(Uri.EscapeDataString(":scheme"), "https");
            req.Headers.TryAddWithoutValidation("origin", "https://leetcode.com");
            req.Headers.TryAddWithoutValidation("x-requested-with", "XMLHttpRequest");
            req.Headers.TryAddWithoutValidation("x-csrftoken",
                responseCookies
                .Where(c => c.Name == "csrftoken")
                .FirstOrDefault()
                .Value);
        }

        private static async Task<int> SubmitCodeAsync(string problemName, string code, string language)
        {
            var lang = language.ToLower();
            switch (language.Trim()) // leave it as switch to extent in the future
            {
                case "C++":
                    lang = "cpp";
                    break;
                case "C#":
                    lang = "csharp";
                    break;
            }
            var problemUri = problemEndpoint.Replace("{PROBLEM-NAME}", problemName);
            var problemRes = await client.GetAsync(problemUri);
            var problemHTML = await problemRes.Content.ReadAsStringAsync();
            var problemId = questionId.Match(problemHTML).Value;
            var submitUri = submitEndpoint.Replace("{PROBLEM-NAME}", problemName);
            var submitParams = new
            {
                data_input = "",
                judge_type = "large",
                lang = lang,
                question_id = problemId,
                test_mode = false,
                typed_code = code,
            };
            var submitReq = new HttpRequestMessage(HttpMethod.Post, submitUri)
            {
                Content = new StringContent(
                JsonConvert.SerializeObject(submitParams),
                Encoding.UTF8, "application/json")
            };
            DecorateCSRFRequest(submitReq, problemName, submitUri);
            var submitRes = await client.SendAsync(submitReq);
            var submissionJson = await submitRes.Content.ReadAsStringAsync();
            if (!submitRes.IsSuccessStatusCode)
            {
                throw new Exception(String.Format("Failed to send code to LeetCode server for lang {2}: {0} - {1}", submitRes.StatusCode, submissionJson, lang));
            }
            var submissionResult = JsonConvert.DeserializeObject<SubmissionResult>(submissionJson);
            return submissionResult.submission_id;
        }

        private static async Task<VirtualJudgeResult> PollResultAsync(string problemName, int submissionId)
        {
            var checkUri = checkEndpoint.Replace("{SUBMISSION-ID}", submissionId.ToString());
            var checkReq = new HttpRequestMessage(HttpMethod.Get, checkUri);
            DecorateCSRFRequest(checkReq, problemName, checkUri);
            var checkRes = await client.GetAsync(checkUri);
            var pollRes = new VirtualJudgeResult
            {
                Result = "WAITING"
            };
            if (checkRes.StatusCode != System.Net.HttpStatusCode.OK)
            {
                return pollRes;
            }
            var resJson = await checkRes.Content.ReadAsStringAsync();
            var result = JsonConvert.DeserializeObject<SubmissionStatus>(resJson);
            if (result.state == "SUCCESS")
            {
                int totalCorrect = result.total_correct.GetValueOrDefault(0);
                int testcases = result.total_testcases.GetValueOrDefault(0);
                const string ACCEPTED = "Accepted";
                pollRes.Result = result.status_msg;
                switch (result.status_code)
                {
                    case 10: // Suuccess
                        pollRes.Result = ACCEPTED;
                        pollRes.TimeUsedInMs = Convert.ToInt64(result.status_runtime.Replace(" ms", ""));
                        break;
                    case 20: // Compile Error
                        pollRes.Hint = result.compile_error;
                        pollRes.Result = "CompileError";
                        break;
                    case 11: // Wrong Answer
                        pollRes.Hint = $"Input: \n```\n{result.input}\n``` \n\n Output: \n```\n{result.code_output}\n``` \n\n Expected:{result.expected_output}";
                        pollRes.Result = "WrongAnswer";
                        break;
                    case 14: // Time Limit Exceeded
                        pollRes.Hint = $"Last executed input: {result.last_testcase}";
                        pollRes.Result = "TimeExceeded";
                        break;
                    case 15: // Runtime Error
                        pollRes.Hint = $"{result.runtime_error} Last test case: {result.last_testcase}";
                        pollRes.Result = "RuntimeError";
                        break;
                    default: // Unknown Error
                        pollRes.Result = "SystemError";
                        pollRes.Hint = result.status_msg;
                        break;
                        // TODO: more status to be discovered
                }
                if (testcases != 0)
                {
                    var subStatuses = result.compare_result
                        .ToCharArray()
                        .Select(c => {
                            return new VirtualJudgeSubStatus
                            {
                                Result = (c == '1') ? ACCEPTED : pollRes.Result
                            };
                        })
                        .ToArray();
                    var failureIndex = 0;
                    var totalFailed = result.total_testcases - totalCorrect;
                    for (int i = 0; i < subStatuses.Count(); i++)
                    {
                        var status = subStatuses[i];
                        status.SubId = i + 1;
                        if (status.Result != ACCEPTED)
                        {
                            failureIndex++;
                            if (failureIndex == totalFailed)
                            {
                                status.Hint = pollRes.Hint;
                            }
                        }
                    }
                    pollRes.SubStatuses = subStatuses;
                }
            }
            return pollRes;
        }
        private static void WriteResultFile(VirtualJudgeResult result)
        {
            File.WriteAllText("result.json", JsonConvert.SerializeObject(result));
            var returnFiles = new[] { "result.json" };
            File.WriteAllText("return.json", JsonConvert.SerializeObject(new { Outputs = returnFiles }));
        }
    }
}
