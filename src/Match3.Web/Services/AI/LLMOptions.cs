namespace Match3.Web.Services.AI
{
    /// <summary>
    /// LLM 配置选项
    /// </summary>
    public class LLMOptions
    {
        public const string SectionName = "LLM";

        /// <summary>
        /// 提供商类型：DeepSeek, OpenAI, Claude
        /// </summary>
        public string Provider { get; set; } = "DeepSeek";

        /// <summary>
        /// API 基础 URL
        /// </summary>
        public string BaseUrl { get; set; } = "https://api.deepseek.com/v1";

        /// <summary>
        /// API Key
        /// </summary>
        public string ApiKey { get; set; } = "";

        /// <summary>
        /// 模型名称
        /// </summary>
        public string Model { get; set; } = "deepseek-chat";

        /// <summary>
        /// 最大 Token 数
        /// </summary>
        public int MaxTokens { get; set; } = 2048;

        /// <summary>
        /// 温度参数 (0-2)
        /// </summary>
        public float Temperature { get; set; } = 0.7f;
    }
}
