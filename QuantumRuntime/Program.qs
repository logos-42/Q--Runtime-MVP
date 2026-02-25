/// 主程序：量子 - 经典混合系统演示
namespace QuantumRuntime {

    open QuantumRuntime.QubitPool;
    open QuantumRuntime.CircuitIR;
    open QuantumRuntime.TaskQueue;
    open QuantumRuntime.Scheduler;

    @EntryPoint()
    operation Main() : Unit {
        Message("=== Quantum Runtime Demo ===");

        // 初始化
        let scheduler = InitializeScheduler(8);
        Message("Initialized scheduler with 8 qubits");

        // 创建电路
        let circuit1 = CreateCircuitBlock("Bell-State");
        let instr1 = CreateInstruction(1, 0, [0], 0);
        let circuit1_final = AddInstructionToBlock(circuit1, instr1);

        Message("Created Bell-State circuit");
        PrintCircuitInfo(circuit1_final);

        // 创建并提交任务
        let task = CreateTask(1, "Test-Task", circuit1_final, TaskPriority.Normal);
        let scheduler2 = SubmitTask(scheduler, task);

        Message("Submitted task");

        // 选择并调度任务
        let nextIdx = SelectNextTask(scheduler2);
        Message($"Next task index: {nextIdx}");

        if nextIdx >= 0 {
            let (scheduledTask, scheduler3) = ScheduleTask(scheduler2, nextIdx);
            Message($"Scheduled task: {scheduledTask::name}");

            let scheduler4 = ExecuteTask(scheduler3, nextIdx);
            Message("Task completed");

            // 打印状态
            PrintSchedulerStatus(scheduler4);
        }

        Message("=== Demo Complete ===");
    }
}
