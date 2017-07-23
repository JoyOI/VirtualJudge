﻿using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Net.Http;
using System.IO;
using Newtonsoft.Json;

namespace JoyOI.VirtualJudge.Bzoj.Actor
{
    public class BzojPullProblemBodyActor
    {
        private const string baseUrl = "http://www.lydsy.com";
        private const string problemEndpoint = "/JudgeOnline/problem.php?id=";
        private Dictionary<Guid, string> imageDictionary = new Dictionary<Guid, string>();
        private List<string> returnFiles = new List<string>(50) { "problemset.json", "image.json" };
        private HttpClient client = new HttpClient() { BaseAddress = new Uri(baseUrl) };

        public void Main()
        {
            MainAsync(Convert.ToInt32(File.ReadAllText("id.txt"))).Wait();
        }

        private async Task MainAsync(int problemId)
        {
            var retryLeftTimes = 3;
            main:
            try
            {
                File.WriteAllText("problem.json", JsonConvert.SerializeObject(await GetProblemBodyAsync(problemId)));
                File.WriteAllText("image.json", JsonConvert.SerializeObject(imageDictionary));
                File.WriteAllText("return.json", JsonConvert.SerializeObject(returnFiles));
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

        private async Task<ProblemJson> GetProblemBodyAsync(int problemId)
        {
            throw new NotImplementedException();
        }
    }
}
