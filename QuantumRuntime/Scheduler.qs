/// 模块：调度器（增强版：集成依赖追踪和自动逆电路）
///
/// 核心设计：
/// - 基于任务优先级和 qubit 资源状态调度
/// - 检测任务间的依赖关系和资源冲突
/// - 支持自动逆电路生成（用于 uncomputation）

namespace QuantumRuntime.Scheduler {

    open QuantumRuntime.QubitPool;
    open QuantumRuntime.CircuitIR;
    open QuantumRuntime.TaskQueue;

    // ============================================
    // 类型定义
    // ============================================

    /// 调度策略枚举
    enum SchedulingPolicy {
        FIFO,           // 先进先出
        Priority,       // 优先级优先
        ResourceAware   // 资源感知（考虑 qubit 依赖）
    }

    /// 调度器配置
    newtype SchedulerConfig = (
        policy: SchedulingPolicy,
        maxConcurrentTasks: Int,
        enablePreemption: Bool,
        enableDependencyTracking: Bool  // 新增：启用依赖追踪
    );

    /// 调度器状态
    newtype Scheduler = (
        config: SchedulerConfig,
        taskQueue: TaskQueueManager,
        qubitPool: QubitPoolManager,
        scheduledTasks: Task[],
        completedTasks: Task[],
        globalTimestamp: Int
    );

    // ============================================
    // 基础操作
    // ============================================

    /// 初始化调度器
    operation InitializeScheduler(numQubits: Int) : Scheduler {
        let config = SchedulerConfig(
            SchedulingPolicy::Priority,
            5,
            false,
            true  // 默认启用依赖追踪
        );
        let pool = InitializeQubitPool(numQubits);
        let queue = InitializeTaskQueue();

        return Scheduler(
            config,
            queue,
            pool,
            [],
            [],
            0
        );
    }

    /// 创建并提交任务
    operation CreateAndSubmitTask(
        scheduler: Scheduler,
        name: String,
        circuit: CircuitBlock,
        priority: TaskPriority
    ) : (Int, Scheduler) {
        // 创建任务
        let task = CreateTask(name, circuit, priority, scheduler::globalTimestamp);

        // 提交到队列
        let (taskId, newQueue) = SubmitTask(scheduler::taskQueue, task);

        return (
            taskId,
            Scheduler(
                scheduler::config,
                newQueue,
                scheduler::qubitPool,
                scheduler::scheduledTasks,
                scheduler::completedTasks,
                scheduler::globalTimestamp + 1
            )
        );
    }

    /// 选择下一个要执行的任务
    operation SelectNextTask(scheduler: Scheduler) : (Task?, Scheduler) {
        if scheduler::config::policy == SchedulingPolicy::FIFO {
            // FIFO：选择最早提交的任务
            return DequeueNextTask(scheduler::taskQueue);
        } else {
            // Priority 或 ResourceAware：选择最高优先级任务
            return DequeueNextTask(scheduler::taskQueue);
        }
    }

