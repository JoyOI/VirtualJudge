using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace JoyOI.VirtualJudge.LeetCode.Actor
{
    class LeetcodePullProblemBodyActor
    {
        private const string baseUrl = "https://leetcode.com";
        private const string problemEndpoint = "/problems/{PROBLEM-NAME}/description/";
        private static List<string> returnFiles = new List<string>(1) { "problem.json" };
        private static HttpClient client = new HttpClient() { BaseAddress = new Uri(baseUrl) };
        private static Regex dataRegex = new Regex("var pageData =(?:(?!</script>)[\\s\\S])*");
        private static Regex bodyRegex = new Regex(@"(?<=<div class=""question-description"">)[\\s\\S]*(?=</div>[\\s\\S]*?<!-- Interview Feedback -->)");
        private static Regex titleRegex = new Regex(@"(?<=<div class=""question-title clearfix"">[\\s\\S]*?<div class=""row"">[\\s\\S]*?<div class=""col-md-12"">[\\s\\S]*?<h3>[\\s\\S]*?)[\\s\\S]*(?=<\/h3>)");

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
            catch (Exception ex)
            {
                --retryLeftTimes;
                if (retryLeftTimes <= 0)
                {
                    File.WriteAllText("error.txt", ex.ToString());
                    File.WriteAllText("return.json", JsonConvert.SerializeObject(new { Outputs = new[] { "error.txt" } }));
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
        private static async Task<Object> GetProblemBodyAsync(string problemName) {
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
            p.StandardInput.WriteLine("1000 1000");
            p.StandardInput.WriteLine(String.Format("node {0}", jsFile));
            p.WaitForExit();
            var templates = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText("stdout.txt"));
            var body = bodyRegex.Match(problemHTML).Value
                .Replace("src=\"/", "src=\"" + baseUrl + "/"); 
            var title = bodyRegex.Match(problemHTML).Value;
            return new
            {
                Body = body,
                Id = problemName,
                Source = "Leetcode",
                OriginUrl = baseUrl + problemUri,
                Title = title
            };
        }
    }
}
