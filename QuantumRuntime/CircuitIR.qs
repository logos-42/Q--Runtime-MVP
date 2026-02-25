/// 模块：量子电路中间表示（IR）
namespace QuantumRuntime.CircuitIR {

    open Microsoft.Quantum.Intrinsic;

    /// 电路资源成本
    newtype ResourceCost = (gateCount: Int, tGateCount: Int, depthEstimate: Int, qubitCount: Int);

    /// 电路块
    newtype CircuitBlock = (name: String, totalCost: ResourceCost, instructions: Int[]);

    /// 创建电路块
    operation CreateCircuitBlock(name : String) : CircuitBlock {
        return CircuitBlock(name, ResourceCost(0, 0, 0, 0), []);
    }

    /// 创建指令
    operation CreateInstruction(id : Int, gateType : Int, qubits : Int[], tCount : Int) : (Int, Int, Int[], Int) {
        return (id, gateType, qubits, tCount);
    }

    /// 添加指令到电路块
    operation AddInstructionToBlock(block : CircuitBlock, instruction : (Int, Int, Int[], Int)) : CircuitBlock {
        let oldCost = block::totalCost;
        let newQubitCount = MaxInt(oldCost::qubitCount, Length(instruction[2]));
        let newCost = ResourceCost(
            oldCost::gateCount + 1,
            oldCost::tGateCount + instruction[3],
            oldCost::depthEstimate + 1,
            newQubitCount
        );
        let newInstructions = block::instructions + [instruction[0]];
        return CircuitBlock(block::name, newCost, newInstructions);
    }

    /// 嵌套电路块
    operation NestCircuitBlock(parent : CircuitBlock, child : CircuitBlock) : CircuitBlock {
        let parentCost = parent::totalCost;
        let childCost = child::totalCost;
        let newCost = ResourceCost(
            parentCost::gateCount + childCost::gateCount,
            parentCost::tGateCount + childCost::tGateCount,
            parentCost::depthEstimate + childCost::depthEstimate,
            MaxInt(parentCost::qubitCount, childCost::qubitCount)
        );
        return CircuitBlock(parent::name, newCost, parent::instructions);
    }

    /// 获取总资源成本
    operation GetTotalResourceCost(block : CircuitBlock) : ResourceCost {
        return block::totalCost;
    }

    /// 打印电路信息
    operation PrintCircuitInfo(block : CircuitBlock) : Unit {
        let cost = block::totalCost;
        Message($"Circuit: {block::name}");
        Message($"  Gates: {cost::gateCount}");
        Message($"  Qubits: {cost::qubitCount}");
        Message($"  Depth: {cost::depthEstimate}");
    }

    /// 最大值函数
    function MaxInt(a : Int, b : Int) : Int {
        if a > b {
            return a;
        } else {
            return b;
        }
    }
}
