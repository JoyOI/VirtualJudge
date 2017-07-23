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
    public class BzojPullProblemSetActor
    {
        private HttpClient client;
        private const string baseUrl = "http://www.lydsy.com";
        private const string problemsetEndpoint = "/JudgeOnline/problemset.php?page={PAGE}";
        private Regex ProblemIdRegex = new Regex("(?<=<a href='problem.php\\?id=)[0-9]{4,6}(?='>)");
        private Regex PageIndexRegex = new Regex("(?<=<a href='problemset.php\\?page=)[0-9]{1,}(?='>[0-9]{1,}</a>)");
        private int page = 1;

        public void Main()
        {
            client = new HttpClient() { BaseAddress = new Uri(baseUrl) };
        }

        private async Task MainAsync()
        {
            var problemIds = (await FindProblemAsync(1)).ToList();
            for (var i = 2; i < await FindMaxPageAsync(); i++)
            {
                problemIds.AddRange(await FindProblemAsync(i));
                await Task.Delay(200);
            }
            File.WriteAllText("problemset.json", JsonConvert.SerializeObject(problemIds));
            File.WriteAllText("return.json", JsonConvert.SerializeObject(new[] { "problemset.json" }));
        }

        private async Task<int> FindMaxPageAsync()
        {
            var response = await client.GetAsync(problemsetEndpoint.Replace("{PAGE}", "1"));
            var html = await response.Content.ReadAsStringAsync();
            var ret = new List<int>(100);
            foreach (Match x in PageIndexRegex.Matches(html))
            {
                ret.Add(int.Parse(x.Value));
            }
            return ret.Max();
        }

        private async Task<IEnumerable<int>> FindProblemAsync(int page)
        {
            var response = await client.GetAsync(problemsetEndpoint.Replace("{PAGE}", page.ToString()));
            var html = await response.Content.ReadAsStringAsync();
            var ret = new List<int>(100);
            foreach (Match x in ProblemIdRegex.Matches(html))
            {
                ret.Add(int.Parse(x.Value));
            }
            return ret;
        }
    }
}
