using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AIIntegration.AI;
using SchedulerTask = AIIntegration.Scheduler.Task;

namespace AIIntegration.Scheduler
{
    // ==================== 基础数据模型 ====================

    public enum TaskPriority { Low, Normal, High, Critical }

    public enum OperationMode { Rule, AI, Hybrid, APIEnhanced }

    /// <summary>
    /// API 增强模式配置
    /// </summary>
    public class APIEnhancedConfig
    {
        /// <summary>
        /// 使用 API 进行优先级重校准
        /// </summary>
        public bool UseAPIForPriority { get; set; } = true;

        /// <summary>
        /// 使用 API 进行优化建议
        /// </summary>
        public bool UseAPIForOptimization { get; set; } = true;

        /// <summary>
        /// API 和本地模型的权重 (0-1)
        /// 1 = 完全信任 API, 0 = 完全信任本地模型
        /// </summary>
        public float APIWeight { get; set; } = 0.6f;

        /// <summary>
        /// 是否异步调用 API（不阻塞调度）
        /// </summary>
        public bool AsyncMode { get; set; } = true;

        /// <summary>
        /// API 调用超时时间 (毫秒)
        /// </summary>
        public int TimeoutMs { get; set; } = 5000;
    }

    public record CircuitBlock(
        string Name,
        int Depth,
        int TGateCount,
        int QubitCount
    );

    public record Task(
        int Id,
        string Name,
        CircuitBlock Circuit,
        TaskPriority Priority,
        float SubmitTime,
        TaskStatus Status = TaskStatus.Queued,
        float? ExecutionTime = null
    );

    public enum TaskStatus { Queued, Scheduled, Running, Completed, Failed }

    // ==================== AI 调度适配层 ====================

    /// <summary>
    /// Phase 3 核心：AISchedulerAdapter
    /// 连接 4 个 AI 模型和现有调度器
    /// 支持四种模式：Rule / AI / Hybrid / APIEnhanced
    /// </summary>
    public class AISchedulerAdapter
    {
        public OperationMode Mode { get; set; } = OperationMode.Hybrid;
        
        /// <summary>
        /// API 增强模式配置
        /// </summary>
        public APIEnhancedConfig? EnhancedConfig { get; set; }

        // AI 模型实例
        private readonly TaskPriorityPredictor _priorityPredictor;
        private readonly SWAPCostPredictor _swapCostPredictor;
        private readonly ResourcePredictor _resourcePredictor;
        private readonly FaultPredictor _faultPredictor;
        
        // API 服务（可选）
        private AIApiService? _apiService;
        
        // API 缓存结果（用于异步模式）
        private Dictionary<int, float> _cachedApiPriorities = new();
        private DateTime _cacheExpiration = DateTime.MinValue;

        // 算法权重（Hybrid 模式）
        private const float AIWeight = 0.70f;      // AI 贡献度 70%
        private const float RuleWeight = 0.30f;    // 规则贡献度 30%

        public AISchedulerAdapter()
        {
            _priorityPredictor = new TaskPriorityPredictor();
            _swapCostPredictor = new SWAPCostPredictor();
            _resourcePredictor = new ResourcePredictor();
            _faultPredictor = new FaultPredictor();
        }

        /// <summary>
        /// 设置 API 服务
        /// </summary>
        public void SetApiService(AIApiService apiService)
        {
            _apiService = apiService;
        }

        /// <summary>
        /// 初始化 API 服务从配置
        /// </summary>
        public void InitializeApiFromConfig(AIApiConfig config)
        {
            if (config.Enabled)
            {
                _apiService = new AIApiService(config);
                Console.WriteLine($"[AIScheduler] API 服务已初始化：{config.Provider} - {config.Model}");
            }
        }

        /// <summary>
        /// Step 2 核心：计算任务综合评分
        /// 结合 AI 模型和规则引擎
        /// 优化：移除不必要的 try-catch，使用提前验证
        /// </summary>
        public float ComputeTaskScore(Task task, float currentTime)
        {
            // 提前验证任务有效性，避免异常处理开销
            if (!IsValidTask(task))
            {
                return ComputeRuleBasedScore(task, currentTime);
            }

            // 特征提取
            var features = new TaskFeatures(
                Depth: task.Circuit.Depth,
                TGateCount: task.Circuit.TGateCount,
                QubitCount: task.Circuit.QubitCount,
                WaitTime: currentTime - task.SubmitTime,
                PastPriority: (float)task.Priority / 3f
            );

            // AI 评分
            var aiScore = _priorityPredictor.Predict(features);

            // 规则评分
            var ruleScore = ComputeRuleBasedScore(task, currentTime);

            // 模式选择
            return Mode switch
            {
                OperationMode.Rule => ruleScore,
                OperationMode.AI => aiScore,
                OperationMode.Hybrid => AIWeight * aiScore + RuleWeight * ruleScore,
                OperationMode.APIEnhanced => ComputeAPIEnhancedScore(task, currentTime, features, aiScore, ruleScore),
                _ => ruleScore
            };
        }

