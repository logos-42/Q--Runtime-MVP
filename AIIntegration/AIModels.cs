using System;
using System.Collections.Generic;
using System.Linq;

namespace AIIntegration.AI
{
    /// <summary>
    /// Phase 3: AI模型组件 (4个预测器)
    /// 用于智能任务调度和资源优化
    /// </summary>
    
    #region 数据模型
    
    public record TaskFeatures(
        float Depth,
        int TGateCount,
        int QubitCount,
        float WaitTime,
        float PastPriority
    );
    
    public record QubitMetrics(
        int QubitId,
        float ErrorRate,
        float FailureRate,
        float RecentUsageFrequency
    );
    
    #endregion
    
    #region 1. TaskPriorityPredictor - 任务优先级预测
    
    /// <summary>
    /// 学习任务特征与优先级的关系
    /// 预测哪些任务应该优先执行
    /// </summary>
    public class TaskPriorityPredictor
    {
        // 线性回归系数（轻量级模型）
        private float _weightDepth = 0.15f;
        private float _weightTGate = 0.40f;
        private float _weightQubit = 0.10f;
        private float _weightWaitTime = 0.25f;
        private float _weightPastPriority = 0.10f;
        private float _bias = 0.5f;
        
        private List<(TaskFeatures features, float actualPriority)> _trainingData = new();
        
        /// <summary>
        /// 预测任务优先级 [0, 1]
        /// T门数量权重最高（关键资源）
        /// 等待时间次之（公平性）
        /// </summary>
        public float Predict(TaskFeatures features)
        {
            var score = 
                features.Depth * _weightDepth +
                features.TGateCount * _weightTGate / 100f +
                features.QubitCount * _weightQubit / 10f +
                features.WaitTime * _weightWaitTime / 100f +
                features.PastPriority * _weightPastPriority +
                _bias;
            
            return Math.Clamp(score, 0f, 1f);
        }
        
        /// <summary>
        /// 从执行数据中学习权重
        /// 简单的在线学习算法
        /// </summary>
        public void Learn(TaskFeatures features, float actualPriority, float learningRate = 0.01f)
        {
            _trainingData.Add((features, actualPriority));
            
            var predicted = Predict(features);
            var error = actualPriority - predicted;
            
            // 简单的梯度下降
            _weightDepth += error * features.Depth * learningRate;
            _weightTGate += error * (features.TGateCount / 100f) * learningRate;
            _weightQubit += error * (features.QubitCount / 10f) * learningRate;
            _weightWaitTime += error * (features.WaitTime / 100f) * learningRate;
            _weightPastPriority += error * features.PastPriority * learningRate;
            _bias += error * learningRate;
        }
        
        public int TrainingDataCount => _trainingData.Count;
    }
    
    #endregion
    
    #region 2. SWAPCostPredictor - SWAP成本自适应预测
    
    /// <summary>
    /// 学习在特定硬件拓扑上的SWAP成本
    /// 根据qubit距离和当前配置动态调整
    /// </summary>
    public class SWAPCostPredictor
    {
        // 基础SWAP成本（假设线性拓扑）
        private Dictionary<(int, int), float> _costCache = new();
        private float _baseSwapCost = 1.0f;
        
        /// <summary>
        /// 预测源qubit到目标qubit的SWAP成本倍数
        /// 假设是线性拓扑：qubits 0-1-2-3-4...
        /// </summary>
        public float Predict(int srcQubit, int dstQubit)
        {
            var key = (Math.Min(srcQubit, dstQubit), Math.Max(srcQubit, dstQubit));
            
            if (_costCache.TryGetValue(key, out var cachedCost))
                return cachedCost;
            
            // 基础模型：距离越远，SWAP成本指数增长
            var distance = Math.Abs(dstQubit - srcQubit);
            var cost = _baseSwapCost * (float)Math.Pow(1.3, distance - 1);
            
            _costCache[key] = cost;
            return cost;
        }
        
        /// <summary>
        /// 根据实际观测更新成本预测
        /// 适应特定硬件的特性
        /// </summary>
        public void ObserveActualCost(int srcQubit, int dstQubit, float actualCost)
        {
            var key = (Math.Min(srcQubit, dstQubit), Math.Max(srcQubit, dstQubit));
            
            if (_costCache.TryGetValue(key, out var cached))
            {
                // 指数加权移动平均
                _costCache[key] = 0.7f * cached + 0.3f * actualCost;
            }
            else
            {
                _costCache[key] = actualCost;
            }
        }
        
