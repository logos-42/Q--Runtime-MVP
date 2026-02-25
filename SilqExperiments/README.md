# Silq Experiments - 量子经典兼容系统

## 项目概述

本项目探索使用 Silq 高级量子编程语言实现量子经典兼容系统的核心抽象。

Silq 是由 ETH Zürich 开发的高级量子编程语言，具有：
- **强静态类型系统**：区分经典类型 (`!τ`) 和量子类型 (`τ`)
- **自动反计算 (Uncomputation)**：自动清理临时量子态，避免隐式测量
- **直观的语法**：类似经典编程语言的表达力

## 目录结构

```
SilqExperiments/
├── QubitPool.silq    - Qubit 资源池抽象
├── TaskQueue.silq    - 任务队列实现
├── Scheduler.silq    - 量子任务调度器
├── CircuitIR.silq    - 电路中间表示
├── Main.silq         - 入口程序和测试
└── README.md         - 项目说明
```

## Silq 基础语法要点

### 类型系统

| 类型 | 说明 | 示例 |
|------|------|------|
| `!𝔹` / `!B` | 经典布尔值 | `x := true: !𝔹` |
| `𝔹` / `B` | 量子布尔值 (可叠加) | `q := H(false: 𝔹)` |
| `!ℕ` / `!N` | 经典自然数 | `n := 5: !ℕ` |
| `int[n]` | n 位量子整数 | `x := 3: int[4]` |
| `τ[]` | 动态数组 | `arr := [1,2,3]` |
| `τ^n` | 固定长度向量 | `vec: 𝔹^3` |

### 函数定义

```silq
def functionName[paramName: Type](params): ReturnType {
    // 函数体
    return value;
}
```

### 量子操作

| 操作 | 说明 | 示例 |
|------|------|------|
| `H` | Hadamard 门 | `q := H(q)` |
| `X` | Pauli-X (比特翻转) | `q := X(q)` |
| `CNOT` / `CX` | 受控非门 | `target := CNOT(control, target)` |
| `measure` | 测量 | `result := measure(q)` |
| `phase` | 相位旋转 | `phase(θ)` |
| `dup` | 量子复制 | `dup(q)` |

### 自动反计算

Silq 的核心特性：
- 临时量子变量自动清理
- 使用 `lifted` 注解标记可自动反计算的函数
- 无需手动编写 `within {...} apply {...}` (如 Q#)

## 运行环境

需要安装：
1. Silq 编译器和 VS Code 扩展
2. 从 https://silq.ethz.ch 获取安装说明

运行命令：
```bash
silq Main.silq
```

## 示例程序

### Bell 态制备
```silq
def prepareBellState(): 𝔹 × 𝔹 {
    q1 := false: 𝔹;
    q2 := false: 𝔹;
    q1 := H(q1);
    q2 := CNOT(q1, q2);
    return (q1, q2);
}
```

### 量子隐形传态
```silq
def teleport(state: 𝔹): !𝔹 {
    // 共享 Bell 态
    aliceBell := false: 𝔹;
    bobBell := false: 𝔹;
    aliceBell := H(aliceBell);
    bobBell := CNOT(aliceBell, bobBell);
    
    // Alice 的 Bell 测量
    aliceBell := CNOT(state, aliceBell);
    state := H(state);
    m1 := measure(state);
    m2 := measure(aliceBell);
    
    // Bob 的校正
    if m2 { bobBell := X(bobBell); }
    if m1 { bobBell := Z(bobBell); }
    
    return measure(bobBell);
}
```

## Q# vs Silq 差异

| 特性 | Q# | Silq |
|------|----|----|
| 资源管理 | 手动 (`using` 块) | 自动 (类型系统) |
| 反计算 | `within {...} apply {...}` | 自动 |
| 类型系统 | 运行时检查 | 编译时强类型 |
| 经典/量子混合 | 分离明显 | 无缝集成 |
| 语法风格 | 命令式 | 函数式 |

## 后续计划

1. 实现完整的 Qubit 资源池抽象
2. 设计基于 Silq 的任务调度器
3. 探索自动反计算在复杂算法中的应用
4. 与 Q# 实现进行性能对比

## 参考资料

- [Silq 官方文档](https://silq.ethz.ch)
- [Silq GitHub](https://github.com/eth-sri/silq)
- [Quantum Computing with Silq Programming (Packt)](https://www.packtpub.com/product/quantum-computing-with-silq-programming/9781800569669)
