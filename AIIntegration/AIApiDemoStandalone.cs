using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AIIntegration.AI;
using AIIntegration.Scheduler;
using SchedulerTask = AIIntegration.Scheduler.Task;

namespace AIIntegration.Demo
{
    /// <summary>
    /// API 调用功能独立演示程序
    /// 展示如何使用大模型 API 增强量子任务调度
    /// </summary>
    class AIApiDemoStandalone
    {
        static void Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            
            Console.WriteLine("╔════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║           AI API 调用功能演示                              ║");
            Console.WriteLine("║        集成 OpenAI/Azure/Claude 等大模型 API               ║");
            Console.WriteLine("╚════════════════════════════════════════════════════════════╝\n");

            // 创建 API 配置
            var config = CreateApiConfig();
            
            if (!config.Enabled)
            {
                Console.WriteLine("\n⚠ API 未启用配置");
                Console.WriteLine("请设置环境变量 OPENAI_API_KEY 或编辑 appsettings.json");
                Console.WriteLine("\n按任意键以本地模式继续，或输入 'q' 退出...");
                var key = Console.ReadKey();
                if (key.KeyChar == 'q' || key.KeyChar == 'Q')
                    return;
            }

            // 初始化 API 服务
            var apiService = new AIApiService(config);
            
            // 创建调度器
            var scheduler = CreateSchedulerWithApi(apiService);

            // 显示主菜单
            ShowMainMenu(apiService, scheduler).Wait();
            
            apiService?.Dispose();
            Console.WriteLine("\n演示结束");
        }

        private static AIApiConfig CreateApiConfig()
        {
            // 从环境变量读取
            var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? "";
            
            // 从配置文件的逻辑（简化版）
            if (string.IsNullOrEmpty(apiKey))
            {
                // 尝试读取 appsettings.json（如果有）
                var appSettingsPath = System.IO.Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory, 
                    "appsettings.json");
                
                if (System.IO.File.Exists(appSettingsPath))
                {
                    Console.WriteLine("[配置] 找到 appsettings.json");
                    // 这里可以添加 JSON 解析逻辑
                }
            }

