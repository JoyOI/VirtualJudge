using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using System.Text.RegularExpressions;
using System.Net.Http;
using System.IO;
using Newtonsoft.Json;

namespace JoyOI.VirtualJudge.CodeVS
{
    public static class CodeVSPullProblemSetActor
    {
        private const string BaseUrl = "http://codevs.cn";
        private const string ProblemListEndpoint = "/problem/?problemset_id=1&page={PAGE}";
        private static Regex PageNumberRegex = new Regex("(?<=/problem/\\?problemset_id=1&page=)[0-9]{1,3}");
        private static Regex ProblemIdRegex = new Regex("(?<=/problem/)[0-9]{4,6}");
        private static HttpClient client = new HttpClient()
        {
            BaseAddress = new Uri(BaseUrl)
        };

        public static void Main()
        {
            MainAsync().Wait();
        }

        public static async Task MainAsync()
        {
            var ret = new List<int>(200);
            var cnt = await GetMaxPageAsync();
            for (var i = 1; i <= cnt; i++)
            {
                ret.AddRange(await GetProblemIdsAsync(i));
            }
            File.WriteAllText("problemset.json", JsonConvert.SerializeObject(ret));
            File.WriteAllText("return.json", JsonConvert.SerializeObject(new { Outputs = new[] { "problemset.json" } }));
        }

        public static async Task<int> GetMaxPageAsync()
        {
            using (var response = await client.GetAsync(ProblemListEndpoint.Replace("{PAGE}", "1")))
            {
                var html = await response.Content.ReadAsStringAsync();
                var matches = PageNumberRegex.Matches(html) as IEnumerable<Match>;
                return matches.Select(x => Convert.ToInt32(x.Value)).Max();
            }
        }

        public static async Task<IEnumerable<int>> GetProblemIdsAsync(int page)
        {
            var response = await client.GetAsync(ProblemListEndpoint.Replace("{PAGE}", page.ToString()));
            var html = await response.Content.ReadAsStringAsync();
            var matches = ProblemIdRegex.Matches(html) as IEnumerable<Match>;
            return matches.Select(x => Convert.ToInt32(x.Value));
        }
    }
}
