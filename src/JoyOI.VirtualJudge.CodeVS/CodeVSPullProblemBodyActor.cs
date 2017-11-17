using System;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Net.Http;
using System.IO;
using Newtonsoft.Json;

namespace JoyOI.VirtualJudge.CodeVS
{
    public static class CodeVSPullProblemBodyActor
    {
        public class ProblemBody
        {
            public string Id { get; set; }
            public string Body { get; set; }
            public string Source { get; set; } = "CodeVS";
            public int MemoryLimitInByte { get; set; }
            public int TimeLimitInMs { get; set; }
            public string Title { get; set; }
            public string OriginUrl { get; set; }
        }

        private const string BaseUrl = "http://codevs.cn";
        private const string ProblemEndpoint = "/problem/{PROBLEMID}";
        private const string HeaderReplaceFrom = "class=\"panel panel-default\"";
        private const string HeaderReplaceTo = "class=\"area-title\"";
        private static HttpClient client = new HttpClient() { BaseAddress = new Uri(BaseUrl) };
        private static Regex BodyRegex = new Regex("(?<=<div class=\"col-lg-9 no-padder \">)[\\s\\S]*(?=<div class=\"m-b-xxl\">)");
        private static Regex TitleRegex = new Regex("(?<=<h3 class=\"m-t m-b-sm\" style=\"display:inline-block\">  <b>).*(?=</b></h3>)");
        private static Regex TimeRegex = new Regex("(?<=时间限制: )[0-9]{1,}");
        private static Regex MemoryRegex = new Regex("(?<=空间限制: )[0-9]{1,}");

        public static void Main()
        {
            MainAsync(Convert.ToInt32(File.ReadAllText("id.txt"))).Wait();
        }

        public static async Task MainAsync(int id)
        {
            using (var response = await client.GetAsync(ProblemEndpoint.Replace("{PROBLEMID}", id.ToString())))
            {
                var html = await response.Content.ReadAsStringAsync();
                var body = BodyRegex.Match(html).Value.Replace(HeaderReplaceFrom, HeaderReplaceTo).Replace("<img src=\"/", "<img src=\"http://codevs.cn/").Trim('\n').Trim();
                var title = TitleRegex.Match(html).Value.Substring(id.ToString().Length + 1).Trim();
                var time = Convert.ToInt32(TimeRegex.Match(html).Value) * 1000;
                var memory = Convert.ToInt32(MemoryRegex.Match(html).Value) * 1024;
                var ret = new ProblemBody
                {
                    Body = body,
                    Id = id.ToString(),
                    MemoryLimitInByte = memory,
                    OriginUrl = BaseUrl + ProblemEndpoint.Replace("{PROBLEMID}", id.ToString()),
                    TimeLimitInMs = time,
                    Title = title
                };

                File.WriteAllText("problem.json", JsonConvert.SerializeObject(ret));
                File.WriteAllText("return.json", JsonConvert.SerializeObject(new { Outputs = new[] { "problem.json" } }));
            }
        }
    }
}
