using Spectre.Console;

namespace AIOS;

/// <summary>
/// AIOS 增强版 - 智能管理模块
/// </summary>
public class AIEnhancedModules
{
    /// <summary>
    /// 基于规则的进程分类器（轻量级，替代 ML）
    /// 判断进程是否为"问题进程"
    /// </summary>
    public class RuleBasedClassifier
    {
        /// <summary>
        /// 分析进程是否有问题
        /// </summary>
        public (bool isProblematic, float confidence) Analyze(SimulatedProcess process)
        {
            float score = 0;
            var reasons = new List<string>();

            // CPU 异常检测
            if (process.CpuDemand > 2000)
            {
                score += 0.4f;
                reasons.Add($"CPU 过高 ({process.CpuDemand})");
            }
            else if (process.CpuDemand > 1500)
            {
                score += 0.2f;
            }

            // 内存异常检测
            if (process.MemoryDemand > 4000)
            {
                score += 0.4f;
                reasons.Add($"内存过高 ({process.MemoryDemand}MB)");
            }
            else if (process.MemoryDemand > 2000)
            {
                score += 0.2f;
            }

            // 优先级与资源不匹配
            if (process.Priority < 3 && process.CpuDemand > 1000)
            {
                score += 0.2f;
                reasons.Add("低优先级高资源");
            }

            // 高优先级但低使用率（可能是僵尸进程）
            if (process.Priority > 7 && process.CpuDemand < 100 && process.MemoryDemand > 500)
            {
                score += 0.3f;
                reasons.Add("高优先级低使用");
            }

            var confidence = Math.Min(1.0f, score);
            var isProblematic = confidence > 0.5f;

            if (isProblematic)
            {
                AnsiConsole.MarkupLine($"  [dim]  分类依据：{string.Join(", ", reasons)}[/]");
            }

            return (isProblematic, confidence);
        }

        public void AddTrainingSample(SimulatedProcess process, bool isProblematic)
        {
            // 规则-based 不需要训练
        }
    }
}

/// <summary>
/// 增强版 AI 调度器 - 集成规则分类
/// </summary>
public class AIEnhancedScheduler
{
    private readonly AIEnhancedModules.RuleBasedClassifier _classifier;
    private readonly Dictionary<int, int> _cpuHistory = new();
    private readonly Random _random = new();

    public AIEnhancedScheduler()
    {
        _classifier = new AIEnhancedModules.RuleBasedClassifier();
        AnsiConsole.MarkupLine("  [green]✓ AI 增强调度器初始化完成（规则 + 启发式）[/]");
    }

    public List<AIDecision> MakeDecisions(SystemState state, List<SimulatedProcess> processes)
    {
        var decisions = new List<AIDecision>();
        var activeProcesses = processes.Where(p => p.State != ProcessState.Terminated).ToList();

        // 1. 规则检测问题进程
        foreach (var process in activeProcesses)
        {
            var (isProblematic, confidence) = _classifier.Analyze(process);
            
            if (isProblematic && confidence > 0.5f)
            {
                decisions.Add(new AIDecision(
                    Action: $"[[AI]] 问题进程检测",
                    Target: $"{process.Name} (PID: {process.Pid})",
                    Reason: $"置信度 {confidence:P0} - CPU:{process.CpuDemand}, Mem:{process.MemoryDemand}MB",
                    Execute: sim =>
                    {
                        sim.AdjustProcessPriority(process.Pid, 1);
                        _classifier.AddTrainingSample(process, true);
                    }
                ));
            }
        }

        // 2. 基于历史数据的趋势预测
        foreach (var process in activeProcesses)
        {
            if (!_cpuHistory.ContainsKey(process.Pid))
                _cpuHistory[process.Pid] = process.CpuDemand;
            else
            {
                var prevDemand = _cpuHistory[process.Pid];
                var growthRate = (process.CpuDemand - prevDemand) / (float)prevDemand;
                
                if (growthRate > 0.2f && process.CpuDemand > 500)
                {
                    decisions.Add(new AIDecision(
                        Action: "[[预测]] CPU 需求增长",
                        Target: $"{process.Name} (PID: {process.Pid})",
                        Reason: $"增长率 {growthRate:P0}，提前干预",
                        Execute: sim => sim.AdjustProcessPriority(process.Pid, Math.Max(1, process.Priority - 2))
                    ));
                }
                
                _cpuHistory[process.Pid] = process.CpuDemand;
            }
        }

        // 3. 公平调度 - 防止饥饿
        var starvingProcess = activeProcesses
            .Where(p => p.Priority <= 2 && p.CpuTime < 50)
            .OrderBy(p => p.CpuTime)
            .FirstOrDefault();

        if (starvingProcess != null && state.CpuUsage < 70)
        {
            decisions.Add(new AIDecision(
                Action: "[[公平]] 防止进程饥饿",
                Target: $"{starvingProcess.Name} (PID: {starvingProcess.Pid})",
                Reason: "低优先级进程长时间未获得 CPU 时间",
                Execute: sim => sim.AdjustProcessPriority(starvingProcess.Pid, 5)
            ));
        }

        return decisions;
    }
}

