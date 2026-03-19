using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace AIIntegration.AI
{
    /// <summary>
    /// AI API 服务层
    /// 提供高级的量子任务调度 AI 功能
    /// </summary>
    public class AIApiService : IDisposable
    {
        private readonly AIApiClient _client;
        private readonly AIApiConfig _config;
        private bool _isAvailable;

        public AIApiService(AIApiConfig config)
        {
            _config = config;
            _client = new AIApiClient(config);
            _isAvailable = config.Enabled;
        }

        /// <summary>
        /// 检查 API 服务是否可用
        /// </summary>
        public bool IsAvailable => _isAvailable && _config.Enabled;

        /// <summary>
        /// 测试 API 连接状态
        /// </summary>
        public async Task<bool> TestConnectionAsync()
        {
            if (!_config.Enabled)
            {
                Console.WriteLine("[API] 服务未启用");
                return false;
            }

            Console.WriteLine("[API] 测试连接...");
            var result = await _client.TestConnectionAsync();
            
            if (result)
            {
                Console.WriteLine($"[API] 连接成功 - 提供商：{_config.Provider}, 模型：{_config.Model}");
                _isAvailable = true;
            }
            else
            {
                Console.WriteLine("[API] 连接失败");
                _isAvailable = false;
            }

            return result;
        }

        #region 任务优先级评估

        /// <summary>
        /// 使用 AI 评估任务优先级
        /// </summary>
        public async Task<Dictionary<int, float>> EvaluateTaskPrioritiesAsync(List<TaskInfoForAI> tasks)
        {
            if (!IsAvailable)
            {
                Console.WriteLine("[API] 服务不可用，返回空结果");
                return new Dictionary<int, float>();
            }

            try
            {
                var prompt = QuantumTaskPromptBuilder.BuildPriorityPrompt(tasks);
                var request = new ChatRequest
                {
                    Messages = new List<ChatMessage>
                    {
                        new ChatMessage { Role = "system", Content = "你是一个量子计算任务调度专家。请严格按照要求的 JSON 格式返回结果。" },
                        new ChatMessage { Role = "user", Content = prompt }
                    },
                    Temperature = 0.3f,  // 较低温度以获得更一致的结果
                    MaxTokens = 500
                };

                Console.WriteLine($"[API] 发送任务优先级评估请求，任务数：{tasks.Count}");
                var response = await _client.SendChatAsync(request);

                if (!response.Success)
                {
                    Console.WriteLine($"[API] 评估失败：{response.ErrorMessage}");
                    return new Dictionary<int, float>();
                }

                Console.WriteLine($"[API] 收到响应，模型：{response.Model}, Token 使用：{response.Usage.TotalTokens}");

                // 解析 JSON 响应
                return ParsePriorityResponse(response.Content, tasks);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[API] 评估异常：{ex.Message}");
                return new Dictionary<int, float>();
            }
        }

        /// <summary>
        /// 同步版本的任务优先级评估
        /// </summary>
        public Dictionary<int, float> EvaluateTaskPriorities(List<TaskInfoForAI> tasks)
        {
            return EvaluateTaskPrioritiesAsync(tasks).GetAwaiter().GetResult();
        }

        private Dictionary<int, float> ParsePriorityResponse(string content, List<TaskInfoForAI> tasks)
        {
            try
            {
                // 尝试提取 JSON 部分
                var jsonStart = content.IndexOf('{');
                var jsonEnd = content.LastIndexOf('}');
                
                if (jsonStart >= 0 && jsonEnd > jsonStart)
                {
                    var json = content.Substring(jsonStart, jsonEnd - jsonStart + 1);
                    var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;

                    var result = new Dictionary<int, float>();
                    
                    foreach (var task in tasks)
                    {
                        if (root.TryGetProperty(task.Id.ToString(), out var priorityElem))
                        {
                            result[task.Id] = priorityElem.GetSingle();
                        }
                        else if (root.TryGetProperty($"task{task.Id}", out priorityElem))
                        {
                            result[task.Id] = priorityElem.GetSingle();
                        }
                        else
                        {
                            // 如果 API 没有返回，使用默认值
                            result[task.Id] = 0.5f;
                        }
                    }

                    return result;
                }

                // 如果无法解析，返回默认值
                Console.WriteLine("[API] 无法解析响应，使用默认优先级");
                return tasks.ToDictionary(t => t.Id, t => 0.5f);
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"[API] JSON 解析错误：{ex.Message}");
                Console.WriteLine($"[API] 原始响应：{content}");
                return tasks.ToDictionary(t => t.Id, t => 0.5f);
            }
        }

        #endregion

        #region 电路优化建议

        /// <summary>
        /// 获取量子电路优化建议
        /// </summary>
        public async Task<List<OptimizationRecommendation>> GetOptimizationSuggestionsAsync(CircuitInfo circuit)
        {
            if (!IsAvailable)
            {
                return new List<OptimizationRecommendation>();
            }

            try
            {
                var prompt = QuantumTaskPromptBuilder.BuildOptimizationPrompt(circuit);
                var request = new ChatRequest
                {
                    Messages = new List<ChatMessage>
                    {
                        new ChatMessage { Role = "system", Content = "你是一个量子电路优化专家。请严格按照要求的 JSON 数组格式返回结果。" },
                        new ChatMessage { Role = "user", Content = prompt }
                    },
                    Temperature = 0.5f,
                    MaxTokens = 1000
                };

                Console.WriteLine($"[API] 请求电路优化建议：{circuit.Name}");
                var response = await _client.SendChatAsync(request);

                if (!response.Success)
                {
                    Console.WriteLine($"[API] 优化建议获取失败：{response.ErrorMessage}");
                    return new List<OptimizationRecommendation>();
                }

                return ParseOptimizationResponse(response.Content);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[API] 优化建议异常：{ex.Message}");
                return new List<OptimizationRecommendation>();
            }
        }

        /// <summary>
        /// 同步版本的电路优化建议
        /// </summary>
        public List<OptimizationRecommendation> GetOptimizationSuggestions(CircuitInfo circuit)
        {
            return GetOptimizationSuggestionsAsync(circuit).GetAwaiter().GetResult();
        }

        private List<OptimizationRecommendation> ParseOptimizationResponse(string content)
        {
            try
            {
                // 尝试提取 JSON 数组
                var jsonStart = content.IndexOf('[');
                var jsonEnd = content.LastIndexOf(']');

                if (jsonStart >= 0 && jsonEnd > jsonStart)
                {
                    var json = content.Substring(jsonStart, jsonEnd - jsonStart + 1);
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var recommendations = JsonSerializer.Deserialize<List<OptimizationRecommendation>>(json, options);
                    
                    return recommendations ?? new List<OptimizationRecommendation>();
                }

                Console.WriteLine("[API] 无法解析优化响应");
                return new List<OptimizationRecommendation>();
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"[API] JSON 解析错误：{ex.Message}");
                return new List<OptimizationRecommendation>();
            }
        }

        #endregion

        #region SWAP 成本分析

        /// <summary>
        /// 分析 SWAP 操作成本
        /// </summary>
        public async Task<(int swapCount, float costMultiplier)> AnalyzeSWAPCostAsync(
            int srcQubit, int dstQubit, string topology = "linear")
        {
            if (!IsAvailable)
            {
                // 返回默认估计
                var distance = Math.Abs(dstQubit - srcQubit);
                return (distance, (float)Math.Pow(1.3, distance - 1));
            }

            try
            {
                var prompt = QuantumTaskPromptBuilder.BuildSWAPAnalysisPrompt(srcQubit, dstQubit, topology);
                var request = new ChatRequest
                {
                    Messages = new List<ChatMessage>
                    {
                        new ChatMessage { Role = "system", Content = "请严格按照要求的 JSON 格式返回结果。" },
                        new ChatMessage { Role = "user", Content = prompt }
                    },
                    Temperature = 0.3f,
                    MaxTokens = 200
                };

                var response = await _client.SendChatAsync(request);

                if (!response.Success)
                {
                    return GetDefaultSWAPCost(srcQubit, dstQubit);
                }

                return ParseSWAPResponse(response.Content, srcQubit, dstQubit);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[API] SWAP 分析异常：{ex.Message}");
                return GetDefaultSWAPCost(srcQubit, dstQubit);
            }
        }

        private (int, float) GetDefaultSWAPCost(int srcQubit, int dstQubit)
        {
            var distance = Math.Abs(dstQubit - srcQubit);
            return (distance, (float)Math.Pow(1.3, distance - 1));
        }

        private (int, float) ParseSWAPResponse(string content, int srcQubit, int dstQubit)
        {
            try
            {
                var jsonStart = content.IndexOf('{');
                var jsonEnd = content.LastIndexOf('}');

                if (jsonStart >= 0 && jsonEnd > jsonStart)
                {
                    var json = content.Substring(jsonStart, jsonEnd - jsonStart + 1);
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;

                    var swapCount = root.TryGetProperty("swapCount", out var sc) ? sc.GetInt32() : Math.Abs(dstQubit - srcQubit);
                    var costMultiplier = root.TryGetProperty("costMultiplier", out var cm) ? cm.GetSingle() : 1.0f;

                    return (swapCount, costMultiplier);
                }

                return GetDefaultSWAPCost(srcQubit, dstQubit);
            }
            catch (JsonException)
            {
                return GetDefaultSWAPCost(srcQubit, dstQubit);
            }
        }

        #endregion

        #region 智能调度决策

        /// <summary>
        /// 获取智能调度决策建议
        /// </summary>
        public async Task<SchedulingDecision> GetSchedulingDecisionAsync(SchedulingContext context)
        {
            if (!IsAvailable)
            {
                return CreateDefaultSchedulingDecision(context);
            }

            try
            {
                var prompt = BuildSchedulingPrompt(context);
                var request = new ChatRequest
                {
                    Messages = new List<ChatMessage>
                    {
                        new ChatMessage { 
                            Role = "system", 
                            Content = "你是量子计算任务调度专家。请分析并返回 JSON 格式的调度决策。" 
                        },
                        new ChatMessage { Role = "user", Content = prompt }
                    },
                    Temperature = 0.4f,
                    MaxTokens = 800
                };

                var response = await _client.SendChatAsync(request);

                if (!response.Success)
                {
                    return CreateDefaultSchedulingDecision(context);
                }

                return ParseSchedulingResponse(response.Content, context);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[API] 调度决策异常：{ex.Message}");
                return CreateDefaultSchedulingDecision(context);
            }
        }

        private string BuildSchedulingPrompt(SchedulingContext context)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("量子任务调度决策：");
            sb.AppendLine();
            sb.AppendLine($"系统状态：");
            sb.AppendLine($"- 可用 Qubits: {context.AvailableQubits}/{context.TotalQubits}");
            sb.AppendLine($"- 系统负载：{context.SystemLoad * 100:F0}%");
            sb.AppendLine($"- 当前时间：{context.CurrentTime:F2}s");
            sb.AppendLine();
            sb.AppendLine("待调度任务：");
            
            foreach (var task in context.PendingTasks)
            {
                sb.AppendLine($"- [{task.Id}] {task.Name}: 深度={task.Depth}, T 门={task.TGateCount}, Qubits={task.QubitCount}, 等待={task.WaitTime:F1}s");
            }

            sb.AppendLine();
            sb.AppendLine("高风险 Qubits: " + (context.RiskyQubits.Any() ? string.Join(", ", context.RiskyQubits) : "无"));
            sb.AppendLine();
            sb.AppendLine("请返回调度决策，格式：JSON {\"selectedTaskId\": N, \"reason\": \"...\", \"allocatedQubits\": [...], \"confidence\": 0.XX}");

            return sb.ToString();
        }

        private SchedulingDecision CreateDefaultSchedulingDecision(SchedulingContext context)
        {
            // 默认选择优先级最高的任务
            var selectedTask = context.PendingTasks.FirstOrDefault();
            
            return new SchedulingDecision
            {
                SelectedTaskId = selectedTask?.Id ?? -1,
                Reason = "默认调度策略",
                AllocatedQubits = new List<int>(),
                Confidence = 0.5f
            };
        }

        private SchedulingDecision ParseSchedulingResponse(string content, SchedulingContext context)
        {
            try
            {
                var jsonStart = content.IndexOf('{');
                var jsonEnd = content.LastIndexOf('}');

                if (jsonStart >= 0 && jsonEnd > jsonStart)
                {
                    var json = content.Substring(jsonStart, jsonEnd - jsonStart + 1);
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;

                    var decision = new SchedulingDecision
                    {
                        SelectedTaskId = root.TryGetProperty("selectedTaskId", out var id) ? id.GetInt32() : -1,
                        Reason = root.TryGetProperty("reason", out var reason) ? reason.GetString()! : "",
                        Confidence = root.TryGetProperty("confidence", out var conf) ? conf.GetSingle() : 0.5f
                    };

                    if (root.TryGetProperty("allocatedQubits", out var qubits))
                    {
                        decision.AllocatedQubits = qubits.EnumerateArray().Select(q => q.GetInt32()).ToList();
                    }

                    return decision;
                }

                return CreateDefaultSchedulingDecision(context);
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"[API] 调度决策解析错误：{ex.Message}");
                return CreateDefaultSchedulingDecision(context);
            }
        }

        #endregion

        public void Dispose()
        {
            _client?.Dispose();
        }
    }

    #region 调度决策相关模型

    /// <summary>
    /// 调度上下文
    /// </summary>
    public record SchedulingContext(
        int TotalQubits,
        int AvailableQubits,
        float SystemLoad,
        float CurrentTime,
        List<TaskInfoForAI> PendingTasks,
        List<int> RiskyQubits
    );

    /// <summary>
    /// 调度决策结果
    /// </summary>
    public class SchedulingDecision
    {
        public int SelectedTaskId { get; set; }
        public string Reason { get; set; } = "";
        public List<int> AllocatedQubits { get; set; } = new();
        public float Confidence { get; set; }
    }

    #endregion
}
