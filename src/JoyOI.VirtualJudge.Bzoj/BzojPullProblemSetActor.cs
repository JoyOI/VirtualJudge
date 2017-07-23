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
        private const string baseUrl = "http://www.lydsy.com";
        private const string problemsetEndpoint = "/JudgeOnline/problemset.php?page={PAGE}";
        private Regex ProblemIdRegex = new Regex("(?<=<a href='problem.php\\?id=)[0-9]{4,6}(?='>)");
        private Regex PageIndexRegex = new Regex("(?<=<a href='problemset.php\\?page=)[0-9]{1,}(?='>[0-9]{1,}</a>)");
        private int page = 1;
        private HttpClient client = new HttpClient() { BaseAddress = new Uri(baseUrl) };

        public void Main()
        {
            MainAsync().Wait();
        }

        private async Task MainAsync()
        {
            var retryLeftTimes = 3;
            main:
            try
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
            catch (Exception ex)
            {
                --retryLeftTimes;
                if (retryLeftTimes <= 0)
                {
                    File.WriteAllText("error.txt", ex.ToString());
                    File.WriteAllText("return.json", JsonConvert.SerializeObject(new[] { "error.txt" }));
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

        private async Task<int> FindMaxPageAsync()
        {
            var retryLeftTimes = 3;
            findMaxPage:
            try
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
            catch
            {
                --retryLeftTimes;
                if (retryLeftTimes <= 0)
                {
                    throw;
                }
                else
                {
                    await Task.Delay(1000);
                    goto findMaxPage;
                }
            }
        }

        private async Task<IEnumerable<int>> FindProblemAsync(int page)
        {
            var retryLeftTimes = 3;
            findProblem:
            try
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
            catch
            {

                --retryLeftTimes;
                if (retryLeftTimes <= 0)
                {
                    throw;
                }
                else
                {
                    await Task.Delay(1000);
                    goto findProblem;
                }
            }
        }
    }
}