        public int ObservedPairs => _costCache.Count;
    }
    
    #endregion
    
    #region 3. ResourcePredictor - 资源执行时间预测
    
    /// <summary>
    /// 预测任务执行时间和资源占用
    /// 基于电路复杂性、系统负载等因素
    /// </summary>
    public class ResourcePredictor
    {
        private float _depthToTimeRatio = 0.1f;      // 深度 -> 执行时间
        private float _tGateToTimeRatio = 0.05f;     // T门 -> 执行时间
        
        private List<(float depth, int tGates, int qubits, float actualTime)> _history = new();
        
        /// <summary>
        /// 预测任务执行时间（相对单位）
        /// </summary>
        public float PredictExecutionTime(int depth, int tGates, int currentSystemLoad)
        {
            var baseTime = 
                depth * _depthToTimeRatio + 
                tGates * _tGateToTimeRatio;
            
            // 系统负载会增加等待时间
            return baseTime * (1f + currentSystemLoad * 0.1f);
        }
        
        /// <summary>
        /// 预测峰值qubit占用
        /// </summary>
        public int PredictPeakQubitUsage(int qubitCount)
        {
            // 考虑到辅助qubit的使用
            return (int)(qubitCount * 1.2f);
        }
        
        /// <summary>
        /// 从历史数据学习
        /// </summary>
        public void RecordExecution(int depth, int tGates, int qubits, float actualTime)
        {
            _history.Add((depth, tGates, qubits, actualTime));
            
            // 动态调整比率
            if (_history.Count > 10)
            {
                var recentAvg = _history.TakeLast(10)
                    .Average(x => x.actualTime / (x.depth * _depthToTimeRatio + x.tGates * _tGateToTimeRatio));
                
                _depthToTimeRatio *= (float)(0.8 + 0.2 * recentAvg);
            }
        }
        
        public int HistorySize => _history.Count;
    }
    
    #endregion
    
    #region 4. FaultPredictor - 故障检测和预防
    
    /// <summary>
    /// 识别容易出现故障的qubit
    /// 用于避免在不可靠的硬件上调度任务
    /// </summary>
    public class FaultPredictor
    {
        private Dictionary<int, float> _qubitRiskScores = new();
        private Dictionary<int, int> _failureCount = new();
        private Dictionary<int, int> _usageCount = new();
        
        /// <summary>
        /// 计算特定qubit的故障风险 [0, 1]
        /// 考虑错误率、最近使用频率等因素
        /// </summary>
        public float PredictRisk(int qubitId, float baseErrorRate = 0.001f)
        {
            if (!_qubitRiskScores.TryGetValue(qubitId, out var risk))
            {
                risk = baseErrorRate;
            }
            
            // 使用频率高的qubit风险略高
            if (_usageCount.TryGetValue(qubitId, out var usage))
            {
                risk *= (1f + usage * 0.001f);
            }
            
            return Math.Clamp(risk, 0f, 1f);
        }
        
        /// <summary>
        /// 记录qubit故障
        /// 更新该qubit的风险评分
        /// </summary>
        public void RecordFailure(int qubitId)
        {
            if (!_failureCount.ContainsKey(qubitId))
                _failureCount[qubitId] = 0;
            
            _failureCount[qubitId]++;
            
            // 更新风险分数
            if (_usageCount.TryGetValue(qubitId, out var usage))
            {
                _qubitRiskScores[qubitId] = (float)_failureCount[qubitId] / usage;
            }
        }
        
        /// <summary>
        /// 记录qubit使用（成功）
        /// </summary>
        public void RecordUsage(int qubitId)
        {
            if (!_usageCount.ContainsKey(qubitId))
                _usageCount[qubitId] = 0;
            
            _usageCount[qubitId]++;
        }
        
        /// <summary>
        /// 获取最危险的qubits
        /// 用于避免调度
        /// </summary>
        public List<int> GetRiskyQubits(float threshold = 0.7f)
        {
            return _qubitRiskScores
                .Where(kv => kv.Value > threshold)
                .OrderByDescending(kv => kv.Value)
                .Select(kv => kv.Key)
                .ToList();
        }
        
        public int MonitoredQubitCount => _usageCount.Count;
    }
    
    #endregion
}
