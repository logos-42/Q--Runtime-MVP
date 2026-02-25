/// 模块：量子电路中间表示（IR）
///
/// 核心设计：
/// - 定义量子门和电路块的抽象表示
/// - 支持电路的构建、组合和验证
/// - 提供电路资源估计（深度、qubit 数量）
/// - 采用函数式不可变数据结构
///
/// 关键考虑：
/// - Q# 是面向量子执行的，电路 IR 是经典数据结构
/// - 使用 newtype 封装实现类型安全
/// - 所有操作返回新实例，保持不可变性

namespace QuantumRuntime.CircuitIR {

    // ============================================================
    // 基础门类型定义
    // ============================================================

    /// 量子门类型枚举
    /// 包含标准量子门集合，用于构建量子电路
    enum GateType {
        // --- 单量子比特门 ---
        H,      // Hadamard 门
        X,      // Pauli-X 门（非门）
        Y,      // Pauli-Y 门
        Z,      // Pauli-Z 门
        S,      // S 门（相位门，sqrt(Z)）
        T,      // T 门（π/8 门）
        Rx,     // X 轴旋转门（带角度参数）
        Ry,     // Y 轴旋转门（带角度参数）
        Rz,     // Z 轴旋转门（带角度参数）
        Id,     // 恒等门

        // --- 双量子比特门 ---
        CNOT,   // 受控非门
        CZ,     // 受控 Z 门
        SWAP,   // 交换门
        CY,     // 受控 Y 门

        // --- 三量子比特门 ---
        CCNOT,  // Toffoli 门（受控受控非门）
        CSWAP,  // 受控交换门（Fredkin 门）

        // --- 测量操作 ---
        MResetZ // 测量并重置为 |0⟩
    }

    /// 门类别枚举
    /// 用于快速判断门的性质（可逆性、所需 qubit 数等）
    enum GateCategory {
        SingleQubit,    // 单量子比特门
        TwoQubit,       // 双量子比特门
        ThreeQubit,     // 三量子比特门
        Measurement     // 测量操作（不可逆）
    }

    /// 电路指令
    /// 表示电路中的单个门操作
    newtype Instruction = (
        id: Int,                    // 指令唯一标识
        gateType: GateType,         // 门类型
        targets: Int[],             // 目标 qubit 索引列表
        parameters: Double[],       // 门参数（如旋转角度）
        depth: Int                  // 指令在电路中的深度层级
    );

    /// 嵌套电路引用
    /// 用于表示电路中嵌套的子电路块
    newtype NestedCircuitRef = (
        id: Int,                    // 引用唯一标识
        subCircuit: CircuitBlock,   // 嵌套的子电路块
        qubitMap: Int[],            // qubit 映射（子电路 qubit -> 父电路 qubit）
        depth: Int                  // 嵌套电路的深度层级
    );

    // ============================================================
    // 电路块表示
    // ============================================================

    /// 电路资源成本估计
    /// 用于快速评估电路的资源需求
    newtype ResourceCost = (
        qubitCount: Int,            // 所需 qubit 数量
        depthEstimate: Int,         // 电路深度估计
        gateCount: Int,             // 总门数量
        twoQubitGateCount: Int,     // 双量子比特门数量（关键资源）
        measurementCount: Int       // 测量操作数量
    );

    /// 电路块（Circuit Block）
    /// 量子电路的基本构建单元，可组合和嵌套
    newtype CircuitBlock = (
        name: String,                       // 电路名称
        instructions: Instruction[],        // 门操作序列
        nestedCircuits: NestedCircuitRef[], // 嵌套的子电路
        totalCost: ResourceCost,            // 总资源成本估计
        isReversible: Bool,                 // 是否可逆（无测量操作）
        qubitList: Int[]                    // 使用的 qubit 索引列表
    );

    // ============================================================
    // 电路构建操作
    // ============================================================

    /// 创建空电路块
    ///
    /// # Parameters
    /// - `name`: 电路名称
    ///
    /// # Returns
    /// 空的 CircuitBlock 实例
    operation CreateCircuitBlock(name: String) : CircuitBlock {
        return CircuitBlock(
            name,
            [],
            [],
            ResourceCost(0, 0, 0, 0, 0),
            true,   // 空电路是可逆的
            []
        );
    }

