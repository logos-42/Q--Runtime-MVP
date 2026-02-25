/// 模块：Qubit 资源池
namespace QuantumRuntime.QubitPool {

    /// Qubit 状态
    enum QubitState {
        Free = 0,
        Allocated = 1,
        InUse = 2,
        Released = 3
    }

    /// Qubit 记录
    newtype QubitRecord = (id: Int, state: QubitState, operationCount: Int);

    /// Qubit 池管理器
    newtype QubitPoolManager = (totalQubits: Int, freeCount: Int, qubitRecords: QubitRecord[]);

    /// 初始化 Qubit 池
    operation InitializeQubitPool(numQubits : Int) : QubitPoolManager {
        let records = [
            QubitRecord(i, QubitState.Free, 0)
            | i in 0..numQubits - 1
        ];
        return QubitPoolManager(numQubits, numQubits, records);
    }

    /// 分配 Qubit
    operation AllocateQubit(pool : QubitPoolManager) : (Int, QubitPoolManager) {
        if pool::freeCount <= 0 {
            fail "No free qubits";
        }

        mutable found = -1;
        mutable newRecords = pool::qubitRecords;

        for i in 0..Length(pool::qubitRecords) - 1 {
            if pool::qubitRecords[i]::state == QubitState.Free {
                set found = i;
                let record = pool::qubitRecords[i];
                let updatedRecord = QubitRecord(record::id, QubitState.Allocated, record::operationCount + 1);
                set newRecords = [
                    if j == i then updatedRecord else pool::qubitRecords[j]
                    | j in 0..Length(pool::qubitRecords) - 1
                ];
                break;
            }
        }

        let qubitId = pool::qubitRecords[found]::id;
        let newPool = QubitPoolManager(pool::totalQubits, pool::freeCount - 1, newRecords);
        return (qubitId, newPool);
    }

    /// 释放 Qubit
    operation ReleaseQubit(qubitId : Int, pool : QubitPoolManager) : QubitPoolManager {
        let newRecords = [
            if r::id == qubitId then QubitRecord(r::id, QubitState.Free, r::operationCount) else r
            | r in pool::qubitRecords
        ];
        return QubitPoolManager(pool::totalQubits, pool::freeCount + 1, newRecords);
    }

    /// 获取池统计
    operation GetPoolStats(pool : QubitPoolManager) : (Int, Int, Int) {
        return (pool::totalQubits, pool::freeCount, Length(pool::qubitRecords));
    }
}
