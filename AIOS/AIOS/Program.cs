using Spectre.Console;

namespace AIOS;

/// <summary>
/// AIOS - AI 管理的操作系统原型
/// 演示 AI 如何管理 CPU、内存、进程、磁盘、网络等系统资源
/// </summary>
public class Program
{
    public static async Task Main(string[] args)
    {
        AnsiConsole.MarkupLine("[bold cyan]╔════════════════════════════════════════════════════════════╗[/]");
        AnsiConsole.MarkupLine("[bold cyan]║         AIOS - AI Operating System Prototype              ║[/]");
        AnsiConsole.MarkupLine("[bold cyan]║        AI 管理的操作系统原型 (增强版)                      ║[/]");
        AnsiConsole.MarkupLine("[bold cyan]╚════════════════════════════════════════════════════════════╝[/]");
        AnsiConsole.WriteLine();

        // 创建系统核心
        var kernel = new AIKernel();
        
        // 启动系统
        await kernel.RunAsync();
    }
}

/// <summary>
/// AI 操作系统内核
/// 协调所有 AI 管理模块
/// </summary>
public class AIKernel
{
    private readonly AIEnhancedScheduler _enhancedScheduler;
    private readonly AIDiskManager _diskManager;
    private readonly AINetworkManager _networkManager;
    private readonly AIProcessHealthChecker _healthChecker;
    private readonly AIScheduler _scheduler;
    private readonly AIMemoryManager _memoryManager;
    private readonly AISystemMonitor _monitor;
    private readonly SystemSimulator _simulator;
    private bool _running = true;

    public AIKernel()
    {
        _enhancedScheduler = new AIEnhancedScheduler();
        _diskManager = new AIDiskManager();
        _networkManager = new AINetworkManager();
        _healthChecker = new AIProcessHealthChecker();
        _scheduler = new AIScheduler();
        _memoryManager = new AIMemoryManager();
        _monitor = new AISystemMonitor();
        _simulator = new SystemSimulator();
    }

    public async Task RunAsync()
    {
        AnsiConsole.MarkupLine("[green]✓ AIOS 内核初始化完成[/]");
        AnsiConsole.MarkupLine($"  - CPU 核心数：{_simulator.CpuCores}");
        AnsiConsole.MarkupLine($"  - 总内存：{_simulator.TotalMemory} MB");
        AnsiConsole.MarkupLine($"  - AI 调度器：[cyan]在线[/] (ML 增强)");
        AnsiConsole.MarkupLine($"  - AI 内存管理：[cyan]在线[/]");
        AnsiConsole.MarkupLine($"  - AI 磁盘管理：[cyan]在线[/]");
        AnsiConsole.MarkupLine($"  - AI 网络管理：[cyan]在线[/]");
        AnsiConsole.MarkupLine($"  - AI 健康检查：[cyan]在线[/]");
        AnsiConsole.WriteLine();

        // 模拟一些进程（高负载场景）
        _simulator.CreateProcess("Chrome 浏览器", 2500, 3000);
        _simulator.CreateProcess("VS Code", 2000, 2048);
        _simulator.CreateProcess("Photoshop", 3000, 4096);
        _simulator.CreateProcess("音乐播放器", 800, 512);
        _simulator.CreateProcess("后台下载", 600, 1024);
        _simulator.CreateProcess("系统更新", 1500, 2048);
        _simulator.CreateProcess("虚拟机", 4000, 8192);

        AnsiConsole.MarkupLine("[yellow]✓ 已加载 7 个模拟进程（高负载场景）[/]");
        AnsiConsole.WriteLine();

        // 主循环
        int tick = 0;
        while (_running && tick < 15)
        {
            tick++;
            AnsiConsole.MarkupLine($"[bold]╡ 系统时钟 #{tick} ╞[/]");
            
            // 1. 监控系统状态
            var state = _monitor.GetSystemState(_simulator);
            var diskState = new DiskState(
                UsagePercent: _random.Next(40, 90),
                QueueLength: _random.Next(1, 12),
                ReadSpeedMBps: _random.Next(50, 500),
                WriteSpeedMBps: _random.Next(50, 300)
            );
            var networkState = new NetworkState(
                TotalUsage: _random.Next(50, 98),
                DownloadSpeedMbps: _random.Next(10, 100),
                UploadSpeedMbps: _random.Next(5, 50),
                ActiveConnections: _random.Next(10, 100)
            );
            DisplaySystemState(state, diskState, networkState);

            // 2. AI 决策（增强版）
            var decisions = await MakeAIDecisions(state, diskState, networkState);
            DisplayDecisions(decisions);

            // 3. 执行决策
            ExecuteDecisions(decisions);

            // 4. 模拟时间流逝
            _simulator.Tick();

            await Task.Delay(800);
            AnsiConsole.WriteLine();
        }

        AnsiConsole.MarkupLine("[bold green]AIOS 演示完成![/]");
    }