            return new AIApiConfig
            {
                Enabled = !string.IsNullOrEmpty(apiKey),
                Provider = ApiProvider.OpenAI,
                ApiKey = apiKey,
                Endpoint = "https://api.openai.com/v1/chat/completions",
                Model = "gpt-3.5-turbo",
                TimeoutSeconds = 30,
                OptimizationOnly = true
            };
        }

        private static AISchedulerAdapter CreateSchedulerWithApi(AIApiService apiService)
        {
            var scheduler = new AISchedulerAdapter
            {
                Mode = OperationMode.APIEnhanced,
                EnhancedConfig = new APIEnhancedConfig
                {
                    APIWeight = 0.6f,
                    UseAPIForPriority = true,
                    AsyncMode = true,
                    TimeoutMs = 5000
                }
            };

            scheduler.SetApiService(apiService);
            
            Console.WriteLine("[调度器] API 增强模式已就绪");
            return scheduler;
        }

        private static async System.Threading.Tasks.Task ShowMainMenu(AIApiService apiService, AISchedulerAdapter scheduler)
        {
            while (true)
            {
                Console.WriteLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                Console.WriteLine("【主菜单】");
                Console.WriteLine("  1. 测试 API 连接");
                Console.WriteLine("  2. 任务优先级评估 (API)");
                Console.WriteLine("  3. 电路优化建议 (API)");
                Console.WriteLine("  4. SWAP 成本分析 (API)");
                Console.WriteLine("  5. 完整调度演示");
                Console.WriteLine("  6. 查看诊断信息");
                Console.WriteLine("  0. 退出");
                Console.Write("\n请选择 [0-6]: ");
                
                var choice = Console.ReadLine()?.Trim();
                
                switch (choice)
                {
                    case "1":
                        await TestApiConnection(apiService);
                        break;
                    case "2":
                        await DemonstrateTaskPriority(apiService);
                        break;
                    case "3":
                        await DemonstrateOptimization(apiService);
                        break;
                    case "4":
                        await DemonstrateSwapAnalysis(apiService);
                        break;
                    case "5":
                        await RunFullSchedulingDemo(scheduler, apiService);
                        break;
                    case "6":
                        scheduler.PrintDiagnostics();
                        break;
                    case "0":
                        return;
                    default:
                        Console.WriteLine("无效选择，请重试");
                        break;
                }
            }
        }

        private static async System.Threading.Tasks.Task TestApiConnection(AIApiService apiService)
        {
            Console.WriteLine("\n=== 测试 API 连接 ===");
            
            if (!apiService.IsAvailable)
            {
                Console.WriteLine("⚠ API 服务未启用");
                Console.WriteLine("请检查：");
                Console.WriteLine("  1. OPENAI_API_KEY 环境变量是否设置");
                Console.WriteLine("  2. appsettings.json 配置是否正确");
                Console.WriteLine("  3. 网络连接是否正常");
                return;
            }

            Console.WriteLine("正在连接...");
            var success = await apiService.TestConnectionAsync();
            
            Console.WriteLine(success ? "✓ 连接成功！" : "✗ 连接失败");
        }

        private static async System.Threading.Tasks.Task DemonstrateTaskPriority(AIApiService apiService)
        {
            Console.WriteLine("\n=== 任务优先级评估 ===");
            
            var tasks = new[]
            {
                new TaskInfoForAI(1, "Grover 搜索", 25, 32, 8, 10.5f),
                new TaskInfoForAI(2, "量子傅里叶变换", 40, 12, 6, 5.2f),
                new TaskInfoForAI(3, "VQE 优化", 15, 8, 4, 25.0f),
                new TaskInfoForAI(4, "量子隐形传态", 10, 4, 3, 2.1f)
            };

            Console.WriteLine("\n待评估任务:");
            foreach (var task in tasks)
            {
                Console.WriteLine($"  [{task.Id}] {task.Name}: D={task.Depth}, T={task.TGateCount}, Q={task.QubitCount}");
            }

            if (!apiService.IsAvailable)
            {
                Console.WriteLine("\n⚠ API 未启用，使用本地模型估算");
                // 演示本地模型
                Console.WriteLine("\n本地模型估算结果:");
                foreach (var task in tasks)
                {
                    var score = 0.5f + (task.TGateCount / 100f) * 0.3f + (task.WaitTime / 100f) * 0.2f;
                    Console.WriteLine($"  任务 {task.Id}: {Math.Min(score, 1.0f):F3}");
                }
                return;
            }

            Console.WriteLine("\n正在请求 AI 评估...");
            var priorities = await apiService.EvaluateTaskPrioritiesAsync(new List<TaskInfoForAI>(tasks));

            Console.WriteLine("\n评估结果:");
            foreach (var kvp in priorities)
            {
                Console.WriteLine($"  任务 {kvp.Key}: 优先级 = {kvp.Value:F3}");
            }
        }

        private static async System.Threading.Tasks.Task DemonstrateOptimization(AIApiService apiService)
        {
            Console.WriteLine("\n=== 电路优化建议 ===");
            
            var circuit = new CircuitInfo(
                "Grover-3-Qubit",
                "Grover Search",
                35,
                24,
                18,
                3
            );

            Console.WriteLine($"\n电路：{circuit.Name} ({circuit.AlgorithmType})");
            Console.WriteLine($"深度={circuit.Depth}, T 门={circuit.TGateCount}, CNOT={circuit.CNOTCount}, Qubits={circuit.QubitCount}");

            if (!apiService.IsAvailable)
            {
                Console.WriteLine("\n⚠ API 未启用，显示规则建议");
                Console.WriteLine("\n规则引擎建议:");
                if (circuit.TGateCount > 15)
                    Console.WriteLine("  • T 门数量较多，考虑 Clifford+T 分解优化");
                if (circuit.CNOTCount > 10)
                    Console.WriteLine("  • CNOT 数量较多，检查是否有可消除的门对");
                if (circuit.Depth > 25)
                    Console.WriteLine("  • 电路深度较大，尝试增加并行度");
                return;
            }

            Console.WriteLine("\n正在请求优化建议...");
            var suggestions = await apiService.GetOptimizationSuggestionsAsync(circuit);

            Console.WriteLine("\n优化建议:");
            if (suggestions.Count == 0)
            {
                Console.WriteLine("  (无建议)");
            }
            else
            {
                foreach (var s in suggestions)
                {
                    Console.WriteLine($"  [{s.Priority}] {s.Type}");
                    Console.WriteLine($"      {s.Description} (改进：{s.EstimatedImprovement * 100:F0}%)");
                }
            }
        }

        private static async System.Threading.Tasks.Task DemonstrateSwapAnalysis(AIApiService apiService)
        {
            Console.WriteLine("\n=== SWAP 成本分析 ===");
            
            int srcQubit = 0, dstQubit = 4;
            Console.WriteLine($"源 Qubit: {srcQubit} → 目标 Qubit: {dstQubit}");
            Console.WriteLine("拓扑：线性 (0-1-2-3-4)");

            if (!apiService.IsAvailable)
            {
                var distance = Math.Abs(dstQubit - srcQubit);
                var cost = (float)Math.Pow(1.3, distance - 1);
                Console.WriteLine($"\n本地模型估算:");
                Console.WriteLine($"  SWAP 数量：{distance}");
                Console.WriteLine($"  成本倍数：{cost:F2}x");
                return;
            }

            var (swapCount, costMultiplier) = await apiService.AnalyzeSWAPCostAsync(srcQubit, dstQubit, "linear");

            Console.WriteLine($"\nAI 分析结果:");
            Console.WriteLine($"  SWAP 数量：{swapCount}");
            Console.WriteLine($"  成本倍数：{costMultiplier:F2}x");
        }

        private static async System.Threading.Tasks.Task RunFullSchedulingDemo(AISchedulerAdapter scheduler, AIApiService apiService)
        {
            Console.WriteLine("\n=== 完整调度演示 ===");
            
            var tasks = new[]
            {
                new SchedulerTask(1, "QFT", new CircuitBlock("QFT", 30, 20, 5), TaskPriority.Normal, 0),
                new SchedulerTask(2, "Grover", new CircuitBlock("Grover", 45, 40, 6), TaskPriority.High, 0),
                new SchedulerTask(3, "VQE", new CircuitBlock("VQE", 20, 10, 4), TaskPriority.Low, 0),
                new SchedulerTask(4, "Teleport", new CircuitBlock("Teleport", 12, 6, 3), TaskPriority.Critical, 0)
            };

            Console.WriteLine("\n任务队列:");
            foreach (var t in tasks)
                Console.WriteLine($"  [{t.Id}] {t.Name}: P={t.Priority}, D={t.Circuit.Depth}, T={t.Circuit.TGateCount}");

            // 预加载
            if (apiService.IsAvailable)
            {
                Console.WriteLine("\n正在预加载 AI 优先级...");
                await scheduler.RefreshPrioritiesAsync(new List<SchedulerTask>(tasks));
            }

            // 评分
            Console.WriteLine("\n任务评分 (时间=100):");
            SchedulerTask? bestTask = null;
            float bestScore = float.MinValue;
            
            foreach (var t in tasks)
            {
                var score = scheduler.ComputeTaskScore(t, 100f);
                Console.WriteLine($"  [{t.Id}] {t.Name}: {score:F3}");
                if (score > bestScore)
                {
                    bestScore = score;
                    bestTask = t;
                }
            }

            if (bestTask != null)
                Console.WriteLine($"\n✓ 选择执行：[{bestTask.Id}] {bestTask.Name} (评分={bestScore:F3})");

            Console.WriteLine("\n【诊断信息】");
            scheduler.PrintDiagnostics();
        }
    }
}
