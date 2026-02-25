/// 模块：调度器
///
/// 核心设计：
/// - 集成任务队列管理
/// - 实现 qubit 资源分配策略
/// - 优化电路执行顺序，最小化资源冲突
/// - 跟踪全局资源状态和任务执行历史

namespace QuantumRuntime.Scheduler {

    open QuantumRuntime.QubitPool;
    open QuantumRuntime.CircuitIR;
    open QuantumRuntime.TaskQueue;

    /// 调度策略枚举
    /// 决定任务选择和资源分配的方式
    enum SchedulingPolicy {
        FIFO,           // 先进先出，简单公平
        Priority,       // 优先级优先，关键任务先执行
        ResourceAware   // 资源感知，优先调度资源充足的任务
    }

    /// 调度器配置
    newtype SchedulerConfig = (
        policy: SchedulingPolicy,   // 调度策略
        maxConcurrentTasks: Int,    // 最大并发任务数
        enablePreemption: Bool      // 是否允许抢占
    );

    /// 调度器主状态
    /// 整合任务队列、qubit 池和调度配置
    newtype Scheduler = (
        config: SchedulerConfig,        // 调度器配置
        taskQueue: TaskQueueManager,    // 任务队列管理器
        qubitPool: QubitPoolManager,    // qubit 资源池
        scheduledTasks: Task[],         // 已调度但未完成的任务
        completedTasks: Task[],         // 已完成的任务历史
        globalTimestamp: Int            // 全局时间戳
    );

    /// 初始化调度器
    /// 
    /// # Parameters
    /// - `numQubits`: qubit 池大小
    /// 
    /// # Returns
    /// 新初始化的调度器
    operation InitializeScheduler(numQubits: Int) : Scheduler {
        let pool = InitializeQubitPool(numQubits);
        let queue = InitializeTaskQueue();
        let config = SchedulerConfig(
            SchedulingPolicy.Priority,
            numQubits,  // 简化：最多并发任务数等于 qubit 数
            false
        );

        return Scheduler(
            config,
            queue,
            pool,
            [],
            [],
            0
        );
    }

    /// 使用自定义配置初始化调度器
    /// 
    /// # Parameters
    /// - `numQubits`: qubit 池大小
    /// - `policy`: 调度策略
    /// - `maxConcurrent`: 最大并发任务数
    /// 
    /// # Returns
    /// 新初始化的调度器
    operation InitializeSchedulerWithConfig(
        numQubits: Int,
        policy: SchedulingPolicy,
        maxConcurrent: Int
    ) : Scheduler {
        let pool = InitializeQubitPool(numQubits);
        let queue = InitializeTaskQueue();
        let config = SchedulerConfig(policy, maxConcurrent, false);

        return Scheduler(
            config,
            queue,
            pool,
            [],
            [],
            0
        );
    }

    /// 创建并添加任务到调度器
    /// 便捷操作：创建任务并提交到队列
    /// 
    /// # Parameters
    /// - `scheduler`: 当前调度器
    /// - `name`: 任务名称
    /// - `circuit`: 量子电路
    /// - `priority`: 任务优先级
    /// 
    /// # Returns
    /// 更新后的调度器和新创建的任务 ID
    operation CreateAndSubmitTask(
        scheduler: Scheduler,
        name: String,
        circuit: CircuitBlock,
        priority: TaskPriority
    ) : (Int, Scheduler) {
        let taskId = scheduler::taskQueue::nextTaskId;
        let task = CreateTask(
            taskId,
            name,
            circuit,
            priority,
            scheduler::globalTimestamp
        );

        let updatedQueue = Enqueue(scheduler::taskQueue, task);
        
        let updatedScheduler = Scheduler(
            scheduler::config,
            updatedQueue,
            scheduler::qubitPool,
            scheduler::scheduledTasks,
            scheduler::completedTasks,
            scheduler::globalTimestamp
        );

        return (taskId, updatedScheduler);
    }

    /// 将已有任务提交到调度器队列
    /// 
    /// # Parameters
    /// - `scheduler`: 当前调度器
    /// - `task`: 要提交的任务
    /// 
    /// # Returns
    /// 更新后的调度器
    operation SubmitTask(scheduler: Scheduler, task: Task) : Scheduler {
        let updatedQueue = Enqueue(scheduler::taskQueue, task);
        
        return Scheduler(
            scheduler::config,
            updatedQueue,
            scheduler::qubitPool,
            scheduler::scheduledTasks,
            scheduler::completedTasks,
            scheduler::globalTimestamp
        );
    }

    /// 资源冲突检测
    /// 检查给定的 qubit 集合是否与已调度的任务冲突
    /// 
    /// # Parameters
    /// - `scheduler`: 当前调度器
    /// - `requestedQubits`: 请求的 qubit ID 列表
    /// 
    /// # Returns
    /// 是否存在冲突
    operation CheckResourceConflict(scheduler: Scheduler, requestedQubits: Int[]) : Bool {
        for scheduledTask in scheduler::scheduledTasks {
            if scheduledTask::state == TaskState.Scheduled or scheduledTask::state == TaskState.Running {
                for allocatedQubit in scheduledTask::allocatedQubits {
                    for requestedQubit in requestedQubits {
                        if allocatedQubit == requestedQubit {
                            return true;  // 发现冲突
                        }
                    }
                }
            }
        }
        return false;  // 无冲突
    }

