using Spectre.Console;

namespace AIOS;

/// <summary>
/// AI 进程调度器
/// 使用 AI 决策来决定哪个进程应该获得 CPU 时间
/// </summary>
public class AIScheduler
{
    private readonly Random _random = new();
    private int _decisionCount = 0;

    /// <summary>
    /// AI 调度决策
    /// 基于多维度因素智能决定进程优先级
    /// </summary>
    public AIDecision? MakeDecision(SystemState state, List<SimulatedProcess> processes)
    {
        _decisionCount++;
        var activeProcesses = processes.Where(p => p.State != ProcessState.Terminated).ToList();

        // AI 决策逻辑（模拟智能决策）
        
        // 1. 检测 CPU 过载情况
        if (state.CpuUsage > 80)
        {
            // 找出低优先级且 CPU 需求高的进程
            var candidate = activeProcesses
                .Where(p => p.Priority < 5 && p.CpuDemand > 300)
                .OrderByDescending(p => p.CpuDemand)
                .FirstOrDefault();

            if (candidate != null)
            {
                return new AIDecision(
                    Action: "降低优先级",
                    Target: $"{candidate.Name} (PID: {candidate.Pid})",
                    Reason: $"CPU 使用率 {state.CpuUsage:F0}% > 80%，该进程 CPU 需求过高 ({candidate.CpuDemand})",
                    Execute: sim => sim.AdjustProcessPriority(candidate.Pid, Math.Max(1, candidate.Priority - 2))
                );
            }
        }

        // 2. 检测长时间等待的进程
        var longWaitProcess = activeProcesses
            .Where(p => p.State == ProcessState.Waiting)
            .OrderBy(p => p.LastScheduledAt ?? DateTime.Now)
            .FirstOrDefault();

        if (longWaitProcess != null && state.CpuUsage < 50)
        {
            return new AIDecision(
                Action: "提升优先级",
                Target: $"{longWaitProcess.Name} (PID: {longWaitProcess.Pid})",
                Reason: $"进程等待时间过长，当前 CPU 空闲 ({state.CpuUsage:F0}%)",
                Execute: sim => sim.AdjustProcessPriority(longWaitProcess.Pid, Math.Min(10, longWaitProcess.Priority + 3))
            );
        }

        // 3. 检测异常进程（CPU 需求突增）
        var anomalyProcess = activeProcesses
            .Where(p => p.CpuDemand > 800)
            .FirstOrDefault();

        if (anomalyProcess != null)
        {
            return new AIDecision(
                Action: "警告并限制",
                Target: $"{anomalyProcess.Name} (PID: {anomalyProcess.Pid})",
                Reason: $"CPU 需求异常 ({anomalyProcess.CpuDemand})，可能存在问题",
                Execute: sim => 
                {
                    sim.AdjustProcessPriority(anomalyProcess.Pid, 1);
                    AnsiConsole.MarkupLine($"  [red]⚠ 警告：{anomalyProcess.Name} 可能是问题进程[/]");
                }
            );
        }

        // 4. 定期优化（每 3 个时钟周期）
        if (_decisionCount % 3 == 0 && state.CpuUsage < 60)
        {
            var highPriorityLowUsage = activeProcesses
                .Where(p => p.Priority > 7 && p.CpuDemand < 100)
                .OrderByDescending(p => p.Priority)
                .FirstOrDefault();

            if (highPriorityLowUsage != null)
            {
                return new AIDecision(
                    Action: "优先级优化",
                    Target: $"{highPriorityLowUsage.Name} (PID: {highPriorityLowUsage.Pid})",
                    Reason: "高优先级但低 CPU 使用，优化资源分配",
                    Execute: sim => sim.AdjustProcessPriority(highPriorityLowUsage.Pid, 5)
                );
            }
        }

        return null;
    }
}

/// <summary>
/// AI 内存管理器
/// 智能管理内存分配和回收
/// </summary>
public class AIMemoryManager
{
    private readonly Dictionary<int, int> _memoryHistory = new();
    private int _tickCount = 0;

