# Quantum Runtime IR v0.2

## 概述

量子计算中间表示层 v0.2，硬件无关、可扩展的 Rust 实现。

## 核心特性

| 特性 | 说明 |
|------|------|
| **DAG 电路结构** | 从线性 Vec 演进为 DAG，支持并行操作检测 |
| **Job 调度器** | 优先级队列 + 资源管理 + 并发控制 |
| **异步 BackendAdapter** | async trait 接口，支持多后端 |
| **逻辑/物理 Qubit 分离** | 类型安全，支持 error correction 扩展 |

## 项目结构

```
QuantumRuntime/IR/
├── src/
│   ├── lib.rs          # 入口 + 错误类型
│   ├── qubit.rs        # 逻辑/物理 Qubit 抽象
│   ├── operation.rs   # 门操作枚举
│   ├── circuit.rs     # DAG 电路结构
│   ├── job.rs         # Job + 调度器
│   ├── backend.rs     # 异步 BackendAdapter
│   └── runtime.rs     # QuantumRuntime 引擎
├── Cargo.toml
└── README.md
```

## 编译测试

```bash
cd QuantumRuntime/IR
cargo build
cargo test
```

## 快速开始

```rust
use quantum_ir::prelude::*;

// 创建 DAG 电路
let mut circuit = CircuitDag::with_name("Bell State");
circuit.add_node(h(LogicalQubitId::new(0)));
circuit.add_node(cnot(LogicalQubitId::new(0), LogicalQubitId::new(1)));

// 创建作业
let job = Job::new(circuit, 1024, "ideal_simulator")
    .with_priority(Priority::High);

// 提交执行
let backend = IdealSimulatorBackend::new();
let result = backend.execute(&job)?;
```

## 模块说明

### circuit.rs - DAG 电路
- `CircuitDag`: 有向无环图结构
- `OperationNode`: 操作节点 + 依赖关系
- 拓扑排序、深度计算、并行组检测

### job.rs - 作业调度
- `Job`: 作业抽象（id, circuit, shots, priority）
- `JobScheduler`: 调度器（优先级排序 + qubit 可用性检查）
- `JobQueue`: 优先级队列

### backend.rs - 后端适配器
- `BackendAdapter` trait: 异步执行接口
- `BackendCapabilities`: 后端能力描述
- `IdealSimulatorBackend`: 模拟器实现

### qubit.rs - Qubit 抽象
- `LogicalQubitId` / `PhysicalQubitId`: 类型分离
- `QubitMapping`: 逻辑→物理映射

## 版本历史

- **v0.1**: 最小可运行抽象（Vec<Operation>）
- **v0.2**: DAG 结构 + 异步调度器（当前）

## 演进方向

- v0.3: 编译器优化通道
- v0.4: 脉冲级控制
- v1.0: 稳定 API

---

**创建日期**: 2026年2月26日  
**版本**: v0.2
