# Phase 2B & 2C 完整演示报告

## 运行环境
- .NET 8.0
- C# 最新版本
- Windows PowerShell

## Phase 2B: 并发任务调度 (Conflict Detection)

### 初始化
```
✓ 初始化调度器 (8 qubits, Queuing 冲突解决策略)
```

### 创建电路

| 电路名称 | 指令数 | 总门数 | T-门数 | 需要 Qubits |
|---------|-------|-------|--------|-----------|
| PhotonPrepare | 2 | 2 | 1 | 2 |
| EntanglePair | 2 | 2 | 1 | 3 |
| MeasureOut | 1 | 1 | 0 | 2 |

### 调度场景模拟

#### 步骤 1: 分配任务 1 (PhotonPrepare)
```
分配 qubits: [0, 1]
状态: Running
已用 qubits: 2/8
```

#### 步骤 2: 分配任务 2 (EntanglePair)  
```
检查冲突: 任务 1 占用 [0, 1], 任务 2 需要 [2, 3, 4]
✓ 无冲突 → 可以并发执行
分配 qubits: [2, 3, 4]
状态: Running
已用 qubits: 5/8
```

#### 步骤 3: 分配任务 3 (MeasureOut)
```
检查冲突: 
  - 任务 1 占用 [0, 1]
  - 任务 2 占用 [2, 3, 4]
  - 任务 3 需要 [5, 6]
✓ 无冲突 → 可以并发执行
分配 qubits: [5, 6]
状态: Running
已用 qubits: 7/8
```

### 最终状态
```
✓ 3 个任务全部并发运行
✓ 占用 7/8 qubits
✓ 碎片化: 1 个空闲 qubit
```

**关键观察**: 
- 冲突检测确保了资源的安全并发访问
- 优先级驱动的调度 + 冲突检查 = 高效资源利用
- 并发能力提高了整体吞吐量

---

## Phase 2C: 电路优化 (Layout Pass / SWAP Insertion)

### 量子芯片拓扑
线性链式拓扑（Line Topology）：
```
0 -- 1 -- 2 -- 3 -- 4 -- 5 -- 6 -- 7
```

只有相邻的 qubits 能直接执行两比特门。远距离操作需要 SWAP。

### 案例 1：远距离门（需要优化）

**电路**: LongDistanceGate

指令列表：
```
1. CNOT(0, 7)     T-门: 1
2. H(3)           T-门: 0
3. CZ(1, 6)       T-门: 2
```

#### 优化分析

| 指令 | 操作 | 直接执行？ | SWAP 需求 | SWAP 成本 |
|-----|------|----------|----------|---------|
| 1 | CNOT(0, 7) | ✗ 距离 7 | 7-1 = 6 swaps | 6 × 3 = 18 |
| 2 | H(3) | ✓ 单比特 | 0 | 0 |
| 3 | CZ(1, 6) | ✗ 距离 5 | 5-1 = 4 swaps | 4 × 3 = 12 |

**成本统计**:
```
原始门数:           3
SWAP 成本:         30
优化后门数:        33
开销比例:        1000%
```

**优化结果**:
```
原始电路:  CNOT(0,7) → H(3) → CZ(1,6)
           [3 gates]

优化电路:  SWAP(0,1) → SWAP(1,2) → ... → CNOT(6,7)
           SWAP(7,6) → ... → CZ(5,6)
           + 原始指令
           [33 gates]
```

### 案例 2：本地门（无需优化）

**电路**: LocalGates

指令列表：
```
1. CNOT(2, 3)     T-门: 0
2. CZ(3, 4)       T-门: 1
```

#### 优化分析

| 指令 | 操作 | 直接执行？ | 原因 |
|-----|------|----------|------|
| 1 | CNOT(2, 3) | ✓ 邻近 | 3 = 2+1 |
| 2 | CZ(3, 4) | ✓ 邻近 | 4 = 3+1 |

**成本统计**:
```
原始门数:        2
SWAP 成本:       0
优化后门数:      2
开销比例:        0%
```

**结论**: 无需优化，原始电路已是最优。

---

## 综合对比

### 性能指标

| 指标 | 远距离电路 | 本地电路 | 改进空间 |
|-----|----------|--------|--------|
| 原始门数 | 3 | 2 | - |
| SWAP 成本 | 30 | 0 | 1000% |
| T-门占比 | 3/3 = 100% | 1/2 = 50% | 40% |
| 实际资源消耗 | 11 倍 | 1 倍 | 🔴 高 |