    /// 根据调度策略选择下一个要执行的任务
    /// 
    /// # Parameters
    /// - `scheduler`: 当前调度器
    /// 
    /// # Returns
    /// 下一个任务的索引，如果没有可用任务则返回 -1
    operation SelectNextTask(scheduler: Scheduler) : Int {
        let queue = scheduler::taskQueue;
        let queueArray = queue::queue;

        if Length(queueArray) == 0 {
            return -1;
        }

        let policy = scheduler::config::policy;

        // FIFO 策略：选择第一个 Pending 任务
        if policy == SchedulingPolicy.FIFO {
            for i in 0..Length(queueArray) - 1 {
                if queueArray[i]::state == TaskState.Pending {
                    return i;
                }
            }
            return -1;
        }

        // Priority 策略：选择优先级最高的 Pending 任务
        if policy == SchedulingPolicy.Priority {
            mutable bestIndex = -1;
            mutable bestPriority = -1;

            for i in 0..Length(queueArray) - 1 {
                let task = queueArray[i];
                if task::state == TaskState.Pending {
                    let priorityValue = GetPriorityValue(task::priority);
                    if priorityValue > bestPriority {
                        set bestPriority = priorityValue;
                        set bestIndex = i;
                    }
                }
            }
            return bestIndex;
        }

        // ResourceAware 策略：选择资源可满足的最高优先级任务
        if policy == SchedulingPolicy.ResourceAware {
            mutable bestIndex = -1;
            mutable bestPriority = -1;
            let (poolTotal, poolFree, poolReserved) = GetPoolStats(scheduler::qubitPool);

            for i in 0..Length(queueArray) - 1 {
                let task = queueArray[i];
                if task::state == TaskState.Pending {
                    let requiredQubits = task::circuit::totalCost::qubitCount;
                    let priorityValue = GetPriorityValue(task::priority);
                    
                    // 检查资源是否足够且无冲突
                    if requiredQubits <= poolFree {
                        if priorityValue > bestPriority {
                            set bestPriority = priorityValue;
                            set bestIndex = i;
                        }
                    }
                }
            }
            return bestIndex;
        }

        return -1;
    }

    /// 调度任务：分配资源并准备执行
    /// 
    /// # Parameters
    /// - `scheduler`: 当前调度器
    /// - `taskIndex`: 要调度的任务索引
    /// 
    /// # Returns
    /// (调度后的任务，更新后的调度器)
    operation ScheduleTask(scheduler: Scheduler, taskIndex: Int) : (Task, Scheduler) {
        let queue = scheduler::taskQueue;
        let queueArray = queue::queue;

        if taskIndex < 0 or taskIndex >= Length(queueArray) {
            fail $"Invalid task index: {taskIndex}";
        }

        let task = queueArray[taskIndex];

        if task::state != TaskState.Pending {
            fail $"Task is not in Pending state: {task::state}";
        }

        // 计算所需 qubit 数
        let requiredQubits = task::circuit::totalCost::qubitCount;

        // 检查资源是否充足
        let (poolTotal, poolFree, poolReserved) = GetPoolStats(scheduler::qubitPool);

        if poolFree < requiredQubits {
            fail $"Insufficient qubits: need {requiredQubits}, have {poolFree}";
        }

        // 分配 qubit
        mutable updatedPool = scheduler::qubitPool;
        mutable allocatedQubits = [];

        for _ in 0..requiredQubits - 1 {
            let (qubitId, newPool) = AllocateQubit(updatedPool);
            set updatedPool = newPool;
            set allocatedQubits = allocatedQubits + [qubitId];
        }

        // 更新任务状态为 Scheduled
        let scheduledTask = Task(
            task::id,
            task::name,
            task::circuit,
            task::priority,
            TaskState.Scheduled,
            allocatedQubits,
            task::estimatedDuration,
            task::actualDuration,
            task::createdAt,
            task::submittedAt
        );

        // 更新队列中的任务
        let updatedQueue = UpdateTaskState(scheduler::taskQueue, taskIndex, TaskState.Scheduled);
        let updatedQueueWithQubits = UpdateTaskQubits(updatedQueue, taskIndex, allocatedQubits);

        // 添加到已调度任务列表
        let newScheduledTasks = scheduler::scheduledTasks + [scheduledTask];

        let updatedScheduler = Scheduler(
            scheduler::config,
            updatedQueueWithQubits,
            updatedPool,
            newScheduledTasks,
            scheduler::completedTasks,
            scheduler::globalTimestamp + 1
        );

        return (scheduledTask, updatedScheduler);
    }

