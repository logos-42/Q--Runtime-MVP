using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AIIntegration.AI;
using AIIntegration.Scheduler;
using SchedulerTask = AIIntegration.Scheduler.Task;
using ThreadingTask = System.Threading.Tasks.Task;

namespace AIIntegration.Demo
{
    /// <summary>
    /// API 调用功能演示
    /// 展示如何使用大模型 API 增强量子任务调度
    /// </summary>
    public class AIApiDemo
    {
        public static void RunDemo()
        {
            Console.WriteLine("╔════════════════════════════════════════════╗");
            Console.WriteLine("║     AI API 调用功能演示                    ║");
            Console.WriteLine("╚════════════════════════════════════════════╝");
            Console.WriteLine();

            // 1. 创建 API 配置
            var config = CreateApiConfig();
            
            // 2. 初始化 API 服务
            var apiService = new AIApiService(config);
            
            // 3. 创建调度器并集成 API
            var scheduler = CreateSchedulerWithApi(apiService);
            
            // 4. 演示功能
            Console.WriteLine("\n=== 演示菜单 ===");
            Console.WriteLine("1. 测试 API 连接");
            Console.WriteLine("2. 任务优先级评估");
            Console.WriteLine("3. 电路优化建议");
            Console.WriteLine("4. SWAP 成本分析");
            Console.WriteLine("5. 完整调度演示");
            Console.WriteLine("0. 退出");
            Console.WriteLine();
            Console.Write("请选择 (0-5): ");
            
            var choice = Console.ReadLine();
            
            switch (choice)
            {
                case "1":
                    TestApiConnection(apiService).Wait();
                    break;
                case "2":
                    DemonstrateTaskPriority(apiService).Wait();
                    break;
                case "3":
                    DemonstrateOptimization(apiService).Wait();
                    break;
                case "4":
                    DemonstrateSwapAnalysis(apiService).Wait();
                    break;
                case "5":
                    RunFullSchedulingDemo(scheduler, apiService).Wait();
                    break;
                default:
                    Console.WriteLine("已退出演示");
                    break;
            }
            
            apiService?.Dispose();
        }

