/// 主程序：量子 - 经典混合系统演示
///
/// 演示流程：
/// 1. 初始化 qubit 资源池和任务队列
/// 2. 创建多个量子电路任务
/// 3. 提交任务到调度器
/// 4. 调度器按优先级执行任务
/// 5. 输出资源使用统计和任务执行结果

namespace QuantumRuntime {

    open QuantumRuntime.QubitPool;
    open QuantumRuntime.CircuitIR;
    open QuantumRuntime.TaskQueue;
    open QuantumRuntime.Scheduler;

    /// 程序入口点
    @EntryPoint()
    operation Main() : Unit {
        Message("╔══════════════════════════════════════════════════════════╗");
        Message("║     Quantum-Classical Hybrid System Demo                 ║");
        Message("║     量子 - 经典混合系统演示                               ║");
        Message("╚══════════════════════════════════════════════════════════╝");
        Message("");

        // ===========================================
        // 第一阶段：系统初始化
        // ===========================================
        Message("【Phase 1】System Initialization");
        Message("─────────────────────────────────");
        
        let numQubits = 8;
        Message($"Initializing scheduler with {numQubits} qubits...");
        
        let scheduler = InitializeSchedulerWithConfig(
            numQubits,
            SchedulingPolicy.Priority,
            4  // 最大并发 4 个任务
        );
        
        Message("Scheduler initialized with Priority scheduling policy");
        Message("");

        // ===========================================
        // 第二阶段：创建量子电路
        // ===========================================
        Message("【Phase 2】Creating Quantum Circuits");
        Message("────────────────────────────────────");

        // --- 电路 1：Bell 态制备电路 ---
        Message("Creating Circuit 1: Bell State Preparation");
        let circuit1 = CreateCircuitBlock("Bell-State");
        let instr1_1 = CreateInstruction(1, GateType.SingleQubit, [0], 0);   // H 门
        let instr1_2 = CreateInstruction(2, GateType.TwoQubit, [0, 1], 1);   // CNOT
        let circuit1_v2 = AddInstructionToBlock(circuit1, instr1_1);
        let circuit1_final = AddInstructionToBlock(circuit1_v2, instr1_2);
        PrintCircuitInfo(circuit1_final);
        Message("");

        // --- 电路 2：量子傅里叶变换（简化版）---
        Message("Creating Circuit 2: Simplified QFT");
        let circuit2 = CreateCircuitBlock("QFT-3qubit");
        let instr2_1 = CreateInstruction(3, GateType.SingleQubit, [0], 0);   // H
        let instr2_2 = CreateInstruction(4, GateType.SingleQubit, [1], 0);   // H
        let instr2_3 = CreateInstruction(5, GateType.SingleQubit, [2], 0);   // H
        let instr2_4 = CreateInstruction(6, GateType.TwoQubit, [0, 1], 2);   // Controlled-Phase
        let instr2_5 = CreateInstruction(7, GateType.TwoQubit, [1, 2], 2);   // Controlled-Phase
        let circuit2_v2 = AddInstructionToBlock(circuit2, instr2_1);
        let circuit2_v3 = AddInstructionToBlock(circuit2_v2, instr2_2);
        let circuit2_v4 = AddInstructionToBlock(circuit2_v3, instr2_3);
        let circuit2_v5 = AddInstructionToBlock(circuit2_v4, instr2_4);
        let circuit2_final = AddInstructionToBlock(circuit2_v5, instr2_5);
        PrintCircuitInfo(circuit2_final);
        Message("");

        // --- 电路 3：Grover 迭代（简化版）---
        Message("Creating Circuit 3: Grover Iteration");
        let circuit3 = CreateCircuitBlock("Grover-Oracle");
        let instr3_1 = CreateInstruction(8, GateType.SingleQubit, [0], 1);   // T 门（Oracle）
        let instr3_2 = CreateInstruction(9, GateType.SingleQubit, [1], 1);   // T 门
        let instr3_3 = CreateInstruction(10, GateType.TwoQubit, [0, 1], 3);  // 扩散操作
        let instr3_4 = CreateInstruction(11, GateType.Measurement, [0], 0);  // 测量
        let circuit3_v2 = AddInstructionToBlock(circuit3, instr3_1);
        let circuit3_v3 = AddInstructionToBlock(circuit3_v2, instr3_2);
        let circuit3_v4 = AddInstructionToBlock(circuit3_v3, instr3_3);
        let circuit3_final = AddInstructionToBlock(circuit3_v4, instr3_4);
        PrintCircuitInfo(circuit3_final);
        Message("");

        // --- 电路 4：嵌套电路（复杂任务）---
        Message("Creating Circuit 4: Nested Circuit");
        let circuit4 = CreateCircuitBlock("Nested-Complex");
        let nested4_1 = NestCircuitBlock(circuit4, circuit1_final);
        let nested4_2 = NestCircuitBlock(nested4_1, circuit2_final);
        let circuit4_final = NestCircuitBlock(nested4_2, circuit3_final);
        PrintCircuitInfo(circuit4_final);
        Message("");

        // ===========================================
        // 第三阶段：创建并提交任务
        // ===========================================
        Message("【Phase 3】Creating and Submitting Tasks");
        Message("───────────────────────────────────────");

        // 创建不同优先级的任务
        Message("Creating tasks with different priorities:");
        
        let (taskId1, scheduler1) = CreateAndSubmitTask(
            scheduler,
            "Bell-State-Task",
            circuit1_final,
            TaskPriority.Normal
        );
        Message($"  ✓ Task #{taskId1}: Bell-State-Task (Normal priority)");

        let (taskId2, scheduler2) = CreateAndSubmitTask(
            scheduler1,
            "QFT-Task",
            circuit2_final,
            TaskPriority.High
        );
        Message($"  ✓ Task #{taskId2}: QFT-Task (High priority)");

        let (taskId3, scheduler3) = CreateAndSubmitTask(
            scheduler2,
            "Grover-Task",
            circuit3_final,
            TaskPriority.Critical
        );
        Message($"  ✓ Task #{taskId3}: Grover-Task (Critical priority)");

        let (taskId4, scheduler4) = CreateAndSubmitTask(
            scheduler3,
            "Nested-Complex-Task",
            circuit4_final,
            TaskPriority.Low
        );
        Message($"  ✓ Task #{taskId4}: Nested-Complex-Task (Low priority)");

        Message("");
        Message("Current Queue Status:");
        PrintQueueStatus(scheduler4::taskQueue);
        Message("");

        // ===========================================
        // 第四阶段：任务调度与执行
        // ===========================================
        Message("【Phase 4】Task Scheduling and Execution");
        Message("────────────────────────────────────────");

        // 执行第一个任务（应该是 Critical 优先级的 Grover-Task）
        Message(">>> Scheduling next task (Priority-based)...");
        let nextIndex = SelectNextTask(scheduler4);
        if nextIndex >= 0 {
            let nextTask = scheduler4::taskQueue::queue[nextIndex];
            Message($"Selected: {nextTask::name} (Priority: {nextTask::priority})");
        }
        Message("");

        // 调度并执行所有任务
        mutable currentScheduler = scheduler4;
        mutable executionCount = 0;

        repeat {
            let nextIdx = SelectNextTask(currentScheduler);
            if nextIdx == -1 {
                Message("No more pending tasks.");
                break;
            }

            let taskToRun = currentScheduler::taskQueue::queue[nextIdx];
            Message($"--- Executing Task #{executionCount + 1}: {taskToRun::name} ---");
            Message($"    Priority: {taskToRun::priority}");
            Message($"    Required Qubits: {taskToRun::circuit::totalCost::qubitCount}");
            Message($"    Estimated Duration: {taskToRun::estimatedDuration}");

            // 调度任务
            let (scheduledTask, afterSchedule) = ScheduleTask(currentScheduler, nextIdx);
            Message($"    Allocated Qubits: {scheduledTask::allocatedQubits}");

            // 执行任务
            let afterExecute = ExecuteTask(afterSchedule, nextIdx);
            Message($"    Status: Completed");
            Message("");

            set currentScheduler = afterExecute;
            set executionCount = executionCount + 1;

        } until (executionCount >= 4)

        // ===========================================
        // 第五阶段：资源统计与总结
        // ===========================================
        Message("【Phase 5】Resource Statistics and Summary");
        Message("──────────────────────────────────────────");

        PrintSchedulerStatus(currentScheduler);
        Message("");

        // 详细统计
        let (total, pending, running, completed, failed) = GetQueueStats(currentScheduler::taskQueue);
        let (poolTotal, poolFree, poolUsed, usageRate) = GetResourceUsage(currentScheduler);

        Message("═══════════════════════════════════════════════════════════");
        Message("                    EXECUTION SUMMARY                       ");
        Message("═══════════════════════════════════════════════════════════");
        Message($"Tasks Created:     {total + completed}");
        Message($"Tasks Completed:   {completed}");
        Message($"Tasks Pending:     {pending}");
        Message($"Tasks Failed:      {failed}");
        Message("");
        Message($"Qubit Pool Size:   {poolTotal}");
        Message($"Qubits Free:       {poolFree}");
        Message($"Qubits In Use:     {poolUsed}");
        Message($"Peak Usage Rate:   {usageRate * 100.0}%");
        Message("");
        Message($"Total Exec Time:   {currentScheduler::globalTimestamp} units");
        Message("═══════════════════════════════════════════════════════════");
        Message("");

        // 已完成任务详情
        if Length(currentScheduler::completedTasks) > 0 {
            Message("Completed Tasks Detail:");
            for task in currentScheduler::completedTasks {
                let priorityStr = 
                    task::priority == TaskPriority.Critical ? "Critical" |
                    task::priority == TaskPriority.High ? "High" |
                    task::priority == TaskPriority.Normal ? "Normal" |
                    "Low";
                Message($"  • {task::name}");
                Message($"      Priority: {priorityStr}");
                Message($"      Duration: {task::actualDuration}");
                Message($"      Qubits Used: {Length(task::allocatedQubits)}");
            }
        }

        Message("");
        Message("╔══════════════════════════════════════════════════════════╗");
        Message("║              Demo Complete!                              ║");
        Message("╚══════════════════════════════════════════════════════════╝");
    }
}