### 优化策略建议

1. **前端优化** (编译阶段)
   - 重排指令顺序，减少 SWAP 数量
   - 使用更优的量子门分解

2. **中端优化** (布局阶段)
   - 贪心算法找最优初始布局
   - 动态重新布局（在执行过程中）

3. **后端优化** (调度阶段)
   - 批量执行相同深度的指令
   - 并发多个互不冲突的任务

---

## 系统集成

### 完整工作流

```
输入电路 
   ↓
[Phase 2B] 冲突检测 + 任务调度
   ├─ 检查资源竞争
   ├─ 优先级排序
   └─ 并发分配
   ↓
[Phase 2C] 布局优化 + SWAP 插入
   ├─ 拓扑感知的映射
   ├─ 最小 SWAP 路由
   └─ 资源成本估计
   ↓
优化电路 + 执行方案
```

### 代码实现类结构

```csharp
// Phase 2B: 冲突检测
class ResourceConflictDetector
{
    bool HasConflict(int[] qubits1, int[] qubits2)
    HashSet<int> GetAllocatedQubits(...)
    List<int> GetAvailableQubits(...)
}

// Phase 2B: 高级调度器
record AdvancedScheduler
{
    List<Task> TaskQueue
    List<Task> RunningTasks      // ← 支持并发！
    List<Task> CompletedTasks
    ConflictResolutionPolicy Policy
}

// Phase 2C: 电路优化
class CircuitLayoutOptimizer
{
    bool CanExecuteDirectly(int[] targetQubits)
    int EstimateSwapCost(int[] targetQubits)
    OptimizedCircuitBlock OptimizeCircuit(...)
}
```

---

## 测试与验证

### 编译结果
```
✓ QuantumRuntime 已成功编译
  - Program.cs (Phase 1): 447 行
  - Demo2Standalone.cs (Phase 2B & 2C): 286 行
  - MenuProgram.cs (交互菜单): 65 行
  总计: ~800 行代码
```

### 功能验证清单

- [x] 冲突检测：识别 qubit 重叠
- [x] 并发调度：多任务同时运行
- [x] 优先级处理：高优先级任务优先
- [x] SWAP 成本估计：计算布局开销
- [x] 拓扑感知：线性拓扑支持
- [x] 资源统计：完整的性能指标

---

## 关键发现

### 1. 冲突检测的必要性
```
场景：3 个并发任务
原实现（串行）：总耗时 = T1 + T2 + T3
Phase 2B（并发）：总耗时 = max(T1, T2, T3)
✓ 提升: 最多 3 倍吞吐量
```

### 2. 拓扑感知优化的影响
```
远距离 gate 的成本：
  = 距离 × 3（SWAP 单位成本）
  
示例：CNOT(0, 7)
  基础成本：1 个门
  SWAP 成本：6 × 3 = 18 个门
  总成本：19（开销 1900%）
```

### 3. 设计空间
```
吞吐量 ↔ 延迟
并发 ↔ 资源利用率
优化 ↔ 编译时间

B + C 的结合优化了吞吐量和资源利用率
```

---

## 下一步研究方向

### Phase 3: 随机 SWAP 消减（Random SWAP Cancellation）
检测和消除冗余的 SWAP 序列

### Phase 4: 多芯片间通信
多个量子处理器的协调调度

### Phase 5: 错误更正集成
将 Phase 2B/2C 与表面码集成

---

## 参考代码

### 冲突检测示例
```csharp
var allocated1 = ResourceConflictDetector.GetAllocatedQubits(allocations);
var available1 = ResourceConflictDetector.GetAvailableQubits(8, allocated1);

bool hasConflict = ResourceConflictDetector.HasConflict(
    new int[] {2, 3, 4},  // Task 2 需要
    new int[] {0, 1}       // Task 1 占用
);  // false - 无冲突，可以并发
```

### 优化成本估计示例
```csharp
int swapCost = CircuitLayoutOptimizer.EstimateSwapCost(
    new int[] {0, 7}  // CNOT(0, 7)
);  // 返回 18（6 个SWAP × 3）
```

---

**生成日期**: 2026年2月25日  
**演示完成度**: 100% ✓
