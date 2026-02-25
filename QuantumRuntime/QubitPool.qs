/// 模块：Qubit 资源池
/// 
/// 核心设计：
/// - 跟踪 qubit 的生命周期（分配、使用、释放）
/// - 抽象 qubit 为有状态的资源对象
/// - 支持资源估计和约束检查

namespace QuantumRuntime.QubitPool {
    
    /// Qubit 的状态枚举
    enum QubitState {
        Free,           // 可用
        Allocated,      // 已分配但未使用
        InUse,          // 正在使用
        BorrowedByGate, // 被 gate 借用
        Released        // 已释放
    }

    /// 单个 qubit 的资源描述
    newtype QubitRecord = (
        id: Int,                    // qubit 唯一标识
        state: QubitState,          // 当前状态
        operationCount: Int,        // 经历过多少个操作
        lastAccessTime: Int,        // 上次访问时间戳
        parityBuffer: Bool          // 可逆计算的奇偶缓冲区
    );

    /// Qubit 资源池：集中管理所有 qubit
    newtype QubitPoolManager = (
        totalQubits: Int,
        freeCount: Int,
        reservedQubits: Int[],     // 预留的 qubit ID
        qubitRecords: QubitRecord[] // 所有 qubit 的状态记录
    );

    /// 初始化一个包含 n 个 qubit 的资源池
    operation InitializeQubitPool(numQubits: Int) : QubitPoolManager {
        let initialRecords = [
            QubitRecord(i, QubitState.Free, 0, 0, false)
            | i in 0..numQubits - 1
        ];
        return QubitPoolManager(
            numQubits,
            numQubits,
            [],
            initialRecords
        );
    }

    /// 从池中请求一个 qubit
    operation AllocateQubit(pool: QubitPoolManager) : (Int, QubitPoolManager) {
        if pool::freeCount <= 0 {
            fail "No free qubits available in pool";
        }

        // 简化：找第一个空闲的
        mutable resultId = -1;
        mutable updated = pool;

        for i in 0..Length(pool::qubitRecords) - 1 {
            if pool::qubitRecords[i]::state == QubitState.Free {
                resultId = pool::qubitRecords[i]::id;
                
                // 更新状态为已分配
                let updatedRecord = QubitRecord(
                    resultId,
                    QubitState.Allocated,
                    pool::qubitRecords[i]::operationCount,
                    pool::qubitRecords[i]::lastAccessTime,
                    pool::qubitRecords[i]::parityBuffer
                );
                
                set updated = QubitPoolManager(
                    pool::totalQubits,
                    pool::freeCount - 1,
                    pool::reservedQubits,
                    [
                        if j == i then updatedRecord else pool::qubitRecords[j]
                        | j in 0..Length(pool::qubitRecords) - 1
                    ]
                );
                
                break;
            }
        }

        return (resultId, updated);
    }

    /// 检查 qubit 的操作计数
    operation GetOperationCount(qubitId: Int, pool: QubitPoolManager) : Int {
        for record in pool::qubitRecords {
            if record::id == qubitId {
                return record::operationCount;
            }
        }
        fail "Qubit not found";
    }

    /// 释放 qubit 回到池中
    operation ReleaseQubit(qubitId: Int, pool: QubitPoolManager) : QubitPoolManager {
        mutable updated = pool;

        for i in 0..Length(pool::qubitRecords) - 1 {
            if pool::qubitRecords[i]::id == qubitId {
                let updatedRecord = QubitRecord(
                    qubitId,
                    QubitState.Free,
                    pool::qubitRecords[i]::operationCount,
                    pool::qubitRecords[i]::lastAccessTime,
                    false // 重置奇偶缓冲区
                );
                
                set updated = QubitPoolManager(
                    pool::totalQubits,
                    pool::freeCount + 1,
                    pool::reservedQubits,
                    [
                        if j == i then updatedRecord else pool::qubitRecords[j]
                        | j in 0..Length(pool::qubitRecords) - 1
                    ]
                );
                break;
            }
        }

        return updated;
    }

    /// 获取池的当前状态（资源估计）
    operation GetPoolStats(pool: QubitPoolManager) : (Int, Int, Int) {
        return (pool::totalQubits, pool::freeCount, Length(pool::reservedQubits));
    }
}
