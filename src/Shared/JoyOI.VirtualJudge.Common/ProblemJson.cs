using System.Collections.Generic;

namespace JoyOI.VirtualJudge
{
    public class ProblemJson
    {
        public string Id { get; set; }

        public string Body { get; set; }

        public string Source { get; set; }

        public string OriginUrl { get; set; }

        public int TimeLimitInMs { get; set; }

        public int MemoryLimitInByte { get; set; }

        public Dictionary<string, string> CodeTemplate { get; set; } = new Dictionary<string, string>();
    }
}