/// <summary>
/// 磁盘 I/O 管理器
/// </summary>
public class AIDiskManager
{
    private readonly Dictionary<int, int> _ioHistory = new();
    private readonly Random _random = new();

    public AIDecision? MakeDecision(SystemState state, List<SimulatedProcess> processes, DiskState diskState)
    {
        var activeProcesses = processes.Where(p => p.State != ProcessState.Terminated).ToList();

        // 模拟 I/O 活动
        foreach (var process in activeProcesses)
        {
            if (!_ioHistory.ContainsKey(process.Pid))
                _ioHistory[process.Pid] = 0;
            
            // 随机 I/O 波动
            var ioActivity = _random.Next(0, 100);
            _ioHistory[process.Pid] = ioActivity;

            // 检测 I/O 密集型进程
            if (ioActivity > 80 && diskState.QueueLength > 5)
            {
                return new AIDecision(
                    Action: "[[磁盘]] I/O 限流",
                    Target: $"{process.Name} (PID: {process.Pid})",
                    Reason: $"I/O 活动 {ioActivity}% 过高，队列长度 {diskState.QueueLength}",
                    Execute: _ => AnsiConsole.MarkupLine($"  [yellow]→ {process.Name} I/O 限流激活[/]")
                );
            }
        }

        // 检测磁盘队列溢出
        if (diskState.QueueLength > 10)
        {
            var highIoProcess = activeProcesses
                .Where(p => _ioHistory.GetValueOrDefault(p.Pid, 0) > 50)
                .OrderByDescending(p => _ioHistory.GetValueOrDefault(p.Pid, 0))
                .FirstOrDefault();

            if (highIoProcess != null)
            {
                return new AIDecision(
                    Action: "[[磁盘]] 队列拥塞控制",
                    Target: $"{highIoProcess.Name} (PID: {highIoProcess.Pid})",
                    Reason: $"磁盘队列 {diskState.QueueLength} > 10，暂停高 I/O 进程",
                    Execute: _ => AnsiConsole.MarkupLine($"  [red]→ {highIoProcess.Name} I/O 暂停 500ms[/]")
                );
            }
        }

        return null;
    }
}

public record DiskState(
    double UsagePercent,
    int QueueLength,
    double ReadSpeedMBps,
    double WriteSpeedMBps
);

/// <summary>
/// 网络带宽管理器
/// </summary>
public class AINetworkManager
{
    private readonly Dictionary<int, int> _bandwidthUsage = new();
    private readonly Random _random = new();

