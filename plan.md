# 量子-经典兼容系统优化计划

## 项目架构分析

### 当前系统组成

```
量子经典兼容系统/
├── QuantumRuntime/           # Q# 运行时 (Phase 2)
│   ├── QubitPool.qs         # Qubit 资源池管理
│   ├── CircuitIR.qs         # 量子电路中间表示 (缺失)
│   ├── TaskQueue.qs         # 任务队列 (缺失)
│   ├── Scheduler.qs         # 任务调度器
│   └── Program.qs           # 主程序
│
├── AIIntegration/           # C# AI 集成 (Phase 3)
│   ├── AIModels.cs          # AI 模型 (4个预测器)
│   ├── AISchedulerAdapter.cs # AI 调度适配层
│   ├── AIEnhancedScheduler.cs # 增强调度器
│   └── ...
│
└── SilqExperiments/         # Silq 实验代码
    ├── CircuitIR.silq
    ├── QubitPool.silq
    ├── Scheduler.silq
    └── ...
```

### 核心数据模型对比

| 组件 | Q# | C# | Silq |
|------|-----|-----|------|
| 门类型 | `enum GateType` | 内联定义 | `enum GateType` |
| 电路块 | `CircuitBlock` | `CircuitBlock(record)` | `CircuitBlock` |
| 资源成本 | `ResourceCost` | 内联计算 | `ResourceCost` |
| 任务 | `Task` | `Task(record)` | - |
| Qubit状态 | `QubitState(enum)` | 无 | `𝔹` (quantum) |

---

## 优化计划

### Phase 1: 关键修复

#### 1.1 恢复缺失的 Q# 文件
- [ ] 恢复 `CircuitIR.qs` - 电路中间表示
- [ ] 恢复 `TaskQueue.qs` - 任务队列系统

#### 1.2 统一数据模型
- [ ] 定义跨语言共享的 `CircuitBlock` 结构
- [ ] 定义跨语言共享的 `Task` 结构
- [ ] 定义跨语言共享的 `ResourceCost` 结构

---

### Phase 2: Q# 代码优化

#### 2.1 QubitPool.qs - 减少重复循环

**问题**: 多个操作中有重复的查找和更新逻辑

```qs
// 当前：每个操作都遍历整个数组
operation AllocateQubit(pool) : (...) {
    for i in 0..Length(pool::qubitRecords) - 1 {
        if pool::qubitRecords[i]::state == QubitState::Free { ... }
    }
}

operation ReleaseQubit(id, pool) : ... {
    for i in 0..Length(pool::qubitRecords) - 1 {
        if pool::qubitRecords[i]::id == id { ... }
    }
}
```

**优化方案**: 提取通用 `UpdateQubitAt` 函数

```qs
// 优化后：通用更新函数
function UpdateQubitAt(
    pool: QubitPoolManager, 
    predicate: (QubitRecord -> Bool),
    updater: (QubitRecord -> QubitRecord)
) : QubitPoolManager {
    // 一次遍历完成所有更新
    ...
}
```

#### 2.2 CircuitIR.qs - 逆电路生成优化

**当前问题**:
- 手动逆推每个门的逆
- 缺乏自动 uncomputing 逻辑

**优化方向**:
- 实现基于 `adjoint` 的自动逆生成
- 集成 uncomputation 大小计算

---

### Phase 3: C# 代码优化

#### 3.1 AIIntegration - 减少重复代码

**问题**:
1. `TaskFeatures` 在多处重复创建
2. 字典更新模式重复 (`RecordUsage`, `RecordFailure`, etc.)

```csharp
// 当前：多处重复
var features = new TaskFeatures(
    Depth: task.Circuit.Depth,
    TGateCount: task.Circuit.TGateCount,
    ...
);
```

**优化方案**:
- 提取 `TaskExtensions.ToFeatures(this Task task)`
- 统一字典操作接口

#### 3.2 优化正则表达式解析

**问题**: 当前未发现明显的正则表达式使用，但预留优化空间

**优化方向**:
- 电路解析使用预编译正则表达式
- 实现简单的正则表达式缓存

---

### Phase 4: 架构优化

#### 4.1 引入 Builder 模式

**当前**: 直接构造函数创建对象

```csharp
// 当前
var task = new Task(id, name, circuit, priority, submitTime);
var circuit = new CircuitBlock("name", depth, tGate, qubit);
```

**优化后**: Builder 链式调用

```csharp
// 优化后
var task = TaskBuilder.Create()
    .WithId(1)
    .WithName("Bell")
    .WithCircuit(circuit)
    .WithPriority(High)
    .Build();

var circuit = CircuitBuilder.Create("Bell")
    .WithDepth(2)
    .WithTGateCount(0)
    .Build();
```

#### 4.2 统一 Q#/C#/Silq 数据模型

```
Shared/
├── CircuitModels.cs     # 共享电路模型 (供 C# 使用)
├── CircuitModels.qs     # 共享电路模型 (供 Q# 使用)
└── CircuitModels.silq   # 共享电路模型 (供 Silq 使用)
```

---

## 实施优先级

| 优先级 | 任务 | 预计工作量 |
|--------|------|------------|
| P0 | 恢复 CircuitIR.qs, TaskQueue.qs | 2h |
| P1 | QubitPool.qs 循环优化 | 1h |
| P1 | 统一数据模型 | 2h |
| P2 | Builder 模式引入 | 2h |
| P2 | AIIntegration 重构 | 1h |
| P3 | Silq 自动 uncompute 实验 | 4h |

---

## 验收标准

- [ ] Q# 项目可编译通过
- [ ] QubitPool 操作复杂度从 O(n²) 降至 O(n)
- [ ] C# 和 Q# 数据模型字段一致
- [ ] Builder 模式覆盖主要对象创建
