using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace AIIntegration.AI
{
    #region API 配置模型

    /// <summary>
    /// AI API 配置
    /// 支持多种大模型服务提供商
    /// </summary>
    public class AIApiConfig
    {
        /// <summary>
        /// API 提供商类型
        /// </summary>
        public ApiProvider Provider { get; set; } = ApiProvider.OpenAI;

        /// <summary>
        /// API Key
        /// </summary>
        public string ApiKey { get; set; } = "";

        /// <summary>
        /// API Endpoint URL
        /// </summary>
        public string Endpoint { get; set; } = "https://api.openai.com/v1/chat/completions";

        /// <summary>
        /// 模型名称
        /// </summary>
        public string Model { get; set; } = "gpt-3.5-turbo";

        /// <summary>
        /// 请求超时时间 (秒)
        /// </summary>
        public int TimeoutSeconds { get; set; } = 30;

        /// <summary>
        /// 是否启用 API 调用
        /// </summary>
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// 是否仅用于优化建议（不用于实时调度）
        /// </summary>
        public bool OptimizationOnly { get; set; } = true;
    }

    /// <summary>
    /// API 提供商枚举
    /// </summary>
    public enum ApiProvider
    {
        OpenAI,      // OpenAI GPT
        Azure,       // Azure OpenAI
        Anthropic,   // Claude
        Custom       // 自定义兼容 API
    }

    #endregion

    #region API 请求/响应模型

    /// <summary>
    /// 通用聊天请求
    /// </summary>
    public class ChatRequest
    {
        public string Model { get; set; } = "";
        public List<ChatMessage> Messages { get; set; } = new();
        public float Temperature { get; set; } = 0.7f;
        public int MaxTokens { get; set; } = 1024;
    }

    /// <summary>
    /// 聊天消息
    /// </summary>
    public class ChatMessage
    {
        public string Role { get; set; } = "user"; // system, user, assistant
        public string Content { get; set; } = "";
    }

    /// <summary>
    /// 通用聊天响应
    /// </summary>
    public class ChatResponse
    {
        public string Content { get; set; } = "";
        public string Model { get; set; } = "";
        public UsageInfo Usage { get; set; } = new();
        public bool Success { get; set; } = true;
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// Token 使用信息
    /// </summary>
    public class UsageInfo
    {
        public int PromptTokens { get; set; }
        public int CompletionTokens { get; set; }
        public int TotalTokens { get; set; }
    }

    #endregion

    #region OpenAI 兼容 API 模型

    /// <summary>
    /// OpenAI 格式请求体
    /// </summary>
    public class OpenAIRequest
    {
        public string Model { get; set; } = "";
        public List<ChatMessage> Messages { get; set; } = new();
        public float Temperature { get; set; } = 0.7f;
        public int MaxTokens { get; set; } = 1024;
    }

    /// <summary>
    /// OpenAI 格式响应体
    /// </summary>
    public class OpenAIResponse
    {
        public string Id { get; set; } = "";
        public string Object { get; set; } = "";
        public long Created { get; set; }
        public string Model { get; set; } = "";
        public List<Choice> Choices { get; set; } = new();
        public UsageInfo Usage { get; set; } = new();
    }

    public class Choice
    {
        public int Index { get; set; }
        public ChatMessage Message { get; set; } = new();
        public string FinishReason { get; set; } = "";
    }

    #endregion

    #region API 客户端

    /// <summary>
    /// AI API 客户端
    /// 支持多种大模型服务提供商的统一接口
    /// </summary>
    public class AIApiClient
    {
        private readonly AIApiConfig _config;
        private readonly HttpClient _httpClient;
        private readonly JsonSerializerOptions _jsonOptions;

        public AIApiClient(AIApiConfig config)
        {
            _config = config;
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(config.TimeoutSeconds)
            };

            // 配置 JSON 选项
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            // 设置默认请求头
            SetupDefaultHeaders();
        }

        private void SetupDefaultHeaders()
        {
            _httpClient.DefaultRequestHeaders.Clear();

            // User-Agent
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "QuantumClassicAI/1.0");

            // Authorization
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_config.ApiKey}");

            // Content-Type
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
        }

        /// <summary>
        /// 发送聊天请求
        /// </summary>
        public async Task<ChatResponse> SendChatAsync(ChatRequest request)
        {
            try
            {
                var requestBody = CreateRequestBody(request);
                var jsonContent = JsonSerializer.Serialize(requestBody, _jsonOptions);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(_config.Endpoint, content);
                var responseJson = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    return new ChatResponse
                    {
                        Success = false,
                        ErrorMessage = $"HTTP {response.StatusCode}: {responseJson}"
                    };
                }

                // 解析响应
                var openAIResponse = JsonSerializer.Deserialize<OpenAIResponse>(responseJson, _jsonOptions);
                
                if (openAIResponse == null || openAIResponse.Choices.Count == 0)
                {
                    return new ChatResponse
                    {
                        Success = false,
                        ErrorMessage = "Empty or invalid response from API"
                    };
                }

                return new ChatResponse
                {
                    Success = true,
                    Content = openAIResponse.Choices[0].Message.Content,
                    Model = openAIResponse.Model,
                    Usage = openAIResponse.Usage
                };
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
            {
                return new ChatResponse
                {
                    Success = false,
                    ErrorMessage = "Request timeout"
                };
            }
            catch (Exception ex)
            {
                return new ChatResponse
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        /// <summary>
        /// 同步发送聊天请求（用于非异步场景）
        /// </summary>
        public ChatResponse SendChat(ChatRequest request)
        {
            return SendChatAsync(request).GetAwaiter().GetResult();
        }

        private OpenAIRequest CreateRequestBody(ChatRequest request)
        {
            return new OpenAIRequest
            {
                Model = string.IsNullOrEmpty(request.Model) ? _config.Model : request.Model,
                Messages = request.Messages,
                Temperature = request.Temperature,
                MaxTokens = request.MaxTokens
            };
        }

        /// <summary>
        /// 测试 API 连接
        /// </summary>
        public async Task<bool> TestConnectionAsync()
        {
            var testRequest = new ChatRequest
            {
                Messages = new List<ChatMessage>
                {
                    new ChatMessage { Role = "user", Content = "Hello, this is a test." }
                },
                MaxTokens = 10
            };

            var response = await SendChatAsync(testRequest);
            return response.Success;
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }

    #endregion

    #region 量子任务特定的 Prompt 构建器

    /// <summary>
    /// 量子任务调度专用的 Prompt 构建器
    /// </summary>
    public static class QuantumTaskPromptBuilder
    {
        /// <summary>
        /// 构建任务优先级评估的 Prompt
        /// </summary>
        public static string BuildPriorityPrompt(List<TaskInfoForAI> tasks)
        {
            var sb = new StringBuilder();
            sb.AppendLine("你是一个量子计算任务调度专家。请分析以下量子任务的优先级：");
            sb.AppendLine();
            sb.AppendLine("评估标准：");
            sb.AppendLine("1. T 门数量越多，优先级越高（关键资源）");
            sb.AppendLine("2. 等待时间越长，优先级越高（公平性）");
            sb.AppendLine("3. 电路深度影响执行时间");
            sb.AppendLine("4. Qubit 数量影响资源占用");
            sb.AppendLine();
            sb.AppendLine("任务列表：");
            
            foreach (var task in tasks)
            {
                sb.AppendLine($"- 任务{task.Id}: {task.Name}");
                sb.AppendLine($"  深度={task.Depth}, T 门={task.TGateCount}, Qubits={task.QubitCount}, 等待时间={task.WaitTime}s");
            }

            sb.AppendLine();
            sb.AppendLine("请返回每个任务的推荐优先级 (0-1 之间的小数)，格式：JSON {\"taskId\": priority}");

            return sb.ToString();
        }

        /// <summary>
        /// 构建优化建议的 Prompt
        /// </summary>
        public static string BuildOptimizationPrompt(CircuitInfo circuit)
        {
            var sb = new StringBuilder();
            sb.AppendLine("你是一个量子电路优化专家。请分析以下量子电路并提供优化建议：");
            sb.AppendLine();
            sb.AppendLine($"电路信息：");
            sb.AppendLine($"- 名称：{circuit.Name}");
            sb.AppendLine($"- 算法类型：{circuit.AlgorithmType}");
            sb.AppendLine($"- 深度：{circuit.Depth}");
            sb.AppendLine($"- T 门数量：{circuit.TGateCount}");
            sb.AppendLine($"- CNOT 数量：{circuit.CNOTCount}");
            sb.AppendLine($"- Qubit 数量：{circuit.QubitCount}");
            sb.AppendLine();
            sb.AppendLine("请提供：");
            sb.AppendLine("1. 可能的优化方案（如 T 门减少、并行化、门消除等）");
            sb.AppendLine("2. 每个优化的预期改进百分比");
            sb.AppendLine("3. 优化优先级排序");
            sb.AppendLine();
            sb.AppendLine("返回格式：JSON 数组 [{\"type\": \"...\", \"description\": \"...\", \"improvement\": 0.XX, \"priority\": \"High/Medium/Low\"}]");

            return sb.ToString();
        }

        /// <summary>
        /// 构建 SWAP 成本分析的 Prompt
        /// </summary>
        public static string BuildSWAPAnalysisPrompt(int srcQubit, int dstQubit, string topology)
        {
            var sb = new StringBuilder();
            sb.AppendLine("分析量子硬件拓扑上的 SWAP 操作成本：");
            sb.AppendLine();
            sb.AppendLine($"源 Qubit: {srcQubit}");
            sb.AppendLine($"目标 Qubit: {dstQubit}");
            sb.AppendLine($"硬件拓扑：{topology}");
            sb.AppendLine();
            sb.AppendLine("请估计需要的 SWAP 操作数量和相对成本倍数。");
            sb.AppendLine("返回格式：JSON {\"swapCount\": N, \"costMultiplier\": X.X}");

            return sb.ToString();
        }
    }

    /// <summary>
    /// 传递给 AI 的任务信息
    /// </summary>
    public record TaskInfoForAI(
        int Id,
        string Name,
        int Depth,
        int TGateCount,
        int QubitCount,
        float WaitTime
    );

    /// <summary>
    /// 电路信息
    /// </summary>
    public record CircuitInfo(
        string Name,
        string AlgorithmType,
        int Depth,
        int TGateCount,
        int CNOTCount,
        int QubitCount
    );

    #endregion
}
