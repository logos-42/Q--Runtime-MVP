# Silq 语法参考与 Q# 对比

## 一、Silq 类型系统

### 经典类型 vs 量子类型

Silq 的核心创新是**类型系统自动追踪量子态**，区分经典值和量子叠加态。

| 类型 | Silq 语法 | Q# 对应 | 说明 |
|------|----------|--------|------|
| 经典布尔 | `!𝔹` 或 `!B` | `Bool` | 只能是 0 或 1，无叠加 |
| 量子布尔 | `𝔹` 或 `B` | `Qubit` | 可以是叠加态 |
| 经典自然数 | `!ℕ` 或 `!N` | `Int` | 经典整数 |
| 经典整数 | `!ℤ` 或 `!Z` | `Int` | 经典整数 (可负) |
| 量子整数 | `int[n]` | `LittleEndian` | n 位量子寄存器 |
| 无符号量子整数 | `uint[n]` | `LittleEndian` | n 位无符号量子寄存器 |
| 经典实数 | `!ℝ` 或 `!R` | `Double` | 经典浮点数 |
| 经典有理数 | `!ℚ` 或 `!Q` | `Double` | 经典有理数 |
| 数组 | `τ[]` | `T[]` | 动态长度数组 |
| 向量 | `τ^n` | `Qubit[]` | 固定长度 |
| 元组 | `τ × τ` | `(T, T)` | 元组类型 |
| 单例 | `𝟙` 或 `1` | `Unit` | 空类型 |

### 类型注解

```silq
// 经典类型 (不能是叠加态)
x := 5: !ℕ;
b := true: !𝔹;

// 量子类型 (可以是叠加态)
q := false: 𝔹;
q := H(q);  // 现在是叠加态

// 类型转换
classic := measure(quantum);  // 量子 → 经典
quantum := classic as 𝔹;       // 经典 → 量子 (制备)
```

---

## 二、函数定义

### Silq 函数语法

```silq
// 基本函数
def functionName[paramName: Type](): ReturnType {
    // 函数体
    return value;
}

// 带经典参数的函数
def addClassical[a: !ℕ, b: !ℕ](): !ℕ {
    return a + b;
}

// 带量子参数的函数
def applyHadamard[q: 𝔹](): 𝔹 {
    return H(q);
}

// 泛型长度参数
def uniformSuperposition[n: !ℕ](): 𝔹^n {
    vec := vector(n, false: 𝔹);
    for i in [0..n) {
        vec[i] := H(vec[i]);
    }
    return vec;
}
```

### Q# vs Silq 函数对比

| 特性 | Q# | Silq |
|------|----|----|
| 函数声明 | `operation Name(input: Type): Type { }` | `def name[input: Type](): Type { }` |
| 函数类型 | `operation` / `function` | `def` (统一) |
| 泛型 | `'T` 类型参数 | `n: !ℕ` 值参数 |
| 可逆性 | `is Adj + Ctl` | 自动推断 |

---

## 三、量子操作

### 基本量子门

| 操作 | Silq | Q# | 说明 |
|------|------|----|----|
| Hadamard | `H(q)` | `H(q)` | 创建叠加态 |
| Pauli-X | `X(q)` | `X(q)` | 比特翻转 |
| Pauli-Y | `Y(q)` | `Y(q)` | Y 门 |
| Pauli-Z | `Z(q)` | `Z(q)` | Z 门 |
| CNOT | `CNOT(ctrl, tgt)` | `CNOT(ctrl, tgt)` | 受控非 |
| 相位 | `phase(θ)` | `P(θ, q)` | 相位旋转 |
| RX | `rotX(θ, q)` | `Rx(θ, q)` | X 轴旋转 |
| RY | `rotY(θ, q)` | `Ry(θ, q)` | Y 轴旋转 |
| RZ | `rotZ(θ, q)` | `Rz(θ, q)` | Z 轴旋转 |
| 测量 | `measure(q)` | `M(q)` | 量子测量 |

### 特殊操作

```silq
// 复制量子态 (不违反不可克隆定理，创建纠缠)
dup(q: const 𝔹): 𝔹 × 𝔹

// 创建数组
array(size: !ℕ, init: const τ): τ[]

// 创建向量
vector(size: !ℕ, init: const τ): τ^size

// 手动反计算 (忘记临时值)
forget(value: τ, condition: const τ): 𝟙

// 反转 mfree 过程
reverse(process)
```

---

## 四、控制流

### 条件语句

```silq
// 经典条件
if classicCondition {
    // 经典分支
} else {
    // 经典分支
}

// 量子条件 (有限制)
if quantumCondition {
    // 两个分支都必须是 mfree
    // 条件必须可自动反计算
} else {
    // ...
}
```

### 循环

```silq
// for 循环 (范围)
for i in [0..n) {
    // 从 0 到 n-1
}

for i in (0..n] {
    // 从 1 到 n
}

// while 循环 (条件必须是经典的)
while condition {
    // condition: !𝔹
}
```

### Q# vs Silq 控制流

| 特性 | Q# | Silq |
|------|----|----|
| 经典 if | `if cond { } elif { } else { }` | `if cond { } else { }` |
| 量子 if | `ControlledOnInt` 等 | `if quantumCond { }` (有限制) |
| for 循环 | `for i in 0..n-1 { }` | `for i in [0..n) { }` |
| while 循环 | `while cond { }` | `while classicCond { }` |
| repeat-until | `repeat { } until { }` | 不支持 (需手动实现) |