    /// 获取门类型对应的类别
    ///
    /// # Parameters
    /// - `gateType`: 门类型枚举值
    ///
    /// # Returns
    /// 门的类别（单/双/三量子比特或测量）
    function GetGateCategory(gateType: GateType) : GateCategory {
        if gateType == GateType.MResetZ {
            return GateCategory.Measurement;
        }
        elif gateType == GateType.CCNOT or gateType == GateType.CSWAP {
            return GateCategory.ThreeQubit;
        }
        elif gateType == GateType.CNOT or gateType == GateType.CZ or 
             gateType == GateType.SWAP or gateType == GateType.CY {
            return GateCategory.TwoQubit;
        }
        else {
            return GateCategory.SingleQubit;
        }
    }

    /// 获取门所需的 qubit 数量
    ///
    /// # Parameters
    /// - `gateType`: 门类型枚举值
    ///
    /// # Returns
    /// 门所需的 qubit 数量
    function GetGateQubitCount(gateType: GateType) : Int {
        let category = GetGateCategory(gateType);
        return category == GateCategory.SingleQubit ? 1 |
               category == GateCategory.TwoQubit ? 2 |
               category == GateCategory.ThreeQubit ? 3 |
               1;  // Measurement
    }

    /// 检查门是否需要参数
    ///
    /// # Parameters
    /// - `gateType`: 门类型枚举值
    ///
    /// # Returns
    /// 是否需要参数
    function GateRequiresParameters(gateType: GateType) : Bool {
        return gateType == GateType.Rx or 
               gateType == GateType.Ry or 
               gateType == GateType.Rz;
    }

    /// 获取门所需的参数数量
    ///
    /// # Parameters
    /// - `gateType`: 门类型枚举值
    ///
    /// # Returns
    /// 参数数量
    function GetGateParameterCount(gateType: GateType) : Int {
        return GateRequiresParameters(gateType) ? 1 | 0;
    }

    /// 检查门是否可逆
    ///
    /// # Parameters
    /// - `gateType`: 门类型枚举值
    ///
    /// # Returns
    /// 门是否可逆
    function IsGateReversible(gateType: GateType) : Bool {
        return gateType != GateType.MResetZ;
    }

    /// 创建单个门指令
    ///
    /// # Parameters
    /// - `id`: 指令唯一标识
    /// - `gateType`: 门类型
    /// - `targets`: 目标 qubit 索引列表
    /// - `depth`: 电路深度层级
    ///
    /// # Returns
    /// 新的 Instruction 实例
    operation CreateInstruction(
        id: Int,
        gateType: GateType,
        targets: Int[],
        depth: Int
    ) : Instruction {
        return Instruction(id, gateType, targets, [], depth);
    }

    /// 创建带参数的门指令（用于旋转门）
    ///
    /// # Parameters
    /// - `id`: 指令唯一标识
    /// - `gateType`: 门类型（必须是 Rx, Ry, Rz 之一）
    /// - `targets`: 目标 qubit 索引列表
    /// - `parameters`: 门参数列表（如旋转角度，单位：弧度）
    /// - `depth`: 电路深度层级
    ///
    /// # Returns
    /// 新的 Instruction 实例
    operation CreateInstructionWithParams(
        id: Int,
        gateType: GateType,
        targets: Int[],
        parameters: Double[],
        depth: Int
    ) : Instruction {
        return Instruction(id, gateType, targets, parameters, depth);
    }

    /// 计算添加指令后的新深度
    ///
    /// # Parameters
    /// - `currentDepth`: 当前电路深度
    /// - `instruction`: 要添加的指令
    /// - `existingInstructions`: 现有指令列表
    ///
    /// # Returns
    /// 新的深度值
    function CalculateNewDepth(
        currentDepth: Int,
        instruction: Instruction,
        existingInstructions: Instruction[]
    ) : Int {
        // 简化：每个指令增加一层深度
        // 更复杂的实现可以考虑并行门
        return currentDepth + 1;
    }

