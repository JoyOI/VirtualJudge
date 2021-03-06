﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace JoyOI.VirtualJudge.LeetCode.Actor
{
    class LeetCodePullProblemSetActor
    {

        private class LeetCodeProblemPointer
        {
            // private string question__title { get; set; }
            public string question__title_slug { get; set; }
            // private int question_id { get; set; }
        }

        private class LeetCodeProblemWrapper
        {
           public bool paid_only { get; set; }
           public LeetCodeProblemPointer stat { get; set; }
        }

        private const string baseUrl = "https://leetcode.com";
        private const string allProblems = "/api/problems/all/";
        private static HttpClient client = new HttpClient() { BaseAddress = new Uri(baseUrl) };

        public static void Main()
        {
            MainAsync().Wait();
        }

        private static async Task MainAsync()
        {
            var retryLeftTimes = 3;
            main:
            try
            {
                var problemIds = (await FindProblemsAsync()).ToList();
                File.WriteAllText("problemset.json", JsonConvert.SerializeObject(problemIds));
                File.WriteAllText("return.json", JsonConvert.SerializeObject(new { Outputs = new[] { "problemset.json" } }));
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

        private static async Task<IEnumerable<string>> FindProblemsAsync()
        {
            using (var response = await client.GetAsync(allProblems))
            {
                var jsonStr = await response.Content.ReadAsStringAsync();
                var def = new
                {
                    stat_status_pairs = new List<LeetCodeProblemWrapper> { new LeetCodeProblemWrapper() }
                };
                var result = JsonConvert.DeserializeAnonymousType(jsonStr, def);
                return result.stat_status_pairs
                    .Where(x => !x.paid_only)
                    .Select(x => x.stat)
                    .Select(x => x.question__title_slug)
                    .ToArray();
            }
        }
    }
}
