namespace QuantumRuntime.CircuitIR {
    
    // 直接使用元组类型
    // ResourceCost = (gateCount, tGateCount, depthEstimate, qubitCount)
    
    operation CreateCircuitBlock(name: String) : (String, (Int, Int, Int, Int), (Int, Int, Int, Int)[]) {
        return (name, (0,0,0,0), []);
    }

    operation GetCost(block: (String, (Int, Int, Int, Int), (Int, Int, Int, Int)[])) : (Int, Int, Int, Int) {
        return block[1];
    }
}
