using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using AIIntegration.Scheduler;
using AIIntegration.Runtime;
using AIIntegration.Silq;

namespace AIIntegration.Demo
{
    /// <summary>
    /// Phase 3 Demo: AI增强调度器演示
    /// 对比三种模式：Rule / AI / Hybrid
    /// </summary>
    class Phase3Demo
    {
        static void Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            
            // 菜单选择
            while (true)
            {
                Console.WriteLine("\n╔════════════════════════════════════════════════════════════╗");
                Console.WriteLine("║         Phase 3: AI 增强量子运行时系统演示                  ║");
                Console.WriteLine("╚════════════════════════════════════════════════════════════╝");
                Console.WriteLine("\n请选择演示场景:");
                Console.WriteLine("1. Phase 3A: AI 调度器（C# 电路）");
                Console.WriteLine("2. Phase 3B: Silq + AI 集成（Silq 电路优化）");
                Console.WriteLine("0. 退出");
                Console.Write("\n选项: ");
                
                var choice = Console.ReadLine();
                
                switch (choice)
                {
                    case "1":
                        Phase3ADemo();
                        break;
                    case "2":
                        SilqAIDemo.SilqMain(args);
                        break;
                    case "0":
                        return;
                    default:
                        Console.WriteLine("无效选项");
                        break;
                }
            }
        }
        
        static void Phase3ADemo()
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.WriteLine("\n╔════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║  Phase 3A: AI增强量子调度器演示                              ║");
            Console.WriteLine("║  对比 Rule / AI / Hybrid 三种模式                          ║");
            Console.WriteLine("╚════════════════════════════════════════════════════════════╝\n");
            
            // 创建测试电路
            var testCircuits = CreateTestCircuits();
            
            // 运行三种模式的演示
            Console.WriteLine("【场景】五并发任务，涉及大量T门，不同复杂度");
            Console.WriteLine("═══════════════════════════════════════\n");
            
            var ruleResult = RunDemo(testCircuits, OperationMode.Rule);
            Console.WriteLine("\n");
            
            var aiResult = RunDemo(testCircuits, OperationMode.AI);
            Console.WriteLine("\n");
            
            var hybridResult = RunDemo(testCircuits, OperationMode.Hybrid);
            Console.WriteLine("\n");
            
            // 性能对比
            PrintComparison(ruleResult, aiResult, hybridResult);
            
