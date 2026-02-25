namespace QuantumRuntime {

    open QuantumRuntime.QubitPool;
    open QuantumRuntime.CircuitIR;
    open QuantumRuntime.TaskQueue;
    open QuantumRuntime.Scheduler;

    @EntryPoint()
    operation Main() : Unit {
        Message("=== Quantum Runtime Demo ===");

        let sched = Init(8);
        Message("Initialized with 8 qubits");

        let circuit = CreateCircuitBlock("Bell");
        let instr = CreateInstruction(1, 0, [0], 0);

        Message("Created circuit");

        let task = CreateTask(1, "Test", circuit, 1, 0);
        let sched2 = Submit(sched, task);

        let (pending, scheduled, completed, ts) = GetStats(sched2);
        Message($"Tasks: {pending} pending, {scheduled} scheduled, {completed} completed");

        let (total, free, used, rate) = GetUsage(sched2);
        Message($"Qubits: {total} total, {free} free, {used} used");

        Message("=== Demo Complete ===");
    }
}
