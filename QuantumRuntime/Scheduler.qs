namespace QuantumRuntime.Scheduler {

    open QuantumRuntime.QubitPool;
    open QuantumRuntime.CircuitIR;
    open QuantumRuntime.TaskQueue;

    newtype Policy = Int;
    newtype Config = (Policy, Int, Bool);
    newtype Scheduler = (Config, TaskQueueManager, QubitPoolManager, Task[], Task[], Int);

    operation Init(numQubits: Int) : Scheduler {
        let pool = InitializeQubitPool(numQubits);
        let queue = Initialize();
        let config = (1, numQubits, false);
        return (config, queue, pool, [], [], 0);
    }

    operation Submit(sched: Scheduler, task: Task) : Scheduler {
        let (config, queue, pool, scheduled, completed, ts) = sched;
        let newQueue = Enqueue(queue, task);
        return (config, newQueue, pool, scheduled, completed, ts);
    }

    operation GetStats(sched: Scheduler) : (Int, Int, Int, Int) {
        let (config, queue, pool, scheduled, completed, ts) = sched;
        let (total, p, r, c, f) = GetStats(queue);
        return (p + r, Length(scheduled), Length(completed), ts);
    }

    operation GetUsage(sched: Scheduler) : (Int, Int, Int, Double) {
        let (config, queue, pool, scheduled, completed, ts) = sched;
        let (total, free, reserved) = GetPoolStats(pool);
        let used = total - free;
        let rate = IntAsDouble(used) / IntAsDouble(total);
        return (total, free, used, rate);
    }
}