            Console.WriteLine("\n\n按任意键退出...");
            Console.ReadKey();
        }
        
        static List<CircuitBlock> CreateTestCircuits()
        {
            return new List<CircuitBlock>
            {
                new("Task-1-VQE", Depth: 15, TGateCount: 20, QubitCount: 8),
                new("Task-2-QAOA", Depth: 25, TGateCount: 45, QubitCount: 10),
                new("Task-3-Grover", Depth: 10, TGateCount: 5, QubitCount: 5),
                new("Task-4-Simulation", Depth: 35, TGateCount: 80, QubitCount: 12),
                new("Task-5-Optimization", Depth: 20, TGateCount: 30, QubitCount: 8)
            };
        }
        
        static DemoResult RunDemo(List<CircuitBlock> circuits, OperationMode mode)
        {
            Console.WriteLine($"【{mode}模式】运行中...");
            Console.WriteLine("─────────────────────");
            
            var stopwatch = Stopwatch.StartNew();
            var scheduler = new AIEnhancedScheduler(numQubits: 20, mode: mode);
            
            // 提交任务
            for (int i = 0; i < circuits.Count; i++)
            {
                scheduler.SubmitTask(
                    taskId: i,
                    name: circuits[i].Name,
                    circuit: circuits[i],
                    priority: DetermineRandomPriority(i)
                );
            }
            
            // 模拟执行队列（简化版）
            int completedCount = 0;
            var executedTasks = new List<(string name, float time)>();
            
            while (completedCount < circuits.Count)
            {
                var nextIdx = scheduler.SelectNextTask();
                if (nextIdx < 0) break;
                
                var task = scheduler.GetTask(nextIdx);
                if (task == null) break;
                
                // 模拟执行时间 = 深度 + T门数*1.5（T门开销大）
                var executionTime = task.Circuit.Depth * 0.5f + task.Circuit.TGateCount * 1.5f;
                
                // 故障检测
                var (isSafe, riskyQubits) = scheduler.ValidateTaskQubits(task);
                var safetyMarker = isSafe ? "✓" : "⚠ ";
                
                Console.WriteLine($"  {safetyMarker} {task.Name}: 深度={task.Circuit.Depth} " +
                                $"T门={task.Circuit.TGateCount} 执行时间={executionTime:F1}");
                
                // 事实中任何qubits问题都记假设成功（演示）
                scheduler.ExecuteTask(task, executionTime);
                executedTasks.Add((task.Name, executionTime));
                completedCount++;
            }
            
            stopwatch.Stop();
            
            // 打印统计
            scheduler.PrintStatistics();
            
            return new DemoResult
            {
                Mode = mode,
                ElapsedMs = stopwatch.ElapsedMilliseconds,
                CompletedTasks = completedCount,
                TotalExecutionTime = executedTasks.Count > 0 
                    ? executedTasks.Sum(x => x.time)
                    : 0f,
                ExecutedTasks = executedTasks
            };
        }
        
        static TaskPriority DetermineRandomPriority(int index)
        {
            return (index % 3) switch
            {
                0 => TaskPriority.Critical,
                1 => TaskPriority.High,
                _ => TaskPriority.Normal
            };
        }
        
        static void PrintComparison(DemoResult rule, DemoResult ai, DemoResult hybrid)
        {
            Console.WriteLine("╔════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║                    性能对比分析                            ║");
            Console.WriteLine("╚════════════════════════════════════════════════════════════╝");
            
            Console.WriteLine("\n【执行时间】（毫秒）");
            Console.WriteLine($"  Rule模式:   {rule.ElapsedMs:D5} ms");
            Console.WriteLine($"  AI模式:     {ai.ElapsedMs:D5} ms (-{((rule.ElapsedMs - ai.ElapsedMs) * 100f / rule.ElapsedMs):F1}%)");
            Console.WriteLine($"  Hybrid模式: {hybrid.ElapsedMs:D5} ms (-{((rule.ElapsedMs - hybrid.ElapsedMs) * 100f / rule.ElapsedMs):F1}%)");
            
            Console.WriteLine("\n【任务总执行时间】（相对单位）");
            Console.WriteLine($"  Rule模式:   {rule.TotalExecutionTime:F1}");
            Console.WriteLine($"  AI模式:     {ai.TotalExecutionTime:F1} ({GetChangePercent(rule.TotalExecutionTime, ai.TotalExecutionTime)}%)");
            Console.WriteLine($"  Hybrid模式: {hybrid.TotalExecutionTime:F1} ({GetChangePercent(rule.TotalExecutionTime, hybrid.TotalExecutionTime)}%)");
            
            Console.WriteLine("\n【评估】");
            if (hybrid.ElapsedMs < rule.ElapsedMs * 0.9f)
                Console.WriteLine("✓ Hybrid模式显著提升调度效率（>10%）");
            else if (hybrid.ElapsedMs < rule.ElapsedMs)
                Console.WriteLine("• Hybrid模式小幅提升效率");
            else
                Console.WriteLine("○ 各模式性能相近（数据规模太小）");
            
            Console.WriteLine("\n【推荐】");
            Console.WriteLine("→ 生产环境建议采用 Hybrid 模式");
            Console.WriteLine("  - 保留规则引擎的可预测性（30%）");
            Console.WriteLine("  - 充分发挥AI的自适应优化（70%）");
            Console.WriteLine("  - 若AI失败可自动降级到规则模式");
        }
        
        static string GetChangePercent(float baseline, float value)
        {
            if (baseline == 0) return "N/A";
            var percent = (value - baseline) / baseline * 100f;
            var sign = percent >= 0 ? "+" : "";
            return $"{sign}{percent:F1}";
        }
    }
    
    class DemoResult
    {
        public OperationMode Mode { get; set; }
        public long ElapsedMs { get; set; }
        public int CompletedTasks { get; set; }
        public float TotalExecutionTime { get; set; }
        public List<(string name, float time)> ExecutedTasks { get; set; } = new();
    }
}