    /// 更新电路的资源成本
    ///
    /// # Parameters
    /// - `currentCost`: 当前资源成本
    /// - `instruction`: 新添加的指令
    ///
    /// # Returns
    /// 更新后的资源成本
    function UpdateResourceCost(currentCost: ResourceCost, instruction: Instruction) : ResourceCost {
        let gateType = instruction::gateType;
        let category = GetGateCategory(gateType);
        
        let newQubitCount = Max(currentCost::qubitCount, Length(instruction::targets));
        let newDepth = currentCost::depthEstimate + 1;
        let newGateCount = currentCost::gateCount + 1;
        let newTwoQubitCount = currentCost::twoQubitGateCount + 
                               (category == GateCategory.TwoQubit ? 1 | 0);
        let newMeasurementCount = currentCost::measurementCount + 
                                  (category == GateCategory.Measurement ? 1 | 0);

        return ResourceCost(
            newQubitCount,
            newDepth,
            newGateCount,
            newTwoQubitCount,
            newMeasurementCount
        );
    }

    /// 更新电路的 qubit 列表
    ///
    /// # Parameters
    /// - `currentQubitList`: 当前 qubit 列表
    /// - `instruction`: 新添加的指令
    ///
    /// # Returns
    /// 更新后的 qubit 列表（去重）
    function UpdateQubitList(currentQubitList: Int[], instruction: Instruction) : Int[] {
        let targets = instruction::targets;
        mutable result = currentQubitList;
        
        for qubit in targets {
            mutable found = false;
            for existing in result {
                if existing == qubit {
                    set found = true;
                    break;
                }
            }
            if not found {
                set result = result + [qubit];
            }
        }
        
        // 排序 qubit 列表
        // 简化：返回未排序的列表（Q# 标准库排序较复杂）
        return result;
    }

    /// 添加门指令到电路块
    ///
    /// # Parameters
    /// - `circuit`: 原电路块
    /// - `instruction`: 要添加的指令
    ///
    /// # Returns
    /// 更新后的 CircuitBlock 实例
    operation AddInstructionToBlock(circuit: CircuitBlock, instruction: Instruction) : CircuitBlock {
        let newInstructions = circuit::instructions + [instruction];
        let newCost = UpdateResourceCost(circuit::totalCost, instruction);
        let newQubitList = UpdateQubitList(circuit::qubitList, instruction);
        
        // 检查是否仍可逆
        let category = GetGateCategory(instruction::gateType);
        let stillReversible = circuit::isReversible and (category != GateCategory.Measurement);

        return CircuitBlock(
            circuit::name,
            newInstructions,
            circuit::nestedCircuits,
            newCost,
            stillReversible,
            newQubitList
        );
    }

    /// 添加旋转门指令到电路块
    ///
    /// # Parameters
    /// - `circuit`: 原电路块
    /// - `instruction`: 要添加的带参数指令
    ///
    /// # Returns
    /// 更新后的 CircuitBlock 实例
    operation AddInstructionWithParamsToBlock(circuit: CircuitBlock, instruction: Instruction) : CircuitBlock {
        let newInstructions = circuit::instructions + [instruction];
        let newCost = UpdateResourceCost(circuit::totalCost, instruction);
        let newQubitList = UpdateQubitList(circuit::qubitList, instruction);
        
        let category = GetGateCategory(instruction::gateType);
        let stillReversible = circuit::isReversible and (category != GateCategory.Measurement);

        return CircuitBlock(
            circuit::name,
            newInstructions,
            circuit::nestedCircuits,
            newCost,
            stillReversible,
            newQubitList
        );
    }

    /// 合并两个电路的资源成本
    ///
    /// # Parameters
    /// - `cost1`: 第一个电路的成本
    /// - `cost2`: 第二个电路的成本
    ///
    /// # Returns
    /// 合并后的资源成本
    function MergeResourceCost(cost1: ResourceCost, cost2: ResourceCost) : ResourceCost {
        return ResourceCost(
            Max(cost1::qubitCount, cost2::qubitCount),
            cost1::depthEstimate + cost2::depthEstimate,
            cost1::gateCount + cost2::gateCount,
            cost1::twoQubitGateCount + cost2::twoQubitGateCount,
            cost1::measurementCount + cost2::measurementCount
        );
    }

