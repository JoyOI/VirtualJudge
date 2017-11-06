using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Net.Http;
using System;
using System.Text;
using System.Linq;

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
        public string SubId { get; set; }

        public string Result { get; set; }

        public long TimeUsedInMs { get; set; }

        public long MemoryUsedInByte { get; set; }

        public string Hint { get; set; }
    }

    class SubmissionResult
    {
        public int submission_id { get; set; }
    }

    class LeetcodeJudgeActor
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

        private static async Task MainAsync(VirtualJudgeMetadata metadata, VirtualJudgeAccount account) {
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
                    pollResult = await PollResultAsync(submissionId);
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
            return await client.PostAsync(loginEndpoint, new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "csrfmiddlewaretoken", csrfToken },
                { "login", username },
                { "password", password },
                { "remember", "" }
            }));
        }
        private static async Task<int> SubmitCodeAsync(string problemName, string code, string language) {
            var lang = language.ToLower();
            switch (language) // leave it as switch to extent in the future
            {
                case "C++":
                    lang = "cpp";
                    break;
            }
            var problemUri = problemEndpoint.Replace("{PROBLEM-NAME}", problemName);
            var problemRes = await client.GetAsync(problemUri);
            var problemHTML = await problemRes.Content.ReadAsStringAsync();
            var problemId = questionId.Match(problemHTML);
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
            var submitRes = await client.PostAsync(submitUri, new StringContent(
                JsonConvert.SerializeObject(submitParams),
                Encoding.UTF8, "application/json"));
            var submissionJson = await submitRes.Content.ReadAsStringAsync();
            var submissionResult = JsonConvert.DeserializeObject<SubmissionResult>(submissionJson);
            return submissionResult.submission_id;
        }

        private static async Task<VirtualJudgeResult> PollResultAsync(int submissionId) {
            var checkUri = checkEndpoint.Replace("{SUBMISSION-ID}", submissionId.ToString());
            await Task.Delay(1000);
            var checkRes = await client.GetAsync(checkUri);
            var pollRes = new VirtualJudgeResult
            {
                Result = "WAITING"
            };
            if (checkRes.StatusCode != System.Net.HttpStatusCode.OK)
            {
                return pollRes;
            }
            var resDef = new
            {
                state = "",
                status_msg = "",
                display_runtime = "",
                compile_error = "",
                run_success = false,
                input = "",
                expected_output = "",
                code_output = "",
                status_code = 0,
                last_testcase = "",
                total_correct = 0,
                total_testcases = 0,
                runtime_error = ""
            };
            var resJson = await checkRes.Content.ReadAsStringAsync();
            var result = JsonConvert.DeserializeAnonymousType(resJson, resDef);
            if (result.state == "SUCCESS")
            {
                int totalCorrect = result.total_correct;
                int testcases = result.total_testcases;
                const string ACCEPTED = "Accepted";
                pollRes.Result = result.status_msg;
                if (!result.run_success)
                {
                    pollRes.Result = result.status_msg;
                    switch (result.status_code) {
                        case 20: // Compile Error
                            pollRes.Hint = result.compile_error;
                            pollRes.Result = "CompileError";
                            break;
                        case 11: // Wrong Answer
                            pollRes.Hint = String.Format
                                ("Input: {0} \n Output: {1} \n Expected:{2}",
                                result.input, result.code_output, result.expected_output);
                            pollRes.Result = "WrongAnswer";
                            break;
                        case 14: // Time Limit Exceeded
                            pollRes.Hint = String.Format
                                ("Last executed input: {0}", result.last_testcase);
                            pollRes.Result = "TimeExceeded";
                            break;
                        case 15: // Runtime Error
                            pollRes.Hint = String.Format
                                ("{0} Last test case: {1}", result.runtime_error, result.last_testcase);
                            pollRes.Result = "RuntimeError";
                            break;
                        default: // Unknown Error
                            pollRes.Result = "SystemError";
                            pollRes.Hint = result.status_msg;
                            break;
                        // TODO: more status to be discovered
                    }
                }
                else
                {
                    pollRes.Result = ACCEPTED;
                    pollRes.TimeUsedInMs = Convert.ToInt64(result.display_runtime);
                }
                if (testcases != 0)
                {
                    var successStatuses = Enumerable.Repeat<VirtualJudgeSubStatus>(new VirtualJudgeSubStatus
                    {
                        Result = ACCEPTED
                    }, totalCorrect).ToArray();
                    int totalFailed = testcases - totalCorrect;
                    var failedStatuses = Enumerable.Repeat<VirtualJudgeSubStatus>(new VirtualJudgeSubStatus
                    {
                        Result = pollRes.Result
                    }, totalFailed).ToArray();
                    if (totalFailed != 0)
                    {
                        failedStatuses.First().Hint = pollRes.Hint;
                    }
                    var totalSubStatuses = failedStatuses.Concat(successStatuses);
                    pollRes.SubStatuses = totalSubStatuses;
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