        /// <summary>
        /// 计算 API 增强的评分
        /// </summary>
        private float ComputeAPIEnhancedScore(Task task, float currentTime, TaskFeatures features, 
                                               float localAiScore, float ruleScore)
        {
            if (_apiService == null || !(_apiService.IsAvailable))
            {
                // API 不可用时降级到 Hybrid 模式
                Console.WriteLine("[AIScheduler] API 不可用，降级到 Hybrid 模式");
                return AIWeight * localAiScore + RuleWeight * ruleScore;
            }

            // 检查缓存
            if (!IsCacheValid())
            {
                RefreshApiCache(task);
            }

            // 获取 API 评分
            if (_cachedApiPriorities.TryGetValue(task.Id, out var apiScore))
            {
                var config = EnhancedConfig ?? new APIEnhancedConfig();
                
                // 混合 API 和本地评分
                var combinedScore = config.APIWeight * apiScore + (1 - config.APIWeight) * localAiScore;
                
                // 再与规则评分混合
                return 0.7f * combinedScore + 0.3f * ruleScore;
            }

            // 没有 API 数据时使用本地 AI
            return AIWeight * localAiScore + RuleWeight * ruleScore;
        }

        /// <summary>
        /// 检查缓存是否有效
        /// </summary>
        private bool IsCacheValid()
        {
            return DateTime.Now < _cacheExpiration && _cachedApiPriorities.Count > 0;
        }

        /// <summary>
        /// 刷新 API 缓存
        /// </summary>
        private void RefreshApiCache(Task currentTask)
        {
            if (_apiService == null || !_apiService.IsAvailable)
                return;

            try
            {
                // 这里可以扩展为传入任务列表
                // 为了简化，仅对当前任务进行评估
                var taskInfo = new TaskInfoForAI(
                    currentTask.Id,
                    currentTask.Name,
                    currentTask.Circuit.Depth,
                    currentTask.Circuit.TGateCount,
                    currentTask.Circuit.QubitCount,
                    0
                );

                var priorities = _apiService.EvaluateTaskPriorities(new List<TaskInfoForAI> { taskInfo });
                
                _cachedApiPriorities = priorities;
                _cacheExpiration = DateTime.Now.AddMinutes(5);  // 5 分钟缓存
                
                Console.WriteLine($"[AIScheduler] API 缓存已更新，任务数：{priorities.Count}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AIScheduler] 刷新 API 缓存失败：{ex.Message}");
            }
        }

        /// <summary>
        /// 异步刷新任务优先级缓存
        /// </summary>
        public async System.Threading.Tasks.Task RefreshPrioritiesAsync(List<SchedulerTask> tasks)
        {
            if (_apiService == null || !_apiService.IsAvailable)
                return;

            try
            {
                var taskInfos = tasks.Select(t => new TaskInfoForAI(
                    t.Id,
                    t.Name,
                    t.Circuit.Depth,
                    t.Circuit.TGateCount,
                    t.Circuit.QubitCount,
                    0
                )).ToList();

                var priorities = await _apiService.EvaluateTaskPrioritiesAsync(taskInfos);
                
                _cachedApiPriorities = priorities;
                _cacheExpiration = DateTime.Now.AddMinutes(10);
                
                Console.WriteLine($"[AIScheduler] 异步 API 缓存已更新，任务数：{priorities.Count}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AIScheduler] 异步刷新失败：{ex.Message}");
            }
        }

        /// <summary>
        /// 验证任务是否有效
        /// </summary>
        private static bool IsValidTask(Task task)
        {
            return task != null
                && task.Circuit != null
                && task.Circuit.Depth >= 0
                && task.Circuit.TGateCount >= 0
                && task.Circuit.QubitCount >= 0;
        }

        /// <summary>
        /// 规则引擎评分（向后兼容）
        /// 优先级 50% + 等待时间 30% + 复杂度 20%
        /// </summary>
        private float ComputeRuleBasedScore(Task task, float currentTime)
        {
            var priorityScore = task.Priority switch
            {
                TaskPriority.Critical => 1.0f,
                TaskPriority.High => 0.7f,
                TaskPriority.Normal => 0.5f,
                TaskPriority.Low => 0.3f,
                _ => 0.5f
            };

            var waitScore = Math.Min((currentTime - task.SubmitTime) / 100f, 1.0f);
            var complexityScore = Math.Min(task.Circuit.TGateCount / 50f, 1.0f);

            return 0.5f * priorityScore + 0.3f * waitScore + 0.2f * complexityScore;
        }

        // ==================== SWAP 成本适配 ====================

        /// <summary>
        /// 估计两个 qubit 间的 SWAP 成本
        /// 基于学习的拓扑特性
        /// </summary>
        public float EstimateSWAPCost(int srcQubit, int dstQubit)
        {
            return _swapCostPredictor.Predict(srcQubit, dstQubit);
        }

