using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace JoyOI.VirtualJudge.LeetCode.Actor
{
    class LeetCodePullProblemBodyActor
    {

        private class RunnerResult {
            public int ExitCode { get; set; }
            public string Error { get; set; }
        }

        private const string baseUrl = "https://leetcode.com";
        private const string problemEndpoint = "/problems/{PROBLEM-NAME}/description/";
        private static List<string> returnFiles = new List<string>(1) { "problem.json" };
        private static HttpClient client = new HttpClient() { BaseAddress = new Uri(baseUrl) };
        private static Regex dataRegex = new Regex("var pageData =(?:(?!</script>)[\\s\\S])*");
        private static Regex bodyRegex = new Regex(@"(?<=<div class=""question-description"">)[\s\S]*(?=</div>[\s\S]*?<!-- Interview Feedback -->)");
        private static Regex titleRegex = new Regex(@"(?<=<title>).*(?= - LeetCode</title>)");

        public static void Main()
        {
            MainAsync(File.ReadAllText("id.txt")).Wait();
        }

        private static async Task MainAsync(string problemName)
        {
            var retryLeftTimes = 3;
            main:
            try
            {
                File.WriteAllText("problem.json", JsonConvert.SerializeObject(await GetProblemBodyAsync(problemName)));
                File.WriteAllText("return.json", JsonConvert.SerializeObject(new { Outputs = returnFiles }));
            }
            catch (Exception)
            {
                --retryLeftTimes;
                if (retryLeftTimes <= 0)
                {
                    throw;
                }
                else
                {
                    await Task.Delay(1000);
                    goto main;
                }
            }
        }
        private static async Task<Object> GetProblemBodyAsync(string problemName)
        {
            var problemUri = problemEndpoint.Replace("{PROBLEM-NAME}", problemName);
            var problemRes = await client.GetAsync(problemUri);
            var problemHTML = await problemRes.Content.ReadAsStringAsync();
            var jsFile = "extract.js";
            var dataJs = dataRegex.Match(problemHTML).Value;
            var execJs = String.Format("{0} \n {1}", dataJs,
                @"var ret = {};
                  for (var i = 0; i < pageData.codeDefinition.length; i++)
                  {
                    ret[pageData.codeDefinition[i].text] = pageData.codeDefinition[i].defaultCode;
                  }
                  console.log(JSON.stringify(ret)); ");
            File.WriteAllText(jsFile, execJs);
            var p = Process.Start(new ProcessStartInfo("runner") { RedirectStandardInput = true });
            p.StandardInput.WriteLine("10000 10000 0");
            p.StandardInput.WriteLine(String.Format("node --stack_size=256 --max_old_space_size=256 {0}", jsFile));
            p.WaitForExit();
            var templates = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText("stdout.txt"));
            var templateStatus = JsonConvert.DeserializeObject<RunnerResult>(File.ReadAllText("runner.json"));
            var body = bodyRegex.Match(problemHTML).Value
                .Replace("src=\"/", "src=\"" + baseUrl + "/");
            var title = titleRegex.Match(problemHTML).Value;
            if (templateStatus.ExitCode != 0) {
                throw new Exception(
                    "Body extraction runner failed with exit code: " +
                    templateStatus.ExitCode +
                    ", stderr: " + 
                    templateStatus.Error);
            }

            return new
            {
                Body = string.Join("\n", body.Split('\n').Select(x => x.Trim())),
                Id = problemName,
                Source = "LeetCode",
                OriginUrl = baseUrl + problemUri,
                CodeTemplate = templates,
                Title = title
            };
        }
    }
}
