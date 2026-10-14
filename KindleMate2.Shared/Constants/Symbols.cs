namespace KindleMate2.Shared.Constants {
    public static class Symbols {
        public const char Hyphen = '-';
        public const char ClosingParenthesis = ')';
        public const char ClosingParenthesisChinese = '）';
        public const char OpeningParenthesis = '(';
        public const char OpeningParenthesisChinese = '（';

        /// <summary>
        /// <c>My Clippings.txt</c> 中分隔条目的整行分隔符(Kindle 固定写 10 个 <c>=</c>)。
        /// 解析处必须按**整行精确相等**判定,不能依赖上下文行(正文可多行)。
        /// </summary>
        public const string Delimiter = "==========";

        /// <summary>
        /// 书名与作者之间的分隔符:两侧带空格的连字符。只有这种形态才可安全拆分 ——
        /// 裸 <c>-</c> 会误伤书名内的连字符(如 <c>Spider-Man</c>)。
        /// </summary>
        public const string TitleAuthorSeparator = " - ";
    }
}