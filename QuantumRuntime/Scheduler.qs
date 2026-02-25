namespace QuantumRuntime.Scheduler {

    open QuantumRuntime.QubitPool;
    open QuantumRuntime.CircuitIR;
    open QuantumRuntime.TaskQueue;

    newtype Policy = Int;

    struct Config {
        policy: Policy;
        maxQubits: Int;
        verbose: Bool;
    }

    struct Scheduler {
        config: Config;
        queue: TaskQueueManager;
        pool: QubitPoolManager;
        scheduled: Task[];
        completed: Task[];
        timestamp: Int;
    }

    operation Init(numQubits: Int) : Scheduler {
        let pool = InitializeQubitPool(numQubits);
        let queue = Initialize();
        let config = Config(policy = 1, maxQubits = numQubits, verbose = false);
        return Scheduler(
            config = config,
            queue = queue,
            pool = pool,
            scheduled = [],
            completed = [],
            timestamp = 0
        );
    }

    operation Submit(sched: Scheduler, task: Task) : Scheduler {
        let (config, queue, pool, scheduled, completed, ts) = sched;
        let newQueue = Enqueue(queue, task);
        return Scheduler(
            config = config,
            queue = newQueue,
            pool = pool,
            scheduled = scheduled,
            completed = completed,
            timestamp = ts
        );
    }

    operation GetStats(sched: Scheduler) : (Int, Int, Int, Int) {
        let (config, queue, pool, scheduled, completed, ts) = sched;
        let (total, p, r, c, f) = GetStats(queue);
        return (p + r, Length(scheduled), Length(completed), ts);
    }

    operation GetUsage(sched: Scheduler) : (Int, Int, Int, Double) {
        let (config, queue, pool, scheduled, completed, ts) = sched;
        let (total, free, reserved) = GetStats(pool);
        let used = total - free;
        let rate = IntAsDouble(used) / IntAsDouble(total);
        return (total, free, used, rate);
    }
}