    /// <summary>
    /// AI 内存管理决策
    /// 基于历史使用模式预测和优化内存分配
    /// </summary>
    public AIDecision? MakeDecision(SystemState state, List<SimulatedProcess> processes)
    {
        _tickCount++;
        var activeProcesses = processes.Where(p => p.State != ProcessState.Terminated).ToList();

        // 记录历史
        foreach (var process in activeProcesses)
        {
            if (!_memoryHistory.ContainsKey(process.Pid))
                _memoryHistory[process.Pid] = 0;
            _memoryHistory[process.Pid] = process.MemoryDemand;
        }

        // 1. 检测内存压力
        if (state.MemoryUsage > 85)
        {
            // 找出内存需求大但使用效率低的进程
            var inefficientProcess = activeProcesses
                .Where(p => p.MemoryDemand > 512 && p.CpuDemand < 100)
                .OrderByDescending(p => p.MemoryDemand)
                .FirstOrDefault();

            if (inefficientProcess != null)
            {
                var newLimit = (int)(inefficientProcess.MemoryDemand * 0.7);
                return new AIDecision(
                    Action: "内存压缩",
                    Target: $"{inefficientProcess.Name} (PID: {inefficientProcess.Pid})",
                    Reason: $"内存使用率 {state.MemoryUsage:F0}% > 85%，该进程内存占用大但 CPU 使用低",
                    Execute: sim => sim.AdjustProcessMemory(inefficientProcess.Pid, newLimit)
                );
            }
        }

        // 2. 检测内存泄漏嫌疑（内存需求持续增长）
        if (_tickCount > 5)
        {
            var suspectProcess = activeProcesses
                .Where(p => p.MemoryDemand > (_memoryHistory.GetValueOrDefault(p.Pid, 0) * 1.3))
                .OrderByDescending(p => p.MemoryDemand)
                .FirstOrDefault();

            if (suspectProcess != null && state.MemoryUsage > 60)
            {
                return new AIDecision(
                    Action: "内存限制",
                    Target: $"{suspectProcess.Name} (PID: {suspectProcess.Pid})",
                    Reason: "检测到内存需求快速增长，可能存在泄漏",
                    Execute: sim => 
                    {
                        sim.AdjustProcessMemory(suspectProcess.Pid, (int)(suspectProcess.MemoryDemand * 0.8));
                        AnsiConsole.MarkupLine($"  [yellow]⚠ 警告：{suspectProcess.Name} 可能内存泄漏[/]");
                    }
                );
            }
        }

        // 3. 内存充足时放宽限制
        if (state.MemoryUsage < 40 && _tickCount % 5 == 0)
        {
            var restrictedProcess = activeProcesses
                .Where(p => p.MemoryLimit > 0 && p.MemoryLimit < p.MemoryDemand)
                .FirstOrDefault();

            if (restrictedProcess != null)
            {
                return new AIDecision(
                    Action: "内存释放",
                    Target: $"{restrictedProcess.Name} (PID: {restrictedProcess.Pid})",
                    Reason: $"内存充裕 ({state.MemoryUsage:F0}%)，解除限制",
                    Execute: sim => sim.AdjustProcessMemory(restrictedProcess.Pid, restrictedProcess.MemoryDemand)
                );
            }
        }

        return null;
    }
}

/// <summary>
/// AI 系统监控器
/// 实时采集和分析系统指标
/// </summary>
public class AISystemMonitor
{
    private readonly List<double> _cpuHistory = new();
    private readonly List<double> _memoryHistory = new();
    private readonly List<double> _responseTimeHistory = new();

    public SystemState GetSystemState(SystemSimulator simulator)
    {
        var processes = simulator.GetProcesses();
        var activeProcesses = processes.Where(p => p.State != ProcessState.Terminated).ToList();

        // 计算 CPU 使用率
        var totalCpuDemand = simulator.GetTotalCpuDemand();
        var maxCpuCapacity = simulator.CpuCores * 1000; // 每核心 1000 单位
        var cpuUsage = Math.Min(100, (double)totalCpuDemand / maxCpuCapacity * 100);

        // 计算内存使用率
        var totalMemoryDemand = simulator.GetTotalMemoryDemand();
        var memoryUsage = Math.Min(100, (double)totalMemoryDemand / simulator.TotalMemory * 100);

        // 计算平均响应时间（模拟）
        var avgResponseTime = activeProcesses.Count > 0 
            ? activeProcesses.Average(p => p.CpuDemand) / 10.0 
            : 0;

        // 等待队列
        var waitingProcesses = activeProcesses.Count(p => p.State == ProcessState.Waiting);

        // 记录历史
        _cpuHistory.Add(cpuUsage);
        _memoryHistory.Add(memoryUsage);
        _responseTimeHistory.Add(avgResponseTime);

        // 保持最近 10 个样本
        if (_cpuHistory.Count > 10) _cpuHistory.RemoveAt(0);
        if (_memoryHistory.Count > 10) _memoryHistory.RemoveAt(0);
        if (_responseTimeHistory.Count > 10) _responseTimeHistory.RemoveAt(0);

        return new SystemState(
            CpuUsage: cpuUsage,
            MemoryUsage: memoryUsage,
            ActiveProcesses: activeProcesses.Count,
            WaitingProcesses: waitingProcesses,
            AverageResponseTime: avgResponseTime
        );
    }

    /// <summary>
    /// 预测趋势（基于历史数据）
    /// </summary>
    public string PredictTrend()
    {
        if (_cpuHistory.Count < 3) return "数据不足";

        var recentAvg = _cpuHistory.TakeLast(3).Average();
        var olderAvg = _cpuHistory.TakeLast(6).Skip(3).Average();

        if (recentAvg > olderAvg * 1.1) return "上升 ↗";
        if (recentAvg < olderAvg * 0.9) return "下降 ↘";
        return "稳定 →";
    }
}
