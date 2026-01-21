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
        /// 模型名称（支持 Function Calling）
        /// </summary>
        public string Model { get; set; } = "deepseek-chat";

        /// <summary>
        /// 推理模型名称（深度思考，如 deepseek-reasoner）
        /// 为空则禁用深度思考功能
        /// </summary>
        public string? ReasonerModel { get; set; }

        /// <summary>
        /// 推理模型最大 Token 数（限制 R1 输出长度以减少等待）
        /// </summary>
        public int ReasonerMaxTokens { get; set; } = 1024;

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
