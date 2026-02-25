/// 模块：Qubit 资源池
namespace QuantumRuntime.QubitPool {

    open Microsoft.Quantum.Intrinsic;

    // Qubit 状态常量
    function QubitState_Free() : Int { return 0; }
    function QubitState_Allocated() : Int { return 1; }
    function QubitState_InUse() : Int { return 2; }
    function QubitState_Released() : Int { return 3; }

    /// Qubit 记录
    newtype QubitRecord = (id: Int, state: Int, operationCount: Int);

    /// Qubit 池管理器
    newtype QubitPoolManager = (totalQubits: Int, freeCount: Int, qubitRecords: QubitRecord[]);

    /// 初始化 Qubit 池
    operation InitializeQubitPool(numQubits : Int) : QubitPoolManager {
        mutable records = [];
        for i in 0..numQubits - 1 {
            set records = records + [QubitRecord(i, QubitState_Free(), 0)];
        }
        return QubitPoolManager(numQubits, numQubits, records);
    }

    /// 分配 Qubit
    operation AllocateQubit(pool : QubitPoolManager) : (Int, QubitPoolManager) {
        if pool::freeCount <= 0 {
            fail "No free qubits";
        }

        mutable foundIndex = -1;
        
        // 查找第一个空闲 qubit
        for i in 0..Length(pool::qubitRecords) - 1 {
            if foundIndex == -1 and pool::qubitRecords[i]::state == QubitState_Free() {
                set foundIndex = i;
            }
        }

        if foundIndex == -1 {
            fail "No free qubits found";
        }

        // 构建新的记录数组
        mutable newRecords = [];
        for i in 0..Length(pool::qubitRecords) - 1 {
            if i == foundIndex {
                let oldRecord = pool::qubitRecords[i];
                set newRecords = newRecords + [QubitRecord(oldRecord::id, QubitState_Allocated(), oldRecord::operationCount + 1)];
            } else {
                set newRecords = newRecords + [pool::qubitRecords[i]];
            }
        }

        let qubitId = pool::qubitRecords[foundIndex]::id;
        let newPool = QubitPoolManager(pool::totalQubits, pool::freeCount - 1, newRecords);
        return (qubitId, newPool);
    }

    /// 释放 Qubit
    operation ReleaseQubit(qubitId : Int, pool : QubitPoolManager) : QubitPoolManager {
        mutable newRecords = [];
        for i in 0..Length(pool::qubitRecords) - 1 {
            let r = pool::qubitRecords[i];
            if r::id == qubitId {
                set newRecords = newRecords + [QubitRecord(r::id, QubitState_Free(), r::operationCount)];
            } else {
                set newRecords = newRecords + [r];
            }
        }
        return QubitPoolManager(pool::totalQubits, pool::freeCount + 1, newRecords);
    }

    /// 获取池统计
    operation GetPoolStats(pool : QubitPoolManager) : (Int, Int, Int) {
        return (pool::totalQubits, pool::freeCount, Length(pool::qubitRecords));
    }
}