    /// 调度并执行下一个任务
    operation ScheduleAndExecuteNext(scheduler: Scheduler) : (Task?, Scheduler) {
        // 选择任务
        let (taskOpt, schedulerWithDequeue) = SelectNextTask(scheduler);

        if taskOpt == null {
            return (null, scheduler);
        }

        let task = taskOpt!!;

        // 检查依赖（如果启用）
        if scheduler::config::enableDependencyTracking {
            if not CanExecuteTask(scheduler::taskQueue, task::id) {
                // 依赖未满足，重新入队
                let (_, newQueue) = SubmitTask(scheduler::taskQueue, task);
                return (null, Scheduler(
                    scheduler::config,
                    newQueue,
                    scheduler::qubitPool,
                    scheduler::scheduledTasks,
                    scheduler::completedTasks,
                    scheduler::globalTimestamp
                ));
            }
        }

        // 分配 qubit
        let (allocatedQubits, newPool) = AllocateQubitsForTask(
            scheduler::qubitPool,
            task::estimatedDuration
        );

        if Length(allocatedQubits) < task::estimatedDuration {
            // 资源不足，重新入队
            let (_, newQueue) = SubmitTask(scheduler::taskQueue, task);
            return (null, Scheduler(
                scheduler::config,
                newQueue,
                scheduler::qubitPool,
                scheduler::scheduledTasks,
                scheduler::completedTasks,
                scheduler::globalTimestamp
            ));
        }

        // 更新任务状态
        let updatedTask = Task(
            task::id,
            task::name,
            task::circuit,
            task::priority,
            TaskState::Running,
            allocatedQubits,
            task::estimatedDuration,
            task::actualDuration,
            task::createdAt,
            task::submittedAt,
            task::dependency
        );

        // 记录 qubit 依赖（如果启用）
        let finalPool = if scheduler::config::enableDependencyTracking {
            RecordTaskQubitDependencies(newPool, updatedTask)
        } else {
            newPool
        };

        let newScheduler = Scheduler(
            scheduler::config,
            schedulerWithDequeue::taskQueue,
            finalPool,
            scheduler::scheduledTasks + [updatedTask],
            scheduler::completedTasks,
            scheduler::globalTimestamp + 1
        );

        return (updatedTask, newScheduler);
    }

    /// 完成任务
    operation CompleteTask(
        scheduler: Scheduler,
        taskId: Int,
        success: Bool
    ) : Scheduler {
        mutable scheduled = scheduler::scheduledTasks;
        mutable completed = scheduler::completedTasks;

        // 找到任务
        for i in 0..Length(scheduler::scheduledTasks) - 1 {
            if scheduler::scheduledTasks[i]::id == taskId {
                let task = scheduler::scheduledTasks[i];
                
                // 从 scheduled 移除
                set scheduled = [
                    scheduler::scheduledTasks[j]
                    | j in 0..Length(scheduler::scheduledTasks) - 1
                    if j != i
                ];

                // 更新状态并添加到 completed
                let finalTask = Task(
                    task::id,
                    task::name,
                    task::circuit,
                    task::priority,
                    if success then TaskState::Completed else TaskState::Failed,
                    task::allocatedQubits,
                    task::estimatedDuration,
                    scheduler::globalTimestamp - task::submittedAt,
                    task::createdAt,
                    task::submittedAt,
                    task::dependency
                );
                set completed = completed + [finalTask];

                // 释放 qubit
                let newPool = ReleaseQubitsFromTask(
                    scheduler::qubitPool,
                    task::allocatedQubits
                );

                // 清除依赖（如果启用）
                let finalPool = if scheduler::config::enableDependencyTracking {
                    ClearTaskQubitDependencies(newPool, task)
                } else {
                    newPool
                };

                return Scheduler(
                    scheduler::config,
                    scheduler::taskQueue,
                    finalPool,
                    scheduled,
                    completed,
                    scheduler::globalTimestamp
                );
            }
        }

        return scheduler;
    }

    // ============================================
    // 资源分配操作
    // ============================================

    /// 为任务分配 qubit
    operation AllocateQubitsForTask(
        pool: QubitPoolManager,
        numQubits: Int
    ) : (Int[], QubitPoolManager) {
        mutable allocated = [];
        mutable currentPool = pool;

        for _ in 0..numQubits - 1 {
            let (qubitId, newPool) = AllocateQubit(currentPool);
            set allocated = allocated + [qubitId];
            set currentPool = newPool;
        }

        return (allocated, currentPool);
    }

    /// 释放任务占用的 qubit
    operation ReleaseQubitsFromTask(
        pool: QubitPoolManager,
        qubitIds: Int[]
    ) : QubitPoolManager {
        mutable currentPool = pool;

        for qubitId in qubitIds {
            set currentPool = ReleaseQubit(qubitId, currentPool);
        }

        return currentPool;
    }

    // ============================================
    // 依赖追踪操作
    // ============================================

    /// 记录任务与 qubit 的依赖关系
    operation RecordTaskQubitDependencies(
        pool: QubitPoolManager,
        task: Task
    ) : QubitPoolManager {
        mutable currentPool = pool;

        // 为任务分配的所有 qubit 建立依赖关系
        let qubits = task::allocatedQubits;
        for i in 0..Length(qubits) - 1 {
            for j in i+1..Length(qubits) - 1 {
                set currentPool = RecordEntanglement(
                    qubits[i],
                    qubits[j],
                    currentPool
                );
            }
        }

        return currentPool;
    }

