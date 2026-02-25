# 量子-经典混合 Runtime 原型

## 项目概述

这是一个用 Q# 实现的量子 Runtime 系统原型，探索**经典系统如何调度和管理量子资源**。

## 核心设计理念

不是追求算法复杂度，而是追求**结构复杂度**：

```
怎样用经典代码来：
  ✓ 抽象 qubit 的生命周期
  ✓ 管理资源约束
  ✓ 实现可逆计算的自动化
  ✓ 优化任务调度
```

## 项目结构

### 1. QubitPool.qs
**Qubit 资源池管理**

关键概念：
- `QubitState`：追踪每个 qubit 的状态（Free → Allocated → InUse → Released）
- `QubitRecord`：单个 qubit 的状态记录（包括操作计数、奇偶缓冲区）
- `QubitPoolManager`：集中管理所有 qubit 资源

**为什么这很重要？**
- 经典计算机通过内存池管理内存，量子系统缺少这一层抽象
- 明确追踪 qubit 分配 → 使用 → 释放，就能做资源估计
- 奇偶缓冲区为可逆计算的自动擦除做准备

---

### 2. CircuitIR.qs
**电路中间表示（Intermediate Representation）**

关键概念：
- `GateType`：区分不同操作（单比特、双比特、测量等）
- `ResourceCost`：追踪每个操作的资源消耗（门数、T 门数、深度、qubit 数）
- `Instruction`：可逆标记 + uncomputation 大小
- `CircuitBlock`：支持嵌套的电路结构

**为什么这很重要？**
- 将量子电路看作"程序 IR"（如编译器的中间表示）
- 门数 ≠ 资源，T 门才是错误更正的瓶颈
- 嵌套结构 → 模块化设计 → 资源组合法则

---

### 3. Scheduler.qs
**任务调度器**

关键概念：
- `Task`：包含电路、优先级、分配状态
- `Scheduler`：维护任务队列、qubit 池、全局时间戳
- 调度策略：优先级贪婪分配

核心操作：
```
SubmitTask → GetNextTask → ScheduleTask → ExecuteTask
     ↓           ↓              ↓              ↓
  入队     找最高优先级    分配qubit      释放资源
```

**为什么这很重要？**
- 量子计算机是共享资源，需要多任务调度
- 优先级决策 + 资源约束 = 组合优化问题
- 时间戳追踪 → 可以分析吞吐量 / 延迟权衡

---

### 4. Main.qs
**完整演示**

展示流程：
1. 创建 10 qubit 的资源池
2. 构建 3 个电路（单比特、双比特、嵌套）
3. 创建 3 个任务（不同优先级）
4. 调度执行，释放资源
5. 统计资源消耗

## 运行

```bash
dotnet new console -n QuantumRuntime
cd QuantumRuntime
# 复制 .qs 文件到项目目录
dotnet build
dotnet run
```

## 实验指向

这个原型展示的是：

| 传统编程 | 量子编程 |
|---------|---------|
| Memory Pool | **Qubit Pool** ✓ |
| Bytecode IR | **Circuit IR** ✓ |
| CPU Scheduler | **Quantum Scheduler** ✓ |
| Task Manager | **Task Priority** ✓ |

## 下一步研究方向

### A. 与 Silq 对比
Silq 用**类型系统**自动处理 uncomputation：
```silq
def qft(qs: !qbit[n]) {
  // 自动释放临时 qubits
  for i in 0..n-1 {
    ...
  }  // uncomputation 自动插入
}
```

我们的 Q# 版本则显式：
```qsharp
let uncomputationSize = CalculateUncomputationSize(...)
```

**问题**：能否用 Q# 的类型系统模仿 Silq 的行为？

### B. 资源估计深化
当前只计算门数，下一步：
- SWAP 插入的开销（layout）
- 错误更正码的 overhead（surface code）
- 算法级的量子优势条件

### C. 可逆计算的自动化
问题：如何在 Q# 中自动生成 uncomputation？

```
原始: a := f(b)
目标: (a, b') 其中 b' = b（自动恢复）
```

## 关键学习点

1. **结构抽象比算法优化更基础**
   - 好的抽象让坏问题变简单
   
2. **资源约束是首要考虑**
   - T 门 > 门数 > 深度（在不同场景下）
   
3. **经典调度思想可迁移到量子**
   - 优先级队列、资源分配、争用解决
   
4. **可逆性有结构**
   - 不是"所有操作都可逆"，而是"这些操作的逆需要什么"

---

**创建日期**: 2026年2月25日  
**研究阶段**: 原型验证（Prototype Validation）