---

## 五、自动反计算 (Uncomputation)

### Silq 的核心特性

Silq **自动**清理临时量子变量，无需手动编写反计算代码。

### Q# 的手动反计算

```qsharp
// Q# 需要手动管理
operation ComputeWithAncilla(): Result {
    using (ancilla = Qubit()) {
        within {
            // 准备 ancilla
            H(ancilla);
        } apply {
            // 使用 ancilla 进行计算
            // ...
        }
        // ancilla 自动清理
    }
}
```

### Silq 的自动反计算

```silq
// Silq 自动处理
def computeWithAncilla(): 𝔹 {
    var result := false: 𝔹;
    var ancilla := false: 𝔹;
    
    ancilla := H(ancilla);
    // 使用 ancilla 进行计算
    // ...
    
    // ancilla 自动清理，无需手动代码
    return result;
}
```

### 注解控制

```silq
// lifted: qfree 函数，参数为常量，启用自动反计算
def myOracle[x: const 𝔹] lifted {
    // 临时变量自动清理
}

// qfree: 不引入/破坏叠加态
def classicalFunction[x: 𝔹] qfree {
    return x;
}

// mfree: 无需测量即可求值
def phaseOperation[x: 𝔹] mfree {
    phase(0.5);
    return x;
}

// const: 变量不变
def useConstant[x: const 𝔹] {
    // x 不会被修改
}
```

---

## 六、模块和导入

### Silq 模块系统

```silq
// 定义模块 (文件即模块)
// QubitPool.silq

// 导出类型和函数 (自动)
enum QubitState { ... }
def createQubitPool[...]() { ... }

// 导入模块
// Main.silq
import QubitPool;

def main() {
    let pool := createQubitPool(...);
}
```

### Q# vs Silq 模块

| 特性 | Q# | Silq |
|------|----|----|
| 命名空间 | `namespace Name { }` | 文件即模块 |
| 导入 | `open Namespace;` | `import Module;` |
| 可见性 | `internal`, `export` | 默认全部导出 |

---

## 七、完整示例对比

### Bell 态制备

**Q#**:
```qsharp
operation PrepareBellState() : (Qubit, Qubit) {
    using ((q1, q2) = (Qubit(), Qubit())) {
        H(q1);
        CNOT(q1, q2);
        return (q1, q2);
    }
}
```

**Silq**:
```silq
def prepareBellState(): 𝔹 × 𝔹 {
    var q1 := false: 𝔹;
    var q2 := false: 𝔹;
    q1 := H(q1);
    q2 := CNOT(q1, q2);
    return (q1, q2);
}
```

### Grover 搜索

**Q#**:
```qsharp
operation GroverSearch(target: Int) : Int {
    use register = Qubit[2];
    
    // 初始化叠加态
    ApplyToEach(H, register);
    
    // Oracle
    Controlled Z(register, target);
    
    // 扩散
    ApplyToEach(H, register);
    ApplyToEach(X, register);
    Controlled Z(register, 0);
    ApplyToEach(X, register);
    ApplyToEach(H, register);
    
    return MeasureInteger(register);
}
```

**Silq**:
```silq
def groverSearch[target: !ℕ](): !ℕ {
    var qubits := vector(2, false: 𝔹);
    
    // 初始化叠加态
    for i in [0..2) {
        qubits[i] := H(qubits[i]);
    }
    
    // Oracle (简化)
    // ...
    
    // 扩散
    for i in [0..2) {
        qubits[i] := H(qubits[i]);
        qubits[i] := X(qubits[i]);
    }
    // ...
    
    return measure(qubits) as !ℕ;
}
```

---

## 八、Silq 优势与限制

### 优势

1. **自动反计算**: 无需手动清理临时量子比特
2. **强类型系统**: 编译时检查经典/量子类型混用
3. **简洁语法**: 代码量通常比 Q# 少 30-50%
4. **经典 - 量子混合**: 无缝集成经典和量子计算
5. **函数式风格**: 更接近数学表达

### 限制

1. **硬件支持有限**: 主要作为高级语言，需要编译到 Q#/Qiskit
2. **库生态较小**: 相比 Q# 的标准库，Silq 库较少
3. **调试工具**: 调试支持不如 Q# 成熟
4. **社区规模**: 用户社区较小，资源有限
5. **命令式操作**: 某些命令式操作不如 Q# 直接

---

## 九、最佳实践

### Silq 编程建议

1. **利用自动反计算**: 让 Silq 处理临时变量清理
2. **明确类型注解**: 特别是经典 vs 量子类型
3. **使用 lifted 注解**: 标记可自动反计算的函数
4. **避免不必要的测量**: 测量会破坏叠加态
5. **使用向量操作**: 批量操作量子比特

### 从 Q# 迁移到 Silq

1. 将 `using` 块改为变量声明
2. 移除 `within/apply` 结构
3. 将 `operation` 改为 `def`
4. 将 `Qubit` 改为 `𝔹`
5. 将 `M(q)` 改为 `measure(q)`
6. 使用 `!τ` 标记经典类型

---

## 十、参考资源

- [Silq 官方网站](https://silq.ethz.ch)
- [Silq 文档](https://silq.ethz.ch/documentation)
- [Silq GitHub](https://github.com/eth-sri/silq)
- [Quantum Computing with Silq Programming (Packt)](https://www.packtpub.com/product/quantum-computing-with-silq-programming/9781800569669)