    /// 执行已调度的任务
    /// 模拟任务执行并释放资源
    /// 
    /// # Parameters
    /// - `scheduler`: 当前调度器
    /// - `taskIndex`: 要执行的任务索引
    /// 
    /// # Returns
    /// 更新后的调度器
    operation ExecuteTask(scheduler: Scheduler, taskIndex: Int) : Scheduler {
        let queue = scheduler::taskQueue;
        let queueArray = queue::queue;

        if taskIndex < 0 or taskIndex >= Length(queueArray) {
            fail $"Invalid task index: {taskIndex}";
        }

        let task = queueArray[taskIndex];

        if task::state != TaskState.Scheduled {
            fail $"Task is not scheduled, current state: {task::state}";
        }

        // 更新任务状态为 Running
        let runningQueue = UpdateTaskState(scheduler::taskQueue, taskIndex, TaskState.Running);
        
        let runningTask = runningQueue::queue[taskIndex];
        let actualDuration = runningTask::estimatedDuration;  // 简化：实际时间 = 预计时间

        // 更新为 Completed 状态
        let completedQueue = UpdateTaskState(runningQueue, taskIndex, TaskState.Completed);
        let completedTask = completedQueue::queue[taskIndex];

        // 释放分配的 qubit
        mutable updatedPool = scheduler::qubitPool;
        for qubitId in completedTask::allocatedQubits {
            set updatedPool = ReleaseQubit(qubitId, updatedPool);
        }

        // 从已调度任务列表中移除
        let newScheduledTasks = [
            t
            | t in scheduler::scheduledTasks
            if t::id != completedTask::id
        ];

        // 添加到已完成任务列表
        let newCompletedTasks = scheduler::completedTasks + [completedTask];

        let updatedScheduler = Scheduler(
            scheduler::config,
            completedQueue,
            updatedPool,
            newScheduledTasks,
            newCompletedTasks,
            scheduler::globalTimestamp + actualDuration
        );

        return updatedScheduler;
    }

    /// 调度并执行下一个任务
    /// 便捷操作：自动选择、调度和执行
    /// 
    /// # Parameters
    /// - `scheduler`: 当前调度器
    /// 
    /// # Returns
    /// (执行的任务，更新后的调度器)
    /// 如果没有可执行任务，返回 (null, scheduler)
    operation ScheduleAndExecuteNext(scheduler: Scheduler) : (Task?, Scheduler) {
        let nextIndex = SelectNextTask(scheduler);

        if nextIndex == -1 {
            return (null, scheduler);
        }

        let (scheduledTask, schedulerAfterSchedule) = ScheduleTask(scheduler, nextIndex);
        let schedulerAfterExecute = ExecuteTask(schedulerAfterSchedule, nextIndex);

        return (scheduledTask, schedulerAfterExecute);
    }

    /// 获取调度器统计信息
    /// 
    /// # Parameters
    /// - `scheduler`: 当前调度器
    /// 
    /// # Returns
    /// (待处理任务数，已调度任务数，已完成任务数，当前时间戳)
    operation GetSchedulerStats(scheduler: Scheduler) : (Int, Int, Int, Int) {
        let (total, pending, running, completed, failed) = GetQueueStats(scheduler::taskQueue);
        return (
            pending + running,  // 待处理（包括运行中）
            Length(scheduler::scheduledTasks),
            Length(scheduler::completedTasks),
            scheduler::globalTimestamp
        );
    }

    /// 获取资源使用统计
    /// 
    /// # Parameters
    /// - `scheduler`: 当前调度器
    /// 
    /// # Returns
    /// (总 qubit 数，空闲 qubit 数，已用 qubit 数，使用率)
    operation GetResourceUsage(scheduler: Scheduler) : (Int, Int, Int, Double) {
        let (poolTotal, poolFree, poolReserved) = GetPoolStats(scheduler::qubitPool);
        let used = poolTotal - poolFree;
        let usageRate = IntAsDouble(used) / IntAsDouble(poolTotal);
        return (poolTotal, poolFree, used, usageRate);
    }

    /// 打印调度器状态
    /// 
    /// # Parameters
    /// - `scheduler`: 当前调度器
    operation PrintSchedulerStatus(scheduler: Scheduler) : Unit {
        Message("=== Scheduler Status ===");
        
        // 打印配置
        let policyStr = 
            scheduler::config::policy == SchedulingPolicy.FIFO ? "FIFO" |
            scheduler::config::policy == SchedulingPolicy.Priority ? "Priority" |
            "ResourceAware";
        Message($"Policy: {policyStr}");
        Message($"Max Concurrent: {scheduler::config::maxConcurrentTasks}");
        Message("");

        // 打印队列状态
        PrintQueueStatus(scheduler::taskQueue);
        Message("");

        // 打印资源使用
        let (total, free, used, rate) = GetResourceUsage(scheduler);
        Message("Resource Usage:");
        Message($"  Total Qubits: {total}");
        Message($"  Free Qubits: {free}");
        Message($"  Used Qubits: {used}");
        Message($"  Usage Rate: {rate * 100.0}%");
        Message("");

        // 打印已完成任务摘要
        if Length(scheduler::completedTasks) > 0 {
            Message("Completed Tasks Summary:");
            for task in scheduler::completedTasks {
                Message($"  - {task::name}: Duration={task::actualDuration}, Qubits={Length(task::allocatedQubits)}");
            }
        }

        Message("========================");
    }
}
