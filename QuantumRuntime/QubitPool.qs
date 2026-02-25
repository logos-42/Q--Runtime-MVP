namespace QuantumRuntime.QubitPool {

    open Microsoft.Quantum.Intrinsic;
    open Microsoft.Quantum.Canon;

    /// Qubit 状态枚举
    enum QubitState {
        Free,
        Allocated,
        InUse,
        BorrowedByGate,
        Released
    }

    /// 资源依赖信息：追踪 qubit 与其他资源的依赖关系
    newtype QubitDependency = (
        dependsOnQubits: Int[],     // 依赖的其他 qubit ID
        dependedByQubits: Int[],    // 被哪些 qubit 依赖
        entangledWith: Int[],       // 纠缠的 qubit ID
        canSafeUncompute: Bool      // 是否可安全 uncompute（无依赖且未纠缠）
    );

    /// Qubit 记录：增强版（添加依赖追踪）
    newtype QubitRecord = (
        id: Int,
        state: QubitState,
        operationCount: Int,
        lastAccessTime: Int,
        parityBuffer: Bool,
        dependency: QubitDependency   // 新增：依赖信息
    );

    /// Qubit 资源池管理器
    newtype QubitPoolManager = (
        totalQubits: Int,
        freeCount: Int,
        reservedQubits: Int[],
        qubitRecords: QubitRecord[]
    );
    
    /// 初始化 Qubit 资源池（增强版：包含依赖追踪）
    operation InitializeQubitPool(numQubits: Int) : QubitPoolManager {
        mutable records = [];
        for i in 0..numQubits - 1 {
            set records = records + [
                QubitRecord(i, QubitState::Free, 0, 0, false,
                    QubitDependency([], [], [], true))
            ];
        }
        return QubitPoolManager(numQubits, numQubits, [], records);
    }
    
    operation AllocateQubit(pool: QubitPoolManager) : (Int, QubitPoolManager) {
        if pool::freeCount <= 0 {
            fail "No free qubits available";
        }
        mutable resultId = -1;
        mutable updated = pool;
        for i in 0..Length(pool::qubitRecords) - 1 {
            if pool::qubitRecords[i]::state == QubitState::Free {
                set resultId = pool::qubitRecords[i]::id;
                let updatedRecord = QubitRecord(
                    resultId,
                    QubitState::Allocated,
                    pool::qubitRecords[i]::operationCount,
                    pool::qubitRecords[i]::lastAccessTime,
                    pool::qubitRecords[i]::parityBuffer,
                    pool::qubitRecords[i]::dependency
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
            }
        }
        return (resultId, updated);
    }

    /// 释放 Qubit（重置依赖信息）
    operation ReleaseQubit(qubitId: Int, pool: QubitPoolManager) : QubitPoolManager {
        mutable updated = pool;
        for i in 0..Length(pool::qubitRecords) - 1 {
            if pool::qubitRecords[i]::id == qubitId {
                let updatedRecord = QubitRecord(
                    qubitId,
                    QubitState::Free,
                    pool::qubitRecords[i]::operationCount,
                    pool::qubitRecords[i]::lastAccessTime,
                    false,
                    QubitDependency([], [], [], true)  // 重置依赖信息
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
            }
        }
        return updated;
    }
    
    operation GetPoolStats(pool: QubitPoolManager) : (Int, Int, Int) {
        return (pool::totalQubits, pool::freeCount, Length(pool::reservedQubits));
    }

    // ============================================
    // 改进 1：WithTempQubit 高级抽象
    // ============================================
    // 封装 within-apply 模式，简化临时 qubit 管理
    // 类似 Silq 的自动 uncomputation 体验

    /// 使用临时 Qubit 执行操作（自动清理）
    /// 封装 within-apply 模式，临时 qubit 自动重置
    operation WithTempQubit<T>(numQubits: Int, body: (Qubit[] => T)) : T {
        using (temp = Qubit[numQubits]) {
            within {
                // 自动初始化到 |0⟩（Q# 默认）
            } apply {
                return body(temp);
            }
            // 自动 ResetAll(temp)
        }
    }

    /// 使用单个临时 Qubit 执行操作
    operation WithSingleTempQubit<T>(body: (Qubit => T)) : T {
        return WithTempQubit(1, fun(qs) -> body(qs[0]));
    }

    /// 使用临时 Qubit 执行计算（带初始化）
    operation WithInitializedTempQubit<T>(
        numQubits: Int,
        init: (Qubit[] => Unit),
        body: (Qubit[] => T)
    ) : T {
        using (temp = Qubit[numQubits]) {
            within {
                init(temp);
            } apply {
                return body(temp);
            }
        }
    }

    // ============================================
    // 改进 3：资源依赖追踪操作
    // ============================================

    /// 记录两个 qubit 之间的纠缠关系
    operation RecordEntanglement(
        qubitId1: Int,
        qubitId2: Int,
        pool: QubitPoolManager
    ) : QubitPoolManager {
        mutable updated = pool;

        // 更新 qubit1 的纠缠列表
        for i in 0..Length(pool::qubitRecords) - 1 {
            if pool::qubitRecords[i]::id == qubitId1 {
                let oldDep = pool::qubitRecords[i]::dependency;
                let newEntangled = oldDep::entangledWith + [qubitId2];
                let newDep = QubitDependency(
                    oldDep::dependsOnQubits,
                    oldDep::dependedByQubits,
                    newEntangled,
                    false  // 有纠缠，不能安全 uncompute
                );
                let updatedRecord = QubitRecord(
                    pool::qubitRecords[i]::id,
                    pool::qubitRecords[i]::state,
                    pool::qubitRecords[i]::operationCount,
                    pool::qubitRecords[i]::lastAccessTime,
                    pool::qubitRecords[i]::parityBuffer,
                    newDep
                );
                set updated = QubitPoolManager(
                    pool::totalQubits,
                    pool::freeCount,
                    pool::reservedQubits,
                    [
                        if j == i then updatedRecord else pool::qubitRecords[j]
                        | j in 0..Length(pool::qubitRecords) - 1
                    ]
                );
            }
        }

        // 更新 qubit2 的纠缠列表（对称）
        for i in 0..Length(updated::qubitRecords) - 1 {
            if updated::qubitRecords[i]::id == qubitId2 {
                let oldDep = updated::qubitRecords[i]::dependency;
                let newEntangled = oldDep::entangledWith + [qubitId1];
                let newDep = QubitDependency(
                    oldDep::dependsOnQubits,
                    oldDep::dependedByQubits,
                    newEntangled,
                    false
                );
                let updatedRecord = QubitRecord(
                    updated::qubitRecords[i]::id,
                    updated::qubitRecords[i]::state,
                    updated::qubitRecords[i]::operationCount,
                    updated::qubitRecords[i]::lastAccessTime,
                    updated::qubitRecords[i]::parityBuffer,
                    newDep
                );
                set updated = QubitPoolManager(
                    updated::totalQubits,
                    updated::freeCount,
                    updated::reservedQubits,
                    [
                        if j == i then updatedRecord else updated::qubitRecords[j]
                        | j in 0..Length(updated::qubitRecords) - 1
                    ]
                );
            }
        }

        return updated;
    }

    /// 记录 qubit 依赖关系（用于电路调度）
    operation RecordDependency(
        qubitId: Int,
        dependsOn: Int[],
        pool: QubitPoolManager
    ) : QubitPoolManager {
        mutable updated = pool;

        for i in 0..Length(pool::qubitRecords) - 1 {
            if pool::qubitRecords[i]::id == qubitId {
                let oldDep = pool::qubitRecords[i]::dependency;
                let newDep = QubitDependency(
                    oldDep::dependsOnQubits + dependsOn,
                    oldDep::dependedByQubits,
                    oldDep::entangledWith,
                    false  // 有依赖，不能安全 uncompute
                );
                let updatedRecord = QubitRecord(
                    pool::qubitRecords[i]::id,
                    pool::qubitRecords[i]::state,
                    pool::qubitRecords[i]::operationCount,
                    pool::qubitRecords[i]::lastAccessTime,
                    pool::qubitRecords[i]::parityBuffer,
                    newDep
                );
                set updated = QubitPoolManager(
                    pool::totalQubits,
                    pool::freeCount,
                    pool::reservedQubits,
                    [
                        if j == i then updatedRecord else pool::qubitRecords[j]
                        | j in 0..Length(pool::qubitRecords) - 1
                    ]
                );
            }
        }

        return updated;
    }

    /// 检查 qubit 是否可安全 uncompute
    operation CanSafeUncompute(qubitId: Int, pool: QubitPoolManager) : Bool {
        for record in pool::qubitRecords {
            if record::id == qubitId {
                return record::dependency::canSafeUncompute;
            }
        }
        return false;
    }

    /// 清除 qubit 的依赖关系（执行 uncompute 后调用）
    operation ClearDependencies(qubitId: Int, pool: QubitPoolManager) : QubitPoolManager {
        mutable updated = pool;

        for i in 0..Length(pool::qubitRecords) - 1 {
            if pool::qubitRecords[i]::id == qubitId {
                let newDep = QubitDependency([], [], [], true);
                let updatedRecord = QubitRecord(
                    pool::qubitRecords[i]::id,
                    pool::qubitRecords[i]::state,
                    pool::qubitRecords[i]::operationCount,
                    pool::qubitRecords[i]::lastAccessTime,
                    pool::qubitRecords[i]::parityBuffer,
                    newDep
                );
                set updated = QubitPoolManager(
                    pool::totalQubits,
                    pool::freeCount,
                    pool::reservedQubits,
                    [
                        if j == i then updatedRecord else pool::qubitRecords[j]
                        | j in 0..Length(pool::qubitRecords) - 1
                    ]
                );
            }
        }

        return updated;
    }

    /// 获取 qubit 的依赖信息
    operation GetQubitDependency(qubitId: Int, pool: QubitPoolManager) : QubitDependency? {
        for record in pool::qubitRecords {
            if record::id == qubitId {
                return record::dependency;
            }
        }
        return null;
    }
}
