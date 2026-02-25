/// 主程序：量子经典兼容系统演示（增强版）
///
/// 演示三项改进：
/// 1. WithTempQubit 高级抽象
/// 2. GenerateInverseCircuit 自动逆电路
/// 3. 资源依赖追踪

namespace QuantumRuntime {

    open Microsoft.Quantum.Intrinsic;
    open Microsoft.Quantum.Canon;
    open Microsoft.Quantum.Diagnostics;
    open QuantumRuntime.QubitPool;
    open QuantumRuntime.CircuitIR;
    open QuantumRuntime.TaskQueue;
    open QuantumRuntime.Scheduler;

    @EntryPoint()
    operation Main() : Unit {
        Message("=== 量子经典兼容系统演示 ===\n");

        // 演示改进 1：WithTempQubit 高级抽象
        Message("--- 改进 1: WithTempQubit 高级抽象 ---");
        DemonstrateWithTempQubit();
        Message("");

        // 演示改进 2：GenerateInverseCircuit 自动逆电路
        Message("--- 改进 2: GenerateInverseCircuit 自动逆电路 ---");
        DemonstrateGenerateInverseCircuit();
        Message("");

        // 演示改进 3：资源依赖追踪
        Message("--- 改进 3: 资源依赖追踪 ---");
        DemonstrateDependencyTracking();
        Message("");

        // 完整系统演示
        Message("--- 完整系统演示 ---");
        DemonstrateFullSystem();
        Message("");

        Message("=== 演示完成 ===");
    }

    // ============================================
    // 改进 1 演示：WithTempQubit 高级抽象
    // ============================================

    operation DemonstrateWithTempQubit() : Unit {
        // 使用 WithTempQubit 简化临时 qubit 管理
        let result = WithTempQubit(2, qs -> {
            // 准备 Bell 态
            H(qs[0]);
            CNOT(qs[0], qs[1]);

            // 测量
            let m1 = M(qs[0]);
            let m2 = M(qs[1]);

            // qs 自动清理，无需手动 ResetAll
            return (m1, m2);
        });

        Message($"Bell 态测量结果：{result}");
        Message("临时 qubit 已自动清理");

        // 使用 WithSingleTempQubit
        let singleResult = WithSingleTempQubit(q -> {
            H(q);
            let m = M(q);
            // q 自动清理
            return m;
        });

        Message($"单 qubit 叠加态测量：{singleResult}");

        // 使用带初始化的版本
        let initResult = WithInitializedTempQubit(1, qs -> {
            // 初始化：制备 |+⟩ 态
            H(qs[0]);
        }, qs -> {
            // 使用已初始化的 qubit
            let m = M(qs[0]);
            return m;
        });

        Message($"初始化后测量：{initResult}");
    }

    // ============================================
    // 改进 2 演示：GenerateInverseCircuit 自动逆电路
    // ============================================

    operation DemonstrateGenerateInverseCircuit() : Unit {
        // 创建电路：H + CNOT（Bell 态制备）
        let bellCircuit = CreateCircuitBlock("Bell-State");
        
        // 添加 H 门
        let hInstr = CreateSingleQubitInstruction(1, GateType::H, 0, []);
        let circuit1 = AddInstructionToBlock(bellCircuit, hInstr);

        // 添加 CNOT 门
        let cnotInstr = CreateTwoQubitInstruction(2, GateType::CNOT, 0, 1);
        let circuit2 = AddInstructionToBlock(circuit1, cnotInstr);

        Message($"原电路：{circuit2::name}");
        Message($"可逆：{circuit2::isReversible}");
        Message($"资源成本：{GetResourceReport(circuit2)}");

        // 生成逆电路
        let inverseCircuit = GenerateInverseCircuit(circuit2);
        Message($"\n逆电路：{inverseCircuit::name}");
        Message($"逆电路资源：{GetResourceReport(inverseCircuit)}");

        // 演示共轭操作：U† V U
        Message("\n--- 共轭操作演示 ---");
        
        // 准备电路 U（H 门）
        let prepareU = CreateCircuitBlock("Prepare-H");
        let hInstr2 = CreateSingleQubitInstruction(1, GateType::H, 0, []);
        let prepareCircuit = AddInstructionToBlock(prepareU, hInstr2);

        // 目标电路 V（Z 门）
        let targetV = CreateCircuitBlock("Target-Z");
        let zInstr = CreateSingleQubitInstruction(2, GateType::Z, 0, []);
        let targetCircuit = AddInstructionToBlock(targetV, zInstr);

        // 共轭：U† V U
        let conjugateResult = ConjugateCircuit(prepareCircuit, targetCircuit);
        Message($"共轭电路：{conjugateResult::name}");
        Message($"总指令数：{conjugateResult::totalCost::gateCount}");

        // 演示旋转门的逆
        Message("\n--- 旋转门逆操作 ---");
        let rxInstr = CreateRotationInstruction(1, GateType::Rx, 0, 1.57);
        Message($"Rx(1.57) 的逆：Rx({rxInstr::inverseParameters!![0]})");
    }

