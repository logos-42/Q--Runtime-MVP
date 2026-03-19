# AI API 集成指南

## 概述

本项目现在支持调用外部大模型 API（OpenAI、Azure OpenAI、Claude 等）来增强量子任务调度能力。

## 架构设计

```
AIIntegration/
├── AIApiClient.cs          # API 客户端层（HTTP 请求、响应解析）
├── AIApiService.cs         # API 服务层（业务逻辑、Prompt 构建）
├── AISchedulerAdapter.cs   # 调度适配器（集成 API 和本地模型）
├── AIModels.cs             # 本地轻量级 ML 模型
├── AIApiDemo.cs            # API 功能演示
├── AIApiDemoStandalone.cs  # 独立演示程序
└── appsettings.json        # 配置文件
```

## 快速开始

### 1. 设置 API Key

**方式一：环境变量（推荐）**
```bash
# Windows PowerShell
$env:OPENAI_API_KEY="sk-your-api-key-here"

# Windows CMD
set OPENAI_API_KEY=sk-your-api-key-here

# Linux/Mac
export OPENAI_API_KEY=sk-your-api-key-here
```

**方式二：编辑 appsettings.json**
```json
{
  "AIApiSettings": {
    "Enabled": true,
    "ApiKey": "sk-your-api-key-here",
    "Model": "gpt-3.5-turbo"
  }
}
```

### 2. 运行演示

```bash
cd AIIntegration
dotnet run
```

## 支持的 API 提供商

### OpenAI
```json
{
  "Provider": "OpenAI",
  "Endpoint": "https://api.openai.com/v1/chat/completions",
  "Model": "gpt-3.5-turbo"
}
```

### Azure OpenAI
```json
{
  "Provider": "Azure",
  "Endpoint": "https://YOUR_RESOURCE.openai.azure.com/openai/deployments/YOUR_DEPLOYMENT/chat/completions?api-version=2023-05-15",
  "Model": "gpt-35-turbo"
}
```

### Anthropic (Claude)
```json
{
  "Provider": "Anthropic",
  "Endpoint": "https://api.anthropic.com/v1/messages",
  "Model": "claude-3-sonnet-20240229"
}
```

### 自定义兼容 API
```json
{
  "Provider": "Custom",
  "Endpoint": "https://your-api.com/v1/chat/completions",
  "Model": "custom-model"
}
```

## 核心功能

### 1. 任务优先级评估

使用 AI 分析量子任务的优先级，考虑因素：
- T 门数量（关键资源）
- 电路深度
- Qubit 占用
- 等待时间

```csharp
var tasks = new List<TaskInfoForAI> { ... };
var priorities = await apiService.EvaluateTaskPrioritiesAsync(tasks);
```

### 2. 电路优化建议

获取 AI 推荐的量子电路优化方案：
- T 门减少
- CNOT 消除
- 并行化
- 算法特定优化

```csharp
var circuit = new CircuitInfo("Grover", "Grover Search", 35, 24, 18, 3);
var suggestions = await apiService.GetOptimizationSuggestionsAsync(circuit);
```

### 3. SWAP 成本分析

分析量子硬件拓扑上的 SWAP 操作成本：

```csharp
var (swapCount, costMultiplier) = await apiService.AnalyzeSWAPCostAsync(0, 4, "linear");
```

### 4. 智能调度决策

综合 AI 分析和本地模型进行任务调度：

```csharp
var scheduler = new AISchedulerAdapter
{
    Mode = OperationMode.APIEnhanced,
    EnhancedConfig = new APIEnhancedConfig { APIWeight = 0.6f }
};
```

## 操作模式

| 模式 | 说明 |
|------|------|
| `Rule` | 纯规则引擎（向后兼容） |
| `AI` | 本地 ML 模型 |
| `Hybrid` | AI + 规则混合（默认） |
| `APIEnhanced` | API 增强的 AI 调度 |

## 降级策略

当 API 不可用时，系统自动降级：
1. API 调用失败 → 使用本地 ML 模型
2. 本地模型不可用 → 使用规则引擎
3. 始终保持基本调度功能

## 性能优化

### 缓存机制
- API 结果缓存 5-10 分钟
- 避免重复调用
- 支持异步预加载

### 超时控制
```csharp
EnhancedConfig = new APIEnhancedConfig
{
    TimeoutMs = 5000,      // 5 秒超时
    AsyncMode = true       // 异步模式
};
```

## 代码示例

### 完整集成示例

```csharp
using AIIntegration.AI;
using AIIntegration.Scheduler;

// 1. 创建配置
var config = new AIApiConfig
{
    Enabled = true,
    Provider = ApiProvider.OpenAI,
    ApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY"),
    Model = "gpt-3.5-turbo"
};

// 2. 初始化服务
var apiService = new AIApiService(config);

// 3. 测试连接
var connected = await apiService.TestConnectionAsync();

// 4. 创建调度器
var scheduler = new AISchedulerAdapter
{
    Mode = OperationMode.APIEnhanced
};
scheduler.SetApiService(apiService);

// 5. 使用
var tasks = GetTaskQueue();
await scheduler.RefreshPrioritiesAsync(tasks);

foreach (var task in tasks)
{
    var score = scheduler.ComputeTaskScore(task, currentTime);
    Console.WriteLine($"任务 {task.Id}: {score:F3}");
}

// 6. 清理
apiService.Dispose();
```

## 环境变量

| 变量名 | 说明 |
|--------|------|
| `OPENAI_API_KEY` | OpenAI API Key |
| `AZURE_API_KEY` | Azure OpenAI API Key |
| `ANTHROPIC_API_KEY` | Anthropic API Key |

## 故障排除

### API 连接失败
1. 检查 API Key 是否正确
2. 验证网络连接
3. 确认 Endpoint URL 正确

### 响应解析失败
1. 检查模型是否支持 JSON 格式
2. 调整 Temperature 参数（建议 0.3-0.5）
3. 查看日志中的原始响应

### 性能问题
1. 启用 AsyncMode 减少阻塞
2. 增加缓存时间
3. 使用本地模型作为主要调度

## 安全建议

1. **不要硬编码 API Key** - 使用环境变量或密钥管理服务
2. **限制 API 调用频率** - 避免超出配额
3. **监控 Token 使用** - 设置预算告警

## 扩展开发

### 添加新的 API 提供商

在 `AIApiClient.cs` 中添加：
```csharp
public enum ApiProvider
{
    OpenAI,
    Azure,
    Anthropic,
    Custom,
    YourNewProvider  // 添加这里
}
```

### 自定义 Prompt 模板

在 `QuantumTaskPromptBuilder` 类中添加新的 Prompt 构建方法。

## 参考资料

- [OpenAI API 文档](https://platform.openai.com/docs)
- [Azure OpenAI 文档](https://learn.microsoft.com/azure/ai-services/openai/)
- [Anthropic API 文档](https://docs.anthropic.com/claude/docs)
