using System.Collections.Generic;
using System;
using System.Linq;

namespace OnionCollections.DesignDataAgent
{
    internal static class Utilities
    {
        public static string CutString(string str, int left, int right)
        {
            if (str.Length < left + right)
                throw new Exception("字串頭尾剪取量大於字串長度。");

            return str.Substring(left, str.Length - left - right);
        }

        public static bool IsStartWith(string str, string head)
        {
            if (str.Length < head.Length)
                return false;

            return str.Substring(0, head.Length) == head;
        }
        public static bool IsStartWith(string str, char[] heads)
        {
            return heads.Contains(str[0]);
        }

        public static bool IsEndWith(string str, string foot)
        {
            if (str.Length < foot.Length)
                return false;
            return str.Substring(str.Length - foot.Length) == foot;
        }
        public static bool IsEndWith(string str, char[] foots)
        {
            return foots.Contains(str.Last());
        }

        public static bool IsPinchWith(string str, char[] signs)
        {
            return IsStartWith(str, signs) && IsEndWith(str, signs);
        }

        public static string GetPathString(IEnumerable<string> path)
        {
            return string.Join(".", path).Replace(".[", "[");
        }
    }
}