    /// 合并两个电路的 qubit 列表
    ///
    /// # Parameters
    /// - `list1`: 第一个电路的 qubit 列表
    /// - `list2`: 第二个电路的 qubit 列表
    ///
    /// # Returns
    /// 合并后的 qubit 列表（去重）
    function MergeQubitLists(list1: Int[], list2: Int[]) : Int[] {
        mutable result = list1;
        
        for qubit in list2 {
            mutable found = false;
            for existing in result {
                if existing == qubit {
                    set found = true;
                    break;
                }
            }
            if not found {
                set result = result + [qubit];
            }
        }
        
        return result;
    }

    /// 组合两个电路块（串行连接）
    /// 将 circuit2 连接到 circuit1 之后
    ///
    /// # Parameters
    /// - `circuit1`: 第一个电路块
    /// - `circuit2`: 第二个电路块
    ///
    /// # Returns
    /// 组合后的 CircuitBlock 实例
    operation CombineCircuitBlocks(circuit1: CircuitBlock, circuit2: CircuitBlock) : CircuitBlock {
        let combinedName = circuit1::name + "+" + circuit2::name;
        
        // 调整 circuit2 指令的深度
        let depthOffset = circuit1::totalCost::depthEstimate;
        let adjustedInstructions2 = [
            Instruction(
                instr::id,
                instr::gateType,
                instr::targets,
                instr::parameters,
                instr::depth + depthOffset
            )
            | instr in circuit2::instructions
        ];

        let combinedInstructions = circuit1::instructions + adjustedInstructions2;
        let combinedNested = circuit1::nestedCircuits + circuit2::nestedCircuits;
        let combinedCost = MergeResourceCost(circuit1::totalCost, circuit2::totalCost);
        let combinedQubitList = MergeQubitLists(circuit1::qubitList, circuit2::qubitList);
        let combinedReversible = circuit1::isReversible and circuit2::isReversible;

        return CircuitBlock(
            combinedName,
            combinedInstructions,
            combinedNested,
            combinedCost,
            combinedReversible,
            combinedQubitList
        );
    }

    /// 将子电路嵌套到父电路中
    /// 使用 qubitMap 将子电路的 qubit 映射到父电路的 qubit
    ///
    /// # Parameters
    /// - `parentCircuit`: 父电路块
    /// - `subCircuit`: 要嵌套的子电路块
    ///
    /// # Returns
    /// 包含嵌套电路的新 CircuitBlock 实例
    operation NestCircuitBlock(parentCircuit: CircuitBlock, subCircuit: CircuitBlock) : CircuitBlock {
        let nestedId = Length(parentCircuit::nestedCircuits);
        
        // 创建 qubit 映射（简化：直接映射）
        let qubitMap = subCircuit::qubitList;
        
        let nestedRef = NestedCircuitRef(
            nestedId,
            subCircuit,
            qubitMap,
            parentCircuit::totalCost::depthEstimate
        );

        let newNestedCircuits = parentCircuit::nestedCircuits + [nestedRef];
        
        // 更新资源成本（加上子电路的成本）
        let newCost = MergeResourceCost(parentCircuit::totalCost, subCircuit::totalCost);
        let newQubitList = MergeQubitLists(parentCircuit::qubitList, subCircuit::qubitList);
        let newReversible = parentCircuit::isReversible and subCircuit::isReversible;

        return CircuitBlock(
            parentCircuit::name,
            parentCircuit::instructions,
            newNestedCircuits,
            newCost,
            newReversible,
            newQubitList
        );
    }

    // ============================================================
    // 电路验证操作
    // ============================================================

    /// 检查电路是否可逆
    /// 可逆电路不包含测量操作
    ///
    /// # Parameters
    /// - `circuit`: 要检查的电路块
    ///
    /// # Returns
    /// 电路是否可逆
    operation IsCircuitReversible(circuit: CircuitBlock) : Bool {
        // 检查直接指令
        for instr in circuit::instructions {
            if not IsGateReversible(instr::gateType) {
                return false;
            }
        }
        
        // 检查嵌套电路
        for nested in circuit::nestedCircuits {
            if not nested::subCircuit::isReversible {
                return false;
            }
        }
        
        return circuit::isReversible;
    }

    /// 验证 qubit 索引的有效性
    /// 检查所有指令的 qubit 索引是否在有效范围内
    ///
    /// # Parameters
    /// - `circuit`: 要验证的电路块
    /// - `maxQubitIndex`: 允许的最大 qubit 索引
    ///
    /// # Returns
    /// (是否有效，错误信息列表)
    operation ValidateQubitIndices(circuit: CircuitBlock, maxQubitIndex: Int) : (Bool, String[]) {
        mutable errors = [];
        
        // 检查直接指令
        for instr in circuit::instructions {
            for qubit in instr::targets {
                if qubit < 0 {
                    set errors = errors + [$"Invalid qubit index {qubit}: must be non-negative"];
                }
                elif qubit > maxQubitIndex {
                    set errors = errors + [$"Qubit index {qubit} exceeds maximum {maxQubitIndex}"];
                }
            }
        }
        
        // 检查嵌套电路
        for nested in circuit::nestedCircuits {
            let (nestedValid, nestedErrors) = ValidateQubitIndices(nested::subCircuit, maxQubitIndex);
            if not nestedValid {
                set errors = errors + nestedErrors;
            }
        }
        
        return (Length(errors) == 0, errors);
    }

    /// 验证门参数的合法性
    /// 检查旋转门等需要参数的门是否有正确的参数
    ///
    /// # Parameters
    /// - `circuit`: 要验证的电路块
    ///
    /// # Returns
    /// (是否有效，错误信息列表)
    operation ValidateGateParameters(circuit: CircuitBlock) : (Bool, String[]) {
        mutable errors = [];
        
        // 检查直接指令
        for instr in circuit::instructions {
            let gateType = instr::gateType;
            let paramCount = Length(instr::parameters);
            let expectedCount = GetGateParameterCount(gateType);
            
            if GateRequiresParameters(gateType) and paramCount != expectedCount {
                set errors = errors + [
                    $"Gate {gateType} requires {expectedCount} parameters, got {paramCount}"
                ];
            }
            
            // 额外检查：旋转角度应在合理范围内（可选）
            if gateType == GateType.Rx or gateType == GateType.Ry or gateType == GateType.Rz {
                for param in instr::parameters {
                    if param < -4.0 * 3.141592653589793 or param > 4.0 * 3.141592653589793 {
                        set errors = errors + [
                            $"Rotation angle {param} may be outside typical range [-4π, 4π]"
                        ];
                    }
                }
            }
        }
        
        // 检查嵌套电路
        for nested in circuit::nestedCircuits {
            let (nestedValid, nestedErrors) = ValidateGateParameters(nested::subCircuit);
            if not nestedValid {
                set errors = errors + nestedErrors;
            }
        }
        
        return (Length(errors) == 0, errors);
    }

    /// 验证电路的完整性
    /// 综合检查 qubit 索引、门参数、电路结构等
    ///
    /// # Parameters
    /// - `circuit`: 要验证的电路块
    /// - `maxQubitIndex`: 允许的最大 qubit 索引
    ///
    /// # Returns
    /// (是否有效，错误信息列表)
    operation ValidateCircuit(circuit: CircuitBlock, maxQubitIndex: Int) : (Bool, String[]) {
        mutable allErrors = [];
        
        // 验证 qubit 索引
        let (qubitsValid, qubitErrors) = ValidateQubitIndices(circuit, maxQubitIndex);
        if not qubitsValid {
            set allErrors = allErrors + qubitErrors;
        }
        
        // 验证门参数
        let (paramsValid, paramErrors) = ValidateGateParameters(circuit);
        if not paramsValid {
            set allErrors = allErrors + paramErrors;
        }
        
        // 验证指令的 qubit 数量匹配
        for instr in circuit::instructions {
            let expectedQubits = GetGateQubitCount(instr::gateType);
            let actualQubits = Length(instr::targets);
            if actualQubits != expectedQubits {
                set allErrors = allErrors + [
                    $"Gate {instr::gateType} expects {expectedQubits} qubits, got {actualQubits}"
                ];
            }
        }
        
        return (Length(allErrors) == 0, allErrors);
    }