    public AIDecision? MakeDecision(SystemState state, List<SimulatedProcess> processes, NetworkState networkState)
    {
        var activeProcesses = processes.Where(p => p.State != ProcessState.Terminated).ToList();

        // 模拟网络活动
        foreach (var process in activeProcesses)
        {
            if (!_bandwidthUsage.ContainsKey(process.Pid))
                _bandwidthUsage[process.Pid] = 0;
            
            // 随机网络使用
            var bandwidth = _random.Next(0, 100);
            _bandwidthUsage[process.Pid] = bandwidth;

            // 检测带宽滥用
            if (bandwidth > 80 && networkState.TotalUsage > 90)
            {
                return new AIDecision(
                    Action: "[[网络]] 带宽限制",
                    Target: $"{process.Name} (PID: {process.Pid})",
                    Reason: $"网络使用 {bandwidth}%，总带宽 {networkState.TotalUsage}%",
                    Execute: _ => AnsiConsole.MarkupLine($"  [yellow]→ {process.Name} 网络限速 50%[/]")
                );
            }
        }

        // 网络拥塞时的优先级调度
        if (networkState.TotalUsage > 95)
        {
            var lowPriorityHighBandwidth = activeProcesses
                .Where(p => p.Priority < 5 && _bandwidthUsage.GetValueOrDefault(p.Pid, 0) > 50)
                .OrderByDescending(p => _bandwidthUsage.GetValueOrDefault(p.Pid, 0))
                .FirstOrDefault();

            if (lowPriorityHighBandwidth != null)
            {
                return new AIDecision(
                    Action: "[[网络]] 拥塞控制",
                    Target: $"{lowPriorityHighBandwidth.Name} (PID: {lowPriorityHighBandwidth.Pid})",
                    Reason: $"网络拥塞 {networkState.TotalUsage}%，限制低优先级进程",
                    Execute: _ => AnsiConsole.MarkupLine($"  [red]→ {lowPriorityHighBandwidth.Name} 网络暂停 1s[/]")
                );
            }
        }

        return null;
    }
}

public record NetworkState(
    double TotalUsage,
    double DownloadSpeedMbps,
    double UploadSpeedMbps,
    int ActiveConnections
);

/// <summary>
/// 进程健康检查器 - 决定是否需要终止/重启进程
/// </summary>
public class AIProcessHealthChecker
{
    private readonly Dictionary<int, int> _violationCount = new();

    public AIDecision? MakeDecision(SimulatedProcess process, SystemState state)
    {
        if (!_violationCount.ContainsKey(process.Pid))
            _violationCount[process.Pid] = 0;

        // 检测无响应进程
        if (process.CpuDemand < 10 && process.MemoryDemand > 1000)
        {
            _violationCount[process.Pid]++;
            
            if (_violationCount[process.Pid] >= 3)
            {
                return new AIDecision(
                    Action: "[[健康]] 进程无响应",
                    Target: $"{process.Name} (PID: {process.Pid})",
                    Reason: "连续 3 次检测无响应，建议重启",
                    Execute: sim =>
                    {
                        AnsiConsole.MarkupLine($"  [yellow]→ 重启 {process.Name}...[/]");
                        process.CpuTime = 0;
                        process.State = ProcessState.Ready;
                        _violationCount[process.Pid] = 0;
                    }
                );
            }
        }

        // 检测资源滥用进程
        if (process.CpuDemand > 3000 && process.MemoryDemand > 4000)
        {
            _violationCount[process.Pid]++;
            
            if (_violationCount[process.Pid] >= 5)
            {
                return new AIDecision(
                    Action: "[[健康]] 资源滥用",
                    Target: $"{process.Name} (PID: {process.Pid})",
                    Reason: "连续 5 次检测资源滥用，建议终止",
                    Execute: sim =>
                    {
                        AnsiConsole.MarkupLine($"  [red]✗ 终止 {process.Name}[/]");
                        sim.TerminateProcess(process.Pid);
                        _violationCount.Remove(process.Pid);
                    }
                );
            }
        }

        // 恢复计数
        if (process.CpuDemand < 1000 && process.MemoryDemand < 2000)
        {
            _violationCount[process.Pid] = Math.Max(0, _violationCount[process.Pid] - 1);
        }

        return null;
    }
}
