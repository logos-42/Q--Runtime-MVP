# Quantum Runtime IR v0.2 - Rust Skeleton

## 概述

Quantum Runtime IR v0.2 是一个自主抽象的量子计算中间表示层，支持 DAG 电路结构、作业调度器和异步后端适配器。

## 核心能力

### 1️⃣ DAG 电路结构

- `CircuitDag` 替代 `Vec<Operation>`
- `OperationNode` 包含依赖关系
- 支持拓扑排序和并行检测
- 自动计算电路深度（关键路径）

### 2️⃣ Job 调度器

- `JobScheduler` 管理作业队列
- 优先级调度（Low/Normal/High/Urgent）
- 资源感知（qubit 可用性检查）
- 依赖感知的调度决策

### 3️⃣ 异步 BackendAdapter

- `submit_job()` → `JobId`
- `get_job_status()` → `JobStatus`
- `get_job_result()` → `JobResult`
- 支持同步 `execute()` 作为默认实现

### 4️⃣ 类型安全

- `LogicalQubitId` vs `PhysicalQubitId` 分离
- newtype 模式防止类型混用
- 明确的 ownership 模型

## 文件结构

```
src/
├── lib.rs         # 库入口和错误类型
├── qubit.rs       # 逻辑/物理 qubit 分离
├── operation.rs   # 操作枚举（含三比特门）
├── circuit.rs     # DAG 电路结构
├── job.rs         # Job 和调度器
├── runtime.rs     # QuantumRuntime 引擎
├── backend.rs     # 异步 BackendAdapter trait
└── prelude.rs     # 便捷导入
examples/
└── demo_v02.rs    # v0.2 演示
```

## 编译验证

```bash
cd QuantumRuntime/IR
cargo build          # ✓ 编译成功
cargo test           # ✓ 36 个测试通过
cargo run --example demo_v02
```

## 使用示例

### 创建 DAG 电路

```rust
use quantum_ir::prelude::*;

let mut dag = CircuitDag::with_name("Bell State");
let q0 = LogicalQubitId::new(0);
let q1 = LogicalQubitId::new(1);

let n1 = dag.add_node(h(q0));
let n2 = dag.add_node(h(q1));
let n3 = dag.add_node(cnot(q0, q1));

dag.add_edge(n1, n3).unwrap();
dag.add_edge(n2, n3).unwrap();

println!("Depth: {}", dag.depth());  // 输出：2
```

### 使用 Runtime 执行

```rust
let mut runtime = QuantumRuntime::default();
runtime.register_backend("sim", Arc::new(IdealSimulatorBackend::new()));

let job_id = runtime.create_job(
    dag,
    100,  // shots
    Priority::High,
    JobMetadata::new().with_user("alice"),
);

let results = runtime.execute_all();
```

## v0.1 → v0.2 改进

| 特性 | v0.1 | v0.2 |
|------|------|------|
| 电路结构 | `Vec<Operation>` | `CircuitDag` |
| Qubit 类型 | `QubitId = u64` | `LogicalQubitId` / `PhysicalQubitId` |
| 调度 | 无 | `JobScheduler` |
| 后端接口 | 同步 | 异步 trait |
| 三比特门 | ❌ | ✅ Toffoli/Fredkin |
| 错误模型 | ❌ | ✅ `ErrorModel` |
| 耦合图 | ❌ | ✅ `CouplingMap` |

## 测试覆盖

- ✅ qubit 模块：6 个测试
- ✅ operation 模块：5 个测试
- ✅ circuit 模块：8 个测试
- ✅ job 模块：7 个测试
- ✅ backend 模块：5 个测试
- ✅ runtime 模块：5 个测试

**总计：36 个测试通过**

## 下一步（v0.3）

1. 实现真正的异步执行（tokio）
2. 添加编译器优化通道
3. 支持脉冲级控制
4. 完善错误校正支持