    /// 检查电路是否为空
    ///
    /// # Parameters
    /// - `circuit`: 要检查的电路块
    ///
    /// # Returns
    /// 电路是否为空（无指令且无嵌套电路）
    function IsCircuitEmpty(circuit: CircuitBlock) : Bool {
        return Length(circuit::instructions) == 0 and 
               Length(circuit::nestedCircuits) == 0;
    }

    // ============================================================
    // 电路展示操作
    // ============================================================

    /// 获取门类型的字符串表示
    ///
    /// # Parameters
    /// - `gateType`: 门类型枚举值
    ///
    /// # Returns
    /// 门的字符串表示
    function GateTypeToString(gateType: GateType) : String {
        return gateType == GateType.H ? "H" |
               gateType == GateType.X ? "X" |
               gateType == GateType.Y ? "Y" |
               gateType == GateType.Z ? "Z" |
               gateType == GateType.S ? "S" |
               gateType == GateType.T ? "T" |
               gateType == GateType.Rx ? "Rx" |
               gateType == GateType.Ry ? "Ry" |
               gateType == GateType.Rz ? "Rz" |
               gateType == GateType.Id ? "Id" |
               gateType == GateType.CNOT ? "CNOT" |
               gateType == GateType.CZ ? "CZ" |
               gateType == GateType.SWAP ? "SWAP" |
               gateType == GateType.CY ? "CY" |
               gateType == GateType.CCNOT ? "CCNOT" |
               gateType == GateType.CSWAP ? "CSWAP" |
               gateType == GateType.MResetZ ? "MResetZ" |
               "Unknown";
    }

    /// 获取指令的字符串表示
    ///
    /// # Parameters
    /// - `instruction`: 指令实例
    ///
    /// # Returns
    /// 指令的字符串表示
    function InstructionToString(instruction: Instruction) : String {
        let gateStr = GateTypeToString(instruction::gateType);
        let targetsStr = $"[{JoinInts(",", instruction::targets)}]";
        
        if Length(instruction::parameters) > 0 {
            let paramsStr = $"[{JoinDoubles(",", instruction::parameters)}]";
            return $"{gateStr}({paramsStr}) on qubit(s) {targetsStr}";
        }
        else {
            return $"{gateStr} on qubit(s) {targetsStr}";
        }
    }

    /// 打印电路信息（用于调试）
    ///
    /// # Parameters
    /// - `circuit`: 要打印的电路块
    operation PrintCircuitInfo(circuit: CircuitBlock) : Unit {
        Message($"=== Circuit: {circuit::name} ===");
        
        if IsCircuitEmpty(circuit) {
            Message("  (empty circuit)");
        }
        else {
            // 打印资源成本
            Message($"  Qubits Required: {circuit::totalCost::qubitCount}");
            Message($"  Circuit Depth:   {circuit::totalCost::depthEstimate}");
            Message($"  Total Gates:     {circuit::totalCost::gateCount}");
            Message($"  2-Qubit Gates:   {circuit::totalCost::twoQubitGateCount}");
            Message($"  Measurements:    {circuit::totalCost::measurementCount}");
            Message($"  Reversible:      {circuit::isReversible ? "Yes" | "No"}");
            Message("");
            
            // 打印指令列表
            if Length(circuit::instructions) > 0 {
                Message("  Instructions:");
                for i in 0..Length(circuit::instructions) - 1 {
                    let instr = circuit::instructions[i];
                    Message($"    [{i}] {InstructionToString(instr)}");
                }
            }
            
            // 打印嵌套电路
            if Length(circuit::nestedCircuits) > 0 {
                Message("  Nested Circuits:");
                for nested in circuit::nestedCircuits {
                    Message($"    - {nested::subCircuit::name} (mapped to qubits [{JoinInts(",", nested::qubitMap)}])");
                }
            }
        }
        
        Message("");
    }