    private readonly Random _random = new();

    private void DisplaySystemState(SystemState state, DiskState diskState, NetworkState networkState)
    {
        var table = new Table();
        table.Title("系统状态");
        table.AddColumn("指标");
        table.AddColumn("值");
        table.AddColumn("状态");

        table.AddRow("CPU 使用率", $"{state.CpuUsage:F1}%", GetStatus(state.CpuUsage));
        table.AddRow("内存使用率", $"{state.MemoryUsage:F1}%", GetStatus(state.MemoryUsage));
        table.AddRow("磁盘队列", diskState.QueueLength.ToString(), GetStatus(diskState.QueueLength * 10));
        table.AddRow("网络使用", $"{networkState.TotalUsage:F1}%", GetStatus(networkState.TotalUsage));
        table.AddRow("活跃进程", state.ActiveProcesses.ToString(), "✓");
        table.AddRow("等待队列", state.WaitingProcesses.ToString(), state.WaitingProcesses > 0 ? "[yellow]等待[/]" : "[green]空闲[/]");

        AnsiConsole.Write(table);
    }

    private string GetStatus(double value)
    {
        if (value < 50) return "[green]正常[/]";
        if (value < 80) return "[yellow]注意[/]";
        return "[red]警告[/]";
    }

    private async Task<List<AIDecision>> MakeAIDecisions(SystemState state, DiskState diskState, NetworkState networkState)
    {
        var decisions = new List<AIDecision>();
        var processes = _simulator.GetProcesses();

        // 1. 增强版 AI 调度器（ML）决策
        var enhancedDecisions = _enhancedScheduler.MakeDecisions(state, processes);
        decisions.AddRange(enhancedDecisions);

        // 2. AI 内存管理器决策
        var memoryDecision = _memoryManager.MakeDecision(state, processes);
        if (memoryDecision != null)
            decisions.Add(memoryDecision);

        // 3. AI 磁盘管理器决策
        var diskDecision = _diskManager.MakeDecision(state, processes, diskState);
        if (diskDecision != null)
            decisions.Add(diskDecision);

        // 4. AI 网络管理器决策
        var networkDecision = _networkManager.MakeDecision(state, processes, networkState);
        if (networkDecision != null)
            decisions.Add(networkDecision);

        // 5. 进程健康检查
        foreach (var process in processes.Where(p => p.State != ProcessState.Terminated))
        {
            var healthDecision = _healthChecker.MakeDecision(process, state);
            if (healthDecision != null)
                decisions.Add(healthDecision);
        }

        return decisions;
    }

    private void DisplayDecisions(List<AIDecision> decisions)
    {
        if (decisions.Count == 0)
        {
            AnsiConsole.MarkupLine("  [dim]AI 决策：无操作 (系统运行正常)[/]");
            return;
        }

        AnsiConsole.MarkupLine("  [bold cyan]AI 决策:[/]");
        foreach (var decision in decisions)
        {
            AnsiConsole.MarkupLine($"    → [cyan]{decision.Action}[/]: {decision.Target} (原因：{decision.Reason})");
        }
    }

    private void ExecuteDecisions(List<AIDecision> decisions)
    {
        foreach (var decision in decisions)
        {
            decision.Execute(_simulator);
        }
    }
}

