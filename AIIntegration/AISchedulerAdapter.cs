using System;
using System.Collections.Generic;
using System.Linq;
using AIIntegration.AI;

namespace AIIntegration.Scheduler
{
    // ==================== 基础数据模型 ====================
    
    public enum TaskPriority { Low, Normal, High, Critical }
    
    public enum OperationMode { Rule, AI, Hybrid }
    
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
    
    // ==================== AI调度适配层 ====================
    
    /// <summary>
    /// Phase 3核心：AISchedulerAdapter
    /// 连接4个AI模型和现有调度器
    /// 支持三种模式：Rule / AI / Hybrid
    /// </summary>
    public class AISchedulerAdapter
    {
        public OperationMode Mode { get; set; } = OperationMode.Hybrid;
        
        // AI模型实例
        private readonly TaskPriorityPredictor _priorityPredictor;
        private readonly SWAPCostPredictor _swapCostPredictor;
        private readonly ResourcePredictor _resourcePredictor;
        private readonly FaultPredictor _faultPredictor;
        
        // 算法权重（Hybrid模式）
        private const float AIWeight = 0.70f;      // AI贡献度70%
        private const float RuleWeight = 0.30f;    // 规则贡献度30%
        
        public AISchedulerAdapter()
        {
            _priorityPredictor = new TaskPriorityPredictor();
            _swapCostPredictor = new SWAPCostPredictor();
            _resourcePredictor = new ResourcePredictor();
            _faultPredictor = new FaultPredictor();
        }
        
        /// <summary>
        /// Step 2核心：计算任务综合评分
        /// 结合AI模型和规则引擎
        /// 优化：移除不必要的try-catch，使用提前验证
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
            
            // AI评分
            var aiScore = _priorityPredictor.Predict(features);
            
            // 规则评分
            var ruleScore = ComputeRuleBasedScore(task, currentTime);
            
            // 模式选择
            return Mode switch
            {
                OperationMode.Rule => ruleScore,
                OperationMode.AI => aiScore,
                OperationMode.Hybrid => AIWeight * aiScore + RuleWeight * ruleScore,
                _ => ruleScore
            };
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
        
        // ==================== SWAP成本适配 ====================
        
        /// <summary>
        /// 估计两个qubit间的SWAP成本
        /// 基于学习的拓扑特性
        /// </summary>
        public float EstimateSWAPCost(int srcQubit, int dstQubit)
        {
            return _swapCostPredictor.Predict(srcQubit, dstQubit);
        }
        
        /// <summary>
        /// 记录实际观测的SWAP成本
        /// 用于在线优化
        /// </summary>
        public void ObserveSWAPCost(int srcQubit, int dstQubit, float actualCost)
        {
            _swapCostPredictor.ObserveActualCost(srcQubit, dstQubit, actualCost);
        }
        
        // ==================== 故障检测 ====================
        
        /// <summary>
        /// 检查qubit是否存在故障风险
        /// 阈值：0.7（70%风险以上视为高风险）
        /// </summary>
        public bool IsRiskyQubit(int qubitId, float threshold = 0.7f)
        {
            return _faultPredictor.PredictRisk(qubitId) > threshold;
        }
        
        /// <summary>
        /// 获取所有高风险qubits
        /// </summary>
        public List<int> GetRiskyQubits(float threshold = 0.7f)
        {
            return _faultPredictor.GetRiskyQubits(threshold);
        }
        
        /// <summary>
        /// 记录qubit故障
        /// </summary>
        public void RecordQubitFailure(int qubitId)
        {
            _faultPredictor.RecordFailure(qubitId);
        }
        
        /// <summary>
        /// 记录qubit成功使用
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
        /// 更新AI模型权重
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
        
        // ==================== 诊断信息 ====================
        
        public void PrintDiagnostics()
        {
            Console.WriteLine("\n=== AI模型诊断 ===");
            Console.WriteLine($"运行模式: {Mode}");
            Console.WriteLine($"优先级预测器训练数据: {_priorityPredictor.TrainingDataCount}条");
            Console.WriteLine($"SWAP成本观测对数: {_swapCostPredictor.ObservedPairs}对");
            Console.WriteLine($"资源预测历史: {_resourcePredictor.HistorySize}条记录");
            Console.WriteLine($"故障监控qubits: {_faultPredictor.MonitoredQubitCount}个");
        }
    }
}