    /// 获取电路的摘要信息
    ///
    /// # Parameters
    /// - `circuit`: 电路块
    ///
    /// # Returns
    /// 电路摘要字符串
    function GetCircuitSummary(circuit: CircuitBlock) : String {
        return $"Circuit '{circuit::name}': {circuit::totalCost::gateCount} gates, " +
               $"{circuit::totalCost::qubitCount} qubits, depth {circuit::totalCost::depthEstimate}";
    }

    /// 获取电路的详细资源报告
    ///
    /// # Parameters
    /// - `circuit`: 电路块
    ///
    /// # Returns
    /// 详细资源报告字符串
    function GetResourceReport(circuit: CircuitBlock) : String {
        let cost = circuit::totalCost;
        return 
            $"Resource Report for '{circuit::name}':\n" +
            $"  Qubit Count:      {cost::qubitCount}\n" +
            $"  Depth Estimate:   {cost::depthEstimate}\n" +
            $"  Gate Count:       {cost::gateCount}\n" +
            $"  2-Qubit Gates:    {cost::twoQubitGateCount}\n" +
            $"  Measurements:     {cost::measurementCount}\n" +
            $"  Reversible:       {circuit::isReversible ? "Yes" | "No"}";
    }

    // ============================================================
    // 辅助函数
    // ============================================================

    /// 计算电路的总资源成本（用于 TaskQueue 中的 GetTotalResourceCost）
    ///
    /// # Parameters
    /// - `circuit`: 电路块
    ///
    /// # Returns
    /// 电路的资源成本
    function GetTotalResourceCost(circuit: CircuitBlock) : ResourceCost {
        return circuit::totalCost;
    }

    /// 获取电路所需的 qubit 数量
    ///
    /// # Parameters
    /// - `circuit`: 电路块
    ///
    /// # Returns
    /// qubit 数量
    function GetCircuitQubitCount(circuit: CircuitBlock) : Int {
        return circuit::totalCost::qubitCount;
    }

    /// 获取电路的深度估计
    ///
    /// # Parameters
    /// - `circuit`: 电路块
    ///
    /// # Returns
    /// 电路深度
    function GetCircuitDepth(circuit: CircuitBlock) : Int {
        return circuit::totalCost::depthEstimate;
    }

    /// 获取电路中的门总数
    ///
    /// # Parameters
    /// - `circuit`: 电路块
    ///
    /// # Returns
    /// 门总数
    function GetGateCount(circuit: CircuitBlock) : Int {
        return circuit::totalCost::gateCount;
    }

    /// 获取电路中的双量子比特门数量
    ///
    /// # Parameters
    /// - `circuit`: 电路块
    ///
    /// # Returns
    /// 双量子比特门数量
    function GetTwoQubitGateCount(circuit: CircuitBlock) : Int {
        return circuit::totalCost::twoQubitGateCount;
    }

    /// 整数数组转字符串（辅助函数）
    ///
    /// # Parameters
    /// - `separator`: 分隔符
    /// - `values`: 整数数组
    ///
    /// # Returns
    /// 连接后的字符串
    function JoinInts(separator: String, values: Int[]) : String {
        if Length(values) == 0 {
            return "";
        }
        
        mutable result = $"{values[0]}";
        for i in 1..Length(values) - 1 {
            set result = result + separator + $"{values[i]}";
        }
        return result;
    }

    /// Double 数组转字符串（辅助函数）
    ///
    /// # Parameters
    /// - `separator`: 分隔符
    /// - `values`: Double 数组
    ///
    /// # Returns
    /// 连接后的字符串
    function JoinDoubles(separator: String, values: Double[]) : String {
        if Length(values) == 0 {
            return "";
        }
        
        mutable result = $"{values[0]}";
        for i in 1..Length(values) - 1 {
            set result = result + separator + $"{values[i]}";
        }
        return result;
    }
}