/// <summary>
/// 系统状态快照
/// </summary>
public record SystemState(
    double CpuUsage,
    double MemoryUsage,
    int ActiveProcesses,
    int WaitingProcesses,
    double AverageResponseTime
);

/// <summary>
/// AI 决策
/// </summary>
public record AIDecision(
    string Action,
    string Target,
    string Reason,
    Action<SystemSimulator> Execute
);

/// <summary>
/// 系统模拟器
/// 模拟真实的系统资源
/// </summary>
public class SystemSimulator
{
    private readonly List<SimulatedProcess> _processes = new();
    private int _nextPid = 1;
    private readonly Random _random = new();

    public int CpuCores => 8;
    public int TotalMemory => 16384; // 16GB

    public void CreateProcess(string name, int cpuDemand, int memoryDemand)
    {
        var process = new SimulatedProcess
        {
            Pid = _nextPid++,
            Name = name,
            CpuDemand = cpuDemand, // 0-1000 (每核心的千分比)
            MemoryDemand = memoryDemand, // MB
            State = ProcessState.Ready,
            CreatedAt = DateTime.Now
        };
        _processes.Add(process);
        AnsiConsole.MarkupLine($"  [green]+[/] 进程创建：{name} (PID: {process.Pid}, CPU: {cpuDemand}, 内存：{memoryDemand}MB)");
    }

    public List<SimulatedProcess> GetProcesses() => new(_processes);

    public void Tick()
    {
        // 模拟进程执行
        foreach (var process in _processes.Where(p => p.State == ProcessState.Running))
        {
            process.CpuTime += 10;
            process.RemainingWork = Math.Max(0, process.RemainingWork - 10);
            if (process.RemainingWork == 0)
            {
                process.State = ProcessState.Terminated;
                AnsiConsole.MarkupLine($"  [dim]✓ 进程完成：{process.Name} (PID: {process.Pid})[/]");
            }
        }

        // 随机波动
        foreach (var process in _processes.Where(p => p.State == ProcessState.Ready))
        {
            process.CpuDemand = Math.Max(10, process.CpuDemand + _random.Next(-20, 20));
        }
    }

    public void AdjustProcessPriority(int pid, int newPriority)
    {
        var process = _processes.FirstOrDefault(p => p.Pid == pid);
        if (process != null)
        {
            process.Priority = newPriority;
            AnsiConsole.MarkupLine($"  [cyan]→ 优先级调整：{process.Name} → {newPriority}[/]");
        }
    }

    public void TerminateProcess(int pid)
    {
        var process = _processes.FirstOrDefault(p => p.Pid == pid);
        if (process != null)
        {
            process.State = ProcessState.Terminated;
            AnsiConsole.MarkupLine($"  [red]✗ 进程终止：{process.Name} (PID: {pid})[/]");
        }
    }

    public void AdjustProcessMemory(int pid, int newLimit)
    {
        var process = _processes.FirstOrDefault(p => p.Pid == pid);
        if (process != null)
        {
            process.MemoryLimit = newLimit;
            AnsiConsole.MarkupLine($"  [yellow]→ 内存限制：{process.Name} → {newLimit}MB[/]");
        }
    }

    public int GetTotalCpuDemand() => _processes.Where(p => p.State != ProcessState.Terminated).Sum(p => p.CpuDemand);
    public int GetTotalMemoryDemand() => _processes.Where(p => p.State != ProcessState.Terminated).Sum(p => p.MemoryDemand);
}

/// <summary>
/// 模拟进程
/// </summary>
public class SimulatedProcess
{
    public int Pid { get; set; }
    public string Name { get; set; } = "";
    public int Priority { get; set; } = 5; // 1-10
    public int CpuDemand { get; set; }
    public int MemoryDemand { get; set; }
    public int MemoryLimit { get; set; }
    public ProcessState State { get; set; } = ProcessState.Ready;
    public int RemainingWork { get; set; } = 100;
    public int CpuTime { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastScheduledAt { get; set; }
}

public enum ProcessState
{
    Ready,
    Running,
    Waiting,
    Terminated
}
