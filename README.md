# Q# Runtime MVP

量子-经典混合 Runtime 原型，使用 Q# 实现。

## 核心设计理念

研究**经典系统如何调度和管理量子资源**，追求结构复杂度而非算法复杂度：

- 抽象 qubit 的生命周期
- 管理资源约束
- 实现可逆计算的追踪
- 优化任务调度

## 项目结构

```
QuantumRuntime/
├── QubitPool.qs     # Qubit 资源池管理
├── CircuitIR.qs     # 电路中间表示
├── Scheduler.qs     # 任务调度器
├── Program.qs       # 主程序
└── README.md        # 项目说明
```

### 模块说明

| 文件 | 功能 |
|------|------|
| QubitPool.qs | qubit 状态追踪（Free→Allocated→InUse→Released） |
| CircuitIR.qs | 电路 IR，支持嵌套和资源成本统计 |
| Scheduler.qs | 任务队列、优先级调度、qubit 分配 |
| Program.qs | 完整演示流程 |

## 运行

```bash
cd QuantumRuntime
dotnet build
dotnet run
```

## 实验目标

与 Silq 对比研究：
# 量子—经典混合 Runtime（Q--Runtime MVP）

这是一个面向工程与研究的量子-经典混合运行时原型，演示如何将经典操作系统/调度思想应用到量子任务与资源管理中。当前实现包含：

- Phase 1: 基础运行时（Qubit 池、Circuit IR、基础调度）
- Phase 2: 并发调度与拓扑感知的电路优化（SWAP 插入、冲突检测）
- Phase 3: AI 增强（智能调度、SWAP 成本自适应、Silq 源码静态分析与优化建议）

## AI 在本项目中的作用

AI 被集成为调度与优化的决策层，主要职责：

- 任务优先级预测：基于电路特征（深度、T 门数、等待时间等）预测应优先执行的任务；
- SWAP 成本预测：在线学习具体硬件拓扑下实际 SWAP 开销，替代静态距离估计；
- 资源/时间预测：估计任务执行时间与峰值 qubit 占用，辅助排期；
- 故障预测：监控并给出高风险 qubit 列表，供调度器回避或降级。

系统提供三种运行模式：`rule`（仅规则）、`ai`（仅 AI）、`hybrid`（推荐，70% AI + 30% 规则）。Hybrid 模式在性能与可预测性之间取得平衡，并可在 AI 异常时自动降级到规则模式。

## 项目架构（概览）

三层架构：

- Layer 1 — AI 推理层：TaskPriorityPredictor、SWAPCostPredictor、ResourcePredictor、FaultPredictor。
- Layer 2 — AISchedulerAdapter：融合 AI 评分与现有规则引擎，提供统一接口给调度器。
- Layer 3 — 运行时与执行层：现有的 Q# / C# 调度器、QubitPool、执行与记录模块；同时接入 Silq 静态分析模块以供优化建议。

目录（简要）：

```
AIIntegration/           # Phase 3: AI 集成（C# 实现）
	├─ AIModels.cs         # 4个预测器实现
	├─ AISchedulerAdapter.cs
	├─ AIEnhancedScheduler.cs
	├─ SilqCircuitAdapter.cs
	├─ SilqAIOptimizer.cs
	└─ Phase3Demo / SilqAIDemo 演示

QuantumRuntime/          # 原始 Q# 原型（Phase 1/2）
SilqExperiments/         # Silq 示例与对比实现
```

## Silq 在项目中的角色

Silq 部分（位于 SilqExperiments）用于：

- 作为高层量子程序的示例输入：展示更高抽象级别的电路编码方式；
- 提供静态语义信息：Silq 的类型系统与自动反计算（uncomputation）能在静态分析时帮助识别可优化的临时态或无用门；
- 给 AI 提供源代码级别的优化建议：SilqCircuitAdapter 会解析 Silq 源码，估算 T 门、CNOT、深度，并由 SilqAIOptimizer 基于规则+AI 给出具体优化建议（例如 T-gate 减少、CNOT 消除、并行化改写、Oracle 优化等）。

简言之：Silq 用作“高层电路源”，AI 模块把静态分析结果转为可执行的优化/调度决策。

## 如何运行（快速）

1. Phase 3 演示（AI + Silq）

```bash
cd AIIntegration
dotnet build -c Release
dotnet run -c Release
```

2. 原始 Q# 原型

```bash
cd QuantumRuntime
dotnet build
dotnet run
```

（注意：Q# SDK 版本与本地 .NET 环境可能影响 QuantumRuntime 的构建，已在仓库内添加 `dotnet-install.ps1` 做兼容处理。）

## 成果与验证

- AI 模式在小规模演示中已显著提高调度效率（示例：AI 模式相比规则模式在演示场景中减少总体执行量）。
- Silq 静态分析能发现高 T 門 / 高深度电路並給出量化的优化建议。

## 后续工作（建议）

- 将 Silq 优化反馈自动写回 Silq 源码或导出到可执行 QASM；
- 收集真实硬件执行数据以持续在线训练 SWAP/故障预测模型；
- 引入更强的 ML 模型或强化学习以用于长期策略优化。

---

若需我把 README 的某部分改为更精简或补充具体命令/示例，请告诉我要调整的段落。

**更新时间**: 2026-02-25
