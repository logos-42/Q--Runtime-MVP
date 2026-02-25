/// 模块：调度器
namespace QuantumRuntime.Scheduler {

    open Microsoft.Quantum.Intrinsic;
    open Microsoft.Quantum.Convert;
    open QuantumRuntime.QubitPool;
    open QuantumRuntime.CircuitIR;
    open QuantumRuntime.TaskQueue;

    // 调度策略常量
    function SchedulingPolicy_FIFO() : Int { return 0; }
    function SchedulingPolicy_Priority() : Int { return 1; }

    /// 调度器配置
    newtype SchedulerConfig = (policy: Int, maxConcurrent: Int);

    /// 调度器
    newtype Scheduler = (
        config: SchedulerConfig,
        taskQueue: TaskQueueManager,
        qubitPool: QubitPoolManager,
        globalTimestamp: Int
    );

    /// 初始化调度器
    operation InitializeScheduler(numQubits : Int) : Scheduler {
        let pool = InitializeQubitPool(numQubits);
        let queue = InitializeTaskQueue();
        let config = SchedulerConfig(SchedulingPolicy_Priority(), numQubits);
        return Scheduler(config, queue, pool, 0);
    }

    /// 提交任务
    operation SubmitTask(scheduler : Scheduler, task : Task) : Scheduler {
        let updatedQueue = Enqueue(scheduler::taskQueue, task);
        return Scheduler(scheduler::config, updatedQueue, scheduler::qubitPool, scheduler::globalTimestamp);
    }

    /// 选择下一个任务
    operation SelectNextTask(scheduler : Scheduler) : Int {
        let queue = scheduler::taskQueue::queue;
        if Length(queue) == 0 {
            return -1;
        }

        let policy = scheduler::config::policy;
        if policy == SchedulingPolicy_FIFO() {
            return 0;
        }

        // Priority 策略
        mutable bestIdx = 0;
        mutable bestPriority = -1;

        for i in 0..Length(queue) - 1 {
            let p = GetPriorityValue(queue[i]::priority);
            if p > bestPriority {
                set bestPriority = p;
                set bestIdx = i;
            }
        }
        return bestIdx;
    }

    /// 调度任务
    operation ScheduleTask(scheduler : Scheduler, taskIndex : Int) : (Task, Scheduler) {
        let queue = scheduler::taskQueue::queue;
        if taskIndex < 0 or taskIndex >= Length(queue) {
            fail "Invalid task index";
        }

        let task = queue[taskIndex];
        let requiredQubits = task::circuit::totalCost::qubitCount;

        // 分配 qubit
        mutable pool = scheduler::qubitPool;
        mutable allocated = [];

        for _ in 0..requiredQubits - 1 {
            let (qid, newPool) = AllocateQubit(pool);
            set pool = newPool;
            set allocated = allocated + [qid];
        }

        // 更新任务状态
        let scheduledTask = Task(
            task::id,
            task::name,
            task::circuit,
            task::priority,
            TaskState_Scheduled(),
            allocated
        );

        // 更新队列
        mutable newQueue = [];
        for i in 0..Length(queue) - 1 {
            if i == taskIndex {
                set newQueue = newQueue + [scheduledTask];
            } else {
                set newQueue = newQueue + [queue[i]];
            }
        }
        let newQueueManager = TaskQueueManager(newQueue, scheduler::taskQueue::nextTaskId);

        let updatedScheduler = Scheduler(
            scheduler::config,
            newQueueManager,
            pool,
            scheduler::globalTimestamp + 1
        );

        return (scheduledTask, updatedScheduler);
    }

    /// 执行任务
    operation ExecuteTask(scheduler : Scheduler, taskIndex : Int) : Scheduler {
        let queue = scheduler::taskQueue::queue;
        if taskIndex < 0 or taskIndex >= Length(queue) {
            fail "Invalid task index";
        }

        let task = queue[taskIndex];

        // 释放 qubit
        mutable pool = scheduler::qubitPool;
        for qid in task::allocatedQubits {
            set pool = ReleaseQubit(qid, pool);
        }

        // 更新任务状态为完成
        let completedTask = Task(
            task::id,
            task::name,
            task::circuit,
            task::priority,
            TaskState_Completed(),
            task::allocatedQubits
        );

        mutable newQueue = [];
        for i in 0..Length(queue) - 1 {
            if i == taskIndex {
                set newQueue = newQueue + [completedTask];
            } else {
                set newQueue = newQueue + [queue[i]];
            }
        }
        let newQueueManager = TaskQueueManager(newQueue, scheduler::taskQueue::nextTaskId);

        return Scheduler(
            scheduler::config,
            newQueueManager,
            pool,
            scheduler::globalTimestamp + 1
        );
    }

    /// 获取调度器统计
    operation GetStats(scheduler : Scheduler) : (Int, Int, Int, Int) {
        let (queueLen, nextId) = QuantumRuntime.TaskQueue.GetStats(scheduler::taskQueue);
        let (total, free, records) = GetPoolStats(scheduler::qubitPool);
        return (queueLen, total, free, scheduler::globalTimestamp);
    }

    /// 获取资源使用
    operation GetUsage(scheduler : Scheduler) : (Int, Int, Int, Double) {
        let (total, free, _) = GetPoolStats(scheduler::qubitPool);
        let used = total - free;
        let rate = IntAsDouble(used) / IntAsDouble(total);
        return (total, free, used, rate);
    }

    /// 打印调度器状态
    operation PrintSchedulerStatus(scheduler : Scheduler) : Unit {
        Message("=== Scheduler Status ===");
        
        let policy = scheduler::config::policy;
        if policy == SchedulingPolicy_FIFO() {
            Message("Policy: FIFO");
        } else {
            Message("Policy: Priority");
        }
        
        PrintQueueStatus(scheduler::taskQueue);
        
        let (total, free, used, rate) = GetUsage(scheduler);
        Message($"Qubits: {total} total, {free} free, {used} used ({rate * 100.0}%)");
        Message("========================");
    }
}
