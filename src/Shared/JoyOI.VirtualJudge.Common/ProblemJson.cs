﻿using System.Collections.Generic;

namespace JoyOI.VirtualJudge
{
    public class ProblemJson
    {
        /// <summary>
        /// 题目ID
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// 题面的markdown形式
        /// </summary>
        public string Body { get; set; }

        /// <summary>
        /// 题目来源
        /// </summary>
        public ProblemSource Source { get; set; }

        /// <summary>
        /// 爬取题面的URL
        /// </summary>
        public string OriginUrl { get; set; }

        /// <summary>
        /// 爬取的时间限制（毫秒）
        /// </summary>
        public int TimeLimitInMs { get; set; }

        /// <summary>
        /// 爬取的内存限制（字节）
        /// </summary>
        public int MemoryLimitInByte { get; set; }

        /// <summary>
        /// Key为支持的语言，Value为代码模板，Bzoj这种标准输入输出的填空白字符串，Leetcode这种，需要爬取各种语言的模板。
        /// </summary>
        public Dictionary<string, string> CodeTemplate { get; set; } = new Dictionary<string, string>();
    }
}