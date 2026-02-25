namespace QuantumRuntime.QubitPool {
    
    // 定义 Qubit 记录类型别名，便于使用
    // QubitRecord = (id, state, operationCount, lastAccessTime, isEntangled)
    // state: 0 = Free, 1 = Allocated
    
    operation InitializeQubitPool(numQubits: Int) : (Int, Int, Int[], (Int, Int, Int, Int, Bool)[]) {
        mutable records = [];
        for i in 0..numQubits - 1 {
            set records = records + [(i, 0, 0, 0, false)];
        }
        return (numQubits, numQubits, [], records);
    }

    operation AllocateQubit(pool: (Int, Int, Int[], (Int, Int, Int, Int, Bool)[])) : (Int, (Int, Int, Int[], (Int, Int, Int, Int, Bool)[])) {
        let (total, free, reserved, records) = pool;
        if free <= 0 {
            fail "No free qubits";
        }
        
        mutable resultId = -1;
        mutable updatedRecords = [];
        
        // 遍历记录，查找第一个空闲 qubit 并更新状态
        for record in records {
            let (id, state, opCount, lastAccess, entangled) = record;
            if state == 0 and resultId == -1 {
                // 找到空闲 qubit，分配它
                set resultId = id;
                set updatedRecords = updatedRecords + [(id, 1, opCount, lastAccess, entangled)];
            } else {
                set updatedRecords = updatedRecords + [record];
            }
        }
        
        if resultId == -1 {
            fail "No free qubits found in records";
        }
        
        // 返回更新后的池状态
        return (resultId, (total, free - 1, reserved, updatedRecords));
    }

    operation ReleaseQubit(qubitId: Int, pool: (Int, Int, Int[], (Int, Int, Int, Int, Bool)[])) : (Int, Int, Int[], (Int, Int, Int, Int, Bool)[]) {
        let (total, free, reserved, records) = pool;
        mutable updatedRecords = [];
        mutable found = false;
        
        // 遍历记录，查找指定的 qubit 并释放
        for record in records {
            let (id, state, opCount, lastAccess, entangled) = record;
            if id == qubitId {
                // 释放 qubit：将 state 从 1 改回 0
                set updatedRecords = updatedRecords + [(id, 0, opCount, lastAccess, entangled)];
                set found = true;
            } else {
                set updatedRecords = updatedRecords + [record];
            }
        }
        
        if not found {
            fail $"Qubit {qubitId} not found";
        }
        
        // 返回更新后的池状态
        return (total, free + 1, reserved, updatedRecords);
    }

    operation GetPoolStats(pool: (Int, Int, Int[], (Int, Int, Int, Int, Bool)[])) : (Int, Int, Int) {
        let (total, free, reserved, records) = pool;
        return (total, free, Length(reserved));
    }
}
