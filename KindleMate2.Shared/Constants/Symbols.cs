namespace KindleMate2.Shared.Constants {
    public static class Symbols {
        public const char Hyphen = '-';
        public const char ClosingParenthesis = ')';
        public const char ClosingParenthesisChinese = '）';
        public const char OpeningParenthesis = '(';
        public const char OpeningParenthesisChinese = '（';

        /// <summary>
        /// <c>My Clippings.txt</c> 中分隔条目的**规范**分隔符(Kindle 固定写 10 个 <c>=</c>)。
        /// 判定处按「整行 trim 后全为 <c>=</c> 且长度 ≥ 3」的宽松规则识别(容忍设备写出不同
        /// 数量的 <c>=</c>),本常量即该规则所指的规范值。
        /// </summary>
        public const string Delimiter = "==========";

        /// <summary>
        /// 书名与作者之间的分隔符:两侧带空格的连字符。只有这种形态才可安全拆分 ——
        /// 裸 <c>-</c> 会误伤书名内的连字符(如 <c>Spider-Man</c>)。
        /// </summary>
        public const string TitleAuthorSeparator = " - ";
    }
}