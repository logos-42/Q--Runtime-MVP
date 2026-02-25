using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using AIIntegration.Scheduler;
using AIIntegration.AI;

namespace AIIntegration.Silq
{
    /// <summary>
    /// Silq 电路解析和AI适配
    /// 将Silq源代码转换为AI可理解的电路表示
    /// </summary>
    public class SilqCircuitAdapter
    {
        // 静态Regex缓存 - 避免重复创建
        private static readonly Dictionary<string, Regex> _regexCache = new();
        // 静态编译选项 - 提升性能
        private const RegexOptions CompiledOptions = RegexOptions.Compiled;
        
        private readonly string _silqSource;
        private SilqCircuitMetadata _metadata = new();
        
        public SilqCircuitAdapter(string silqSource)
        {
            _silqSource = silqSource;
        }
        
        /// <summary>
        /// 获取缓存的Regex对象
        /// </summary>
        private static Regex GetCachedRegex(string pattern)
        {
            if (!_regexCache.TryGetValue(pattern, out var regex))
            {
                regex = new Regex(pattern, CompiledOptions);
                _regexCache[pattern] = regex;
            }
            return regex;
        }
        
        /// <summary>
        /// 解析Silq源代码提取电路元数据
        /// </summary>
        public SilqCircuitMetadata Parse()
        {
            _metadata = new SilqCircuitMetadata();
            
            ExtractFunctionSignatures();
            EstimateResources();
            AnalyzeQuantumOperations();
            DetectPatterns();
            
            return _metadata;
        }
        
        /// <summary>
        /// 提取函数签名（电路定义）
        /// </summary>
        private void ExtractFunctionSignatures()
        {
            // 匹配 def functionName[...](...)
            var funcPattern = @"def\s+(\w+)\[?([^\]]*)\]?\((.*?)\)\s*:\s*([^{]+)";
            var matches = Regex.Matches(_silqSource, funcPattern);
            
            foreach (Match match in matches)
            {
                var funcName = match.Groups[1].Value;
                var typeParams = match.Groups[2].Value;
                var paramsStr = match.Groups[3].Value;
                var returnType = match.Groups[4].Value.Trim();
                
                _metadata.Functions.Add(new SilqFunction
                {
                    Name = funcName,
                    TypeParameters = typeParams.Split(',').Select(s => s.Trim()).ToList(),
                    Parameters = paramsStr.Split(',').Select(s => s.Trim()).ToList(),
                    ReturnType = returnType
                });
            }
        }
        
        /// <summary>
        /// 估计资源消耗
        /// </summary>
        private void EstimateResources()
        {
            // 计算不同类型的量子门
            var hCount = CountOccurrences("H(");
            var cnotCount = CountOccurrences("CNOT(");
            var zCount = CountOccurrences("Z(");
            var xCount = CountOccurrences("X(");
            var sCount = CountOccurrences("S(");
            var tCount = CountOccurrences("T(");
            var measureCount = CountOccurrences("measure(");
            
            _metadata.Resources = new SilqResources
            {
                HGateCount = hCount,
                CNOTGateCount = cnotCount,
                ZGateCount = zCount,
                XGateCount = xCount,
                SGateCount = sCount,
                TGateCount = tCount,
                MeasurementCount = measureCount,
                TotalGateCount = hCount + cnotCount + zCount + xCount + sCount + tCount,
                
                // T门权重高（错误纠正成本）
                TCostEstimate = tCount * 10f,
                
                // 深度估计（线性逼近）
                DepthEstimate = EstimateDepth(),
                
                // Clifford门计数（H, X, Z, CNOT, S 都是Clifford）
                CliffordCount = hCount + xCount + zCount + cnotCount + sCount
            };
        }
        
        private int EstimateDepth()
        {
            // 简化的深度估计：基于门序列长度
            var lines = _silqSource.Split('\n');
            var gateLines = lines.Count(l => 
                l.Contains("(") && (l.Contains("H(") || l.Contains("CNOT(") || 
                l.Contains("X(") || l.Contains("Z(") || l.Contains("S(") || l.Contains("T(")));
            return gateLines;
        }
        
        /// <summary>
        /// 分析量子操作模式
        /// </summary>
        private void AnalyzeQuantumOperations()
        {
            // 检测特定的量子算法模式
            if (_silqSource.Contains("measureBellState") || ContainsIgnoreCase("Bell"))
            {
                _metadata.AlgorithmType = "Bell State";
            }
            else if (ContainsIgnoreCase("Teleport"))
            {
                _metadata.AlgorithmType = "Quantum Teleportation";
            }
            else if (ContainsIgnoreCase("Grover"))
            {
                _metadata.AlgorithmType = "Grover Search";
            }
            else if (ContainsIgnoreCase("Fourier"))
            {
                _metadata.AlgorithmType = "QFT";
            }
            else
            {
                _metadata.AlgorithmType = "Custom";
            }
            
            // 检测Qubit数量
            _metadata.EstimatedQubitCount = EstimateQubitCount();
        }
        
        private int EstimateQubitCount()
        {
            // 从变量声明提取
            var qubitVars = Regex.Matches(_silqSource, @"var\s+\w+\s*:=.*:\s*𝔹|var\s+\w+\s*:=.*:\s*B");
            var arrayQubit = Regex.Matches(_silqSource, @"𝔹\[\]|\w+\[\d+\]");
            
            return qubitVars.Count + arrayQubit.Count;
        }
        
        /// <summary>
        /// 检测优化机会
        /// </summary>
        private void DetectPatterns()
        {
            var opportunities = new List<string>();
            
            if (_metadata.Resources.TGateCount > 20)
                opportunities.Add("高T门数量 - 考虑T门优化");
                
            if (_metadata.Resources.DepthEstimate > 30)
                opportunities.Add("高电路深度 - 考虑并行化");
                
            if (_metadata.Resources.CNOTGateCount > 15)
                opportunities.Add("高CNOT数 - 考虑CNOT还原");
                
            if (ContainsIgnoreCase("for") || ContainsIgnoreCase("while"))
                opportunities.Add("包含循环 - 展开可能减少开销");
            
            _metadata.OptimizationOpportunities = opportunities;
        }
        
        private int CountOccurrences(string pattern)
        {
            var regex = GetCachedRegex(Regex.Escape(pattern));
            return regex.Matches(_silqSource).Count;
        }
        
        private bool ContainsIgnoreCase(string text)
        {
            return _silqSource.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0;
        }
        
        /// <summary>
        /// 将Silq电路转换为C# CircuitBlock以供AI处理
        /// </summary>
        public CircuitBlock ConvertToCircuitBlock(string circuitName = "SilqCircuit")
        {
            return new CircuitBlock(
                Name: circuitName,
                Depth: _metadata.Resources.DepthEstimate,
                TGateCount: _metadata.Resources.TGateCount,
                QubitCount: _metadata.EstimatedQubitCount
            );
        }
    }
    
    /// <summary>
    /// Silq电路元数据
    /// </summary>
    public class SilqCircuitMetadata
    {
        public List<SilqFunction> Functions { get; set; } = new();
        public SilqResources Resources { get; set; } = new();
        public string AlgorithmType { get; set; } = "Unknown";
        public int EstimatedQubitCount { get; set; }
        public List<string> OptimizationOpportunities { get; set; } = new();
    }
    
    public class SilqFunction
    {
        public string Name { get; set; } = "";
        public List<string> TypeParameters { get; set; } = new();
        public List<string> Parameters { get; set; } = new();
        public string ReturnType { get; set; } = "";
    }
    
    public class SilqResources
    {
        public int HGateCount { get; set; }
        public int CNOTGateCount { get; set; }
        public int ZGateCount { get; set; }
        public int XGateCount { get; set; }
        public int SGateCount { get; set; }
        public int TGateCount { get; set; }
        public int MeasurementCount { get; set; }
        public int TotalGateCount { get; set; }
        public float TCostEstimate { get; set; }
        public int DepthEstimate { get; set; }
        public int CliffordCount { get; set; }
    }
}