        /// <summary>
        /// 创建 API 配置
        /// </summary>
        private static AIApiConfig CreateApiConfig()
        {
            // 从环境变量读取 API Key（推荐方式）
            var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? "";
            
            // 或者从配置文件读取
            if (string.IsNullOrEmpty(apiKey))
            {
                Console.WriteLine("[配置] 未找到 OPENAI_API_KEY 环境变量");
                Console.WriteLine("[配置] 请在 appsettings.json 中配置 API Key 或设置环境变量");
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

        /// <summary>
        /// 创建集成 API 的调度器
        /// </summary>
        private static AISchedulerAdapter CreateSchedulerWithApi(AIApiService apiService)
        {
            var scheduler = new AISchedulerAdapter
            {
                Mode = OperationMode.APIEnhanced,
                EnhancedConfig = new APIEnhancedConfig
                {
                    APIWeight = 0.6f,
                    UseAPIForPriority = true,
                    AsyncMode = true
                }
            };

            scheduler.SetApiService(apiService);
            
            Console.WriteLine("[调度器] 已创建 API 增强模式调度器");
            return scheduler;
        }

        /// <summary>
        /// 1. 测试 API 连接
        /// </summary>
        private static async System.Threading.Tasks.Task TestApiConnection(AIApiService apiService)
        {
            Console.WriteLine("\n=== 测试 API 连接 ===");
            
            if (!apiService.IsAvailable)
            {
                Console.WriteLine("[状态] API 服务未启用，请检查配置");
                return;
            }

            var success = await apiService.TestConnectionAsync();
            
            if (success)
            {
                Console.WriteLine("✓ 连接成功！");
            }
            else
            {
                Console.WriteLine("✗ 连接失败，请检查 API Key 和网络");
            }
        }

        /// <summary>
        /// 2. 任务优先级评估演示
        /// </summary>
        private static async System.Threading.Tasks.Task DemonstrateTaskPriority(AIApiService apiService)
        {
            Console.WriteLine("\n=== 任务优先级评估 ===");
            
            if (!apiService.IsAvailable)
            {
                Console.WriteLine("[状态] API 服务未启用，使用本地模型");
                return;
            }

            var tasks = new List<TaskInfoForAI>
            {
                new TaskInfoForAI(1, "Grover 搜索", 25, 32, 8, 10.5f),
                new TaskInfoForAI(2, "量子傅里叶变换", 40, 12, 6, 5.2f),
                new TaskInfoForAI(3, "VQE 优化", 15, 8, 4, 25.0f),
                new TaskInfoForAI(4, "量子隐形传态", 10, 4, 3, 2.1f)
            };

            Console.WriteLine("待评估任务:");
            foreach (var task in tasks)
            {
                Console.WriteLine($"  [{task.Id}] {task.Name}: 深度={task.Depth}, T 门={task.TGateCount}, Qubits={task.QubitCount}");
            }

            Console.WriteLine("\n正在请求 AI 评估...");
            var priorities = await apiService.EvaluateTaskPrioritiesAsync(tasks);

            Console.WriteLine("\n评估结果:");
            foreach (var kvp in priorities)
            {
                Console.WriteLine($"  任务 {kvp.Key}: 优先级 = {kvp.Value:F3}");
            }
        }

        /// <summary>
        /// 3. 电路优化建议演示
        /// </summary>
        private static async System.Threading.Tasks.Task DemonstrateOptimization(AIApiService apiService)
        {
            Console.WriteLine("\n=== 电路优化建议 ===");
            
            if (!apiService.IsAvailable)
            {
                Console.WriteLine("[状态] API 服务未启用");
                return;
            }

            var circuit = new CircuitInfo(
                "Grover-3-Qubit",
                "Grover Search",
                35,
                24,
                18,
                3
            );

            Console.WriteLine($"电路：{circuit.Name}");
            Console.WriteLine($"类型：{circuit.AlgorithmType}");
            Console.WriteLine($"深度={circuit.Depth}, T 门={circuit.TGateCount}, CNOT={circuit.CNOTCount}, Qubits={circuit.QubitCount}");

            Console.WriteLine("\n正在请求优化建议...");
            var suggestions = await apiService.GetOptimizationSuggestionsAsync(circuit);

            Console.WriteLine("\n优化建议:");
            if (suggestions.Count == 0)
            {
                Console.WriteLine("  (无建议)");
            }
            else
            {
                foreach (var suggestion in suggestions)
                {
                    Console.WriteLine($"  [{suggestion.Priority}] {suggestion.Type}");
                    Console.WriteLine($"      {suggestion.Description}");
                    Console.WriteLine($"      预期改进：{suggestion.EstimatedImprovement * 100:F0}%");
                }
            }
        }

        /// <summary>
        /// 4. SWAP 成本分析演示
        /// </summary>
        private static async System.Threading.Tasks.Task DemonstrateSwapAnalysis(AIApiService apiService)
        {
            Console.WriteLine("\n=== SWAP 成本分析 ===");
            
            if (!apiService.IsAvailable)
            {
                Console.WriteLine("[状态] API 服务未启用，使用本地模型");
                return;
            }

            int srcQubit = 0;
            int dstQubit = 4;
            
            Console.WriteLine($"源 Qubit: {srcQubit}");
            Console.WriteLine($"目标 Qubit: {dstQubit}");
            Console.WriteLine($"拓扑：线性 (0-1-2-3-4)");

            Console.WriteLine("\n正在分析...");
            var (swapCount, costMultiplier) = await apiService.AnalyzeSWAPCostAsync(srcQubit, dstQubit, "linear");

            Console.WriteLine($"\n分析结果:");
            Console.WriteLine($"  预计 SWAP 数量：{swapCount}");
            Console.WriteLine($"  成本倍数：{costMultiplier:F2}x");
        }

        /// <summary>
        /// 5. 完整调度演示
        /// </summary>
        private static async System.Threading.Tasks.Task RunFullSchedulingDemo(AISchedulerAdapter scheduler, AIApiService apiService)
        {
            Console.WriteLine("\n=== 完整调度演示 ===");
            
            // 创建模拟任务队列
            var tasks = new List<SchedulerTask>
            {
                new SchedulerTask(1, "任务 A", new CircuitBlock("QFT", 30, 20, 5), TaskPriority.Normal, 0),
                new SchedulerTask(2, "任务 B", new CircuitBlock("Grover", 45, 40, 6), TaskPriority.High, 0),
                new SchedulerTask(3, "任务 C", new CircuitBlock("VQE", 20, 10, 4), TaskPriority.Low, 0),
                new SchedulerTask(4, "任务 D", new CircuitBlock("Teleport", 12, 6, 3), TaskPriority.Critical, 0)
            };

            Console.WriteLine("任务队列:");
            foreach (var task in tasks)
            {
                Console.WriteLine($"  [{task.Id}] {task.Name}: 优先级={task.Priority}, 深度={task.Circuit.Depth}, T 门={task.Circuit.TGateCount}");
            }

            // 如果 API 可用，预加载优先级
            if (apiService.IsAvailable)
            {
                Console.WriteLine("\n正在预加载 AI 优先级...");
                await scheduler.RefreshPrioritiesAsync(tasks);
            }

            // 计算每个任务的评分
            Console.WriteLine("\n任务评分:");
            foreach (var task in tasks)
            {
                var score = scheduler.ComputeTaskScore(task, 100f);  // 假设当前时间=100
                Console.WriteLine($"  [{task.Id}] {task.Name}: 评分 = {score:F3}");
            }

            // 选择最高分任务
            var bestTask = tasks.OrderByDescending(t => scheduler.ComputeTaskScore(t, 100f)).First();
            Console.WriteLine($"\n✓ 选择执行：[{bestTask.Id}] {bestTask.Name}");

            // 打印诊断信息
            scheduler.PrintDiagnostics();
        }
    }
}