    /// 清除任务的 qubit 依赖关系
    operation ClearTaskQubitDependencies(
        pool: QubitPoolManager,
        task: Task
    ) : QubitPoolManager {
        mutable currentPool = pool;

        for qubitId in task::allocatedQubits {
            set currentPool = ClearDependencies(qubitId, currentPool);
        }

        return currentPool;
    }

    /// 检查资源冲突
    operation CheckResourceConflict(
        scheduler: Scheduler,
        requiredQubits: Int[]
    ) : Bool {
        // 检查是否有正在运行的任务使用相同的 qubit
        for task in scheduler::scheduledTasks {
            if task::state == TaskState::Running {
                for q1 in requiredQubits {
                    for q2 in task::allocatedQubits {
                        if q1 == q2 {
                            return true;
                        }
                    }
                }
            }
        }
        return false;
    }

    // ============================================
    // 改进 2：自动逆电路支持
    // ============================================

    /// 使用逆电路执行任务（自动 uncomputation 模式）
    operation ExecuteWithAutoUncompute(
        scheduler: Scheduler,
        task: Task
    ) : (Task, Scheduler) {
        // 检查电路是否可逆
        if not task::circuit::isReversible {
            // 不可逆，直接执行
            return ExecuteTaskDirect(scheduler, task);
        }

        // 生成逆电路
        let inverseCircuit = GenerateInverseCircuit(task::circuit);

        // 创建逆任务
        let inverseTask = Task(
            -task::id,  // 负 ID 表示是逆任务
            task::name + "†",
            inverseCircuit,
            task::priority,
            TaskState::Pending,
            task::allocatedQubits,
            task::estimatedDuration,
            task::actualDuration,
            task::createdAt,
            task::submittedAt,
            task::dependency
        );

        // 先执行原任务
        let (_, schedulerAfterTask) = ExecuteTaskDirect(scheduler, task);

        // 再执行逆任务（清理临时状态）
        let (_, finalScheduler) = ExecuteTaskDirect(schedulerAfterTask, inverseTask);

        return (task, finalScheduler);
    }

    /// 直接执行任务
    operation ExecuteTaskDirect(
        scheduler: Scheduler,
        task: Task
    ) : (Task, Scheduler) {
        // 更新任务状态为 Running
        let runningTask = Task(
            task::id,
            task::name,
            task::circuit,
            task::priority,
            TaskState::Running,
            task::allocatedQubits,
            task::estimatedDuration,
            task::actualDuration,
            task::createdAt,
            task::submittedAt,
            task::dependency
        );

        let newScheduler = Scheduler(
            scheduler::config,
            scheduler::taskQueue,
            scheduler::qubitPool,
            scheduler::scheduledTasks + [runningTask],
            scheduler::completedTasks,
            scheduler::globalTimestamp + 1
        );

        return (runningTask, newScheduler);
    }

    // ============================================
    // 统计和查询
    // ============================================

    /// 获取资源使用率
    operation GetResourceUsage(scheduler: Scheduler) : (Int, Int, Int, Double) {
        let (total, free, _) = GetPoolStats(scheduler::qubitPool);
        let used = total - free;
        let usage = (used / IFloor(IntAsDouble(total))) * 100.0;
        return (total, used, free, usage);
    }

    /// 获取调度器状态摘要
    operation GetSchedulerSummary(scheduler: Scheduler) : String {
        let (pending, running, completed, failed, total) = 
            GetQueueStats(scheduler::taskQueue);
        let (totalQ, usedQ, freeQ, usage) = GetResourceUsage(scheduler);

        return $"Scheduler: {pending} pending, {running} running, {completed} completed, {failed} failed | Qubits: {usedQ}/{totalQ} ({usage}%)";
    }

    function IFloor(x: Double) : Int {
        return Round(x - 0.5);
    }
}