    // ============================================
    // 改进 3 演示：资源依赖追踪
    // ============================================

    operation DemonstrateDependencyTracking() : Unit {
        // 初始化资源池
        let pool = InitializeQubitPool(5);
        Message($"初始化资源池：5 qubits");

        // 分配 qubit
        let (q1, pool1) = AllocateQubit(pool);
        let (q2, pool2) = AllocateQubit(pool1);
        Message($"分配 qubit: {q1}, {q2}");

        // 记录纠缠关系
        let pool3 = RecordEntanglement(q1, q2, pool2);
        Message($"记录 qubit {q1} 和 {q2} 之间的纠缠");

        // 检查依赖
        let dep1 = GetQubitDependency(q1, pool3);
        if dep1 != null {
            let d = dep1!!;
            Message($"Qubit {q1} 依赖信息:");
            Message($"  纠缠：{d::entangledWith}");
            Message($"  可安全 uncompute: {d::canSafeUncompute}");
        }

        // 检查是否可安全 uncompute
        let canUncompute = CanSafeUncompute(q1, pool3);
        Message($"\nQubit {q1} 可安全 uncompute: {canUncompute}");

        // 释放 qubit 并清除依赖
        let pool4 = ClearDependencies(q1, pool3);
        let pool5 = ReleaseQubit(q1, pool4);
        Message($"\n释放 qubit {q1} 并清除依赖");

        // 验证已清除
        let canUncomputeAfter = CanSafeUncompute(q1, pool5);
        Message($"Qubit {q1} 释放后可安全 uncompute: {canUncomputeAfter}");
    }

    // ============================================
    // 完整系统演示
    // ============================================

    operation DemonstrateFullSystem() : Unit {
        // 初始化调度器
        let scheduler = InitializeScheduler(10);
        Message($"初始化调度器：10 qubits");

        // 创建电路
        let circuit1 = CreateCircuitBlock("Test-Circuit-1");
        let hInstr = CreateSingleQubitInstruction(1, GateType::H, 0, []);
        let circuit1WithH = AddInstructionToBlock(circuit1, hInstr);

        // 提交任务
        let (taskId1, scheduler1) = CreateAndSubmitTask(
            scheduler,
            "Bell-Preparation",
            circuit1WithH,
            TaskPriority::High
        );
        Message($"提交任务 ID: {taskId1}");

        // 再提交一个任务
        let circuit2 = CreateCircuitBlock("Test-Circuit-2");
        let xInstr = CreateSingleQubitInstruction(2, GateType::X, 1, []);
        let circuit2WithX = AddInstructionToBlock(circuit2, xInstr);

        let (taskId2, scheduler2) = CreateAndSubmitTask(
            scheduler1,
            "X-Gate-Operation",
            circuit2WithX,
            TaskPriority::Normal
        );
        Message($"提交任务 ID: {taskId2}");

        // 调度并执行
        let (task1, scheduler3) = ScheduleAndExecuteNext(scheduler2);
        if task1 != null {
            Message($"执行任务：{task1!!::name}");
            
            // 完成任务
            let scheduler4 = CompleteTask(scheduler3, task1!!::id, true);
            Message($"任务 {task1!!::id} 完成");
        }

        // 获取统计
        Message($"\n{GetSchedulerSummary(scheduler3)}");

        // 演示资源依赖追踪
        Message("\n--- 任务依赖演示 ---");
        let queue = scheduler3::taskQueue;
        
        // 设置任务依赖
        let queueWithDep = SetTaskDependency(queue, taskId2, [taskId1]);
        Message($"设置任务 {taskId2} 依赖于任务 {taskId1}");

        // 检查是否可以执行
        let canExec = CanExecuteTask(queueWithDep, taskId2);
        Message($"任务 {taskId2} 可以执行：{canExec}");

        // 演示冲突检测
        let task1Opt = null;
        for t in queue::queue {
            if t::id == taskId1 {
                set task1Opt = t;
            }
        }
        
        if task1Opt != null {
            let task2Opt = null;
            for t in queue::queue {
                if t::id == taskId2 {
                    set task2Opt = t;
                }
            }
            
            if task2Opt != null {
                let hasConflict = CheckQubitConflict(task1Opt!!, task2Opt!!);
                Message($"任务 {taskId1} 和 {taskId2} 有 qubit 冲突：{hasConflict}");
            }
        }
    }
}