        /// <summary>
        /// 记录实际观测的 SWAP 成本
        /// 用于在线优化
        /// </summary>
        public void ObserveSWAPCost(int srcQubit, int dstQubit, float actualCost)
        {
            _swapCostPredictor.ObserveActualCost(srcQubit, dstQubit, actualCost);
        }

        // ==================== 故障检测 ====================

        /// <summary>
        /// 检查 qubit 是否存在故障风险
        /// 阈值：0.7（70% 风险以上视为高风险）
        /// </summary>
        public bool IsRiskyQubit(int qubitId, float threshold = 0.7f)
        {
            return _faultPredictor.PredictRisk(qubitId) > threshold;
        }

        /// <summary>
        /// 获取所有高风险 qubits
        /// </summary>
        public List<int> GetRiskyQubits(float threshold = 0.7f)
        {
            return _faultPredictor.GetRiskyQubits(threshold);
        }

        /// <summary>
        /// 记录 qubit 故障
        /// </summary>
        public void RecordQubitFailure(int qubitId)
        {
            _faultPredictor.RecordFailure(qubitId);
        }

        /// <summary>
        /// 记录 qubit 成功使用
        /// </summary>
        public void RecordQubitUsage(int qubitId)
        {
            _faultPredictor.RecordUsage(qubitId);
        }

        // ==================== 资源预测 ====================

        /// <summary>
        /// 预测任务执行时间
        /// </summary>
        public float PredictExecutionTime(Task task, int systemLoad)
        {
            return _resourcePredictor.PredictExecutionTime(
                task.Circuit.Depth,
                task.Circuit.TGateCount,
                systemLoad
            );
        }

        /// <summary>
        /// 记录任务执行时间（用于学习）
        /// </summary>
        public void RecordTaskExecution(Task task, float actualTime)
        {
            _resourcePredictor.RecordExecution(
                task.Circuit.Depth,
                task.Circuit.TGateCount,
                task.Circuit.QubitCount,
                actualTime
            );
        }

        // ==================== 在线学习 ====================

        /// <summary>
        /// 从任务执行反馈中学习
        /// 更新 AI 模型权重
        /// </summary>
        public void LearnFromExecution(Task task, float actualExecutionTime,
                                       float actualPriority, int systemLoad)
        {
            var features = new TaskFeatures(
                Depth: task.Circuit.Depth,
                TGateCount: task.Circuit.TGateCount,
                QubitCount: task.Circuit.QubitCount,
                WaitTime: actualExecutionTime,
                PastPriority: (float)task.Priority / 3f
            );

            _priorityPredictor.Learn(features, actualPriority, learningRate: 0.01f);
            _resourcePredictor.RecordExecution(
                task.Circuit.Depth,
                task.Circuit.TGateCount,
                task.Circuit.QubitCount,
                actualExecutionTime
            );
        }

        // ==================== API 增强功能 ====================

        /// <summary>
        /// 获取电路优化建议（通过 API）
        /// </summary>
        public async Task<List<OptimizationRecommendation>> GetOptimizationSuggestionsAsync(
            string circuitName, string algorithmType, CircuitBlock circuit)
        {
            if (_apiService == null || !_apiService.IsAvailable)
            {
                return new List<OptimizationRecommendation>();
            }

            var circuitInfo = new CircuitInfo(
                circuitName,
                algorithmType,
                circuit.Depth,
                circuit.TGateCount,
                0,  // CNOT count not available in CircuitBlock
                circuit.QubitCount
            );

            return await _apiService.GetOptimizationSuggestionsAsync(circuitInfo);
        }

        /// <summary>
        /// 分析 SWAP 成本（通过 API）
        /// </summary>
        public async Task<(int swapCount, float costMultiplier)> AnalyzeSWAPCostWithAIAsync(
            int srcQubit, int dstQubit, string topology = "linear")
        {
            if (_apiService == null || !_apiService.IsAvailable)
            {
                // 降级到本地模型
                var cost = _swapCostPredictor.Predict(srcQubit, dstQubit);
                return (Math.Abs(dstQubit - srcQubit), cost);
            }

            return await _apiService.AnalyzeSWAPCostAsync(srcQubit, dstQubit, topology);
        }

        // ==================== 诊断信息 ====================

        public void PrintDiagnostics()
        {
            Console.WriteLine("\n=== AI 模型诊断 ===");
            Console.WriteLine($"运行模式：{Mode}");
            Console.WriteLine($"API 服务状态：{(_apiService?.IsAvailable ?? false ? "已启用" : "未启用")}");
            Console.WriteLine($"优先级预测器训练数据：{_priorityPredictor.TrainingDataCount}条");
            Console.WriteLine($"SWAP 成本观测对数：{_swapCostPredictor.ObservedPairs}对");
            Console.WriteLine($"资源预测历史：{_resourcePredictor.HistorySize}条记录");
            Console.WriteLine($"故障监控 qubits: {_faultPredictor.MonitoredQubitCount}个");
            Console.WriteLine($"API 缓存任务数：{_cachedApiPriorities.Count}");
        }
    }
}
