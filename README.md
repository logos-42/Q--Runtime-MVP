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
- Q#：手动 uncomputation
- Silq：自动类型系统驱动的 uncomputation

## 参考

- 探索方向：量子-经典混合结构设计
- 核心问题：经典调度思想如何迁移到量子

---

**创建日期**: 2026-02-25
