/// 模块：任务队列系统
///
/// 核心设计：
/// - 定义量子任务的数据结构和状态
/// - 实现任务队列的入队、出队、peek 操作
/// - 支持任务优先级排序
/// - 跟踪任务生命周期状态

namespace QuantumRuntime.TaskQueue {

    open QuantumRuntime.CircuitIR;

    /// 任务优先级枚举
    /// 数值越高优先级越高，用于调度决策
    enum TaskPriority {
        Low,        // 0 - 最低优先级
        Normal,     // 1 - 普通优先级
        High,       // 2 - 高优先级
        Critical    // 3 - 关键优先级
    }

    /// 任务执行状态枚举
    /// 描述任务在生命周期中的当前阶段
    enum TaskState {
        Pending,    // 等待调度
        Scheduled,  // 已调度，等待执行
        Running,    // 正在执行
        Completed,  // 执行完成
        Failed      // 执行失败
    }

    /// 量子任务定义
    /// 封装一个待执行的量子电路及其元数据
    newtype Task = (
        id: Int,                    // 任务唯一标识符
        name: String,               // 任务名称（用于日志和调试）
        circuit: CircuitBlock,      // 要执行的量子电路
        priority: TaskPriority,     // 任务优先级
        state: TaskState,           // 当前执行状态
        allocatedQubits: Int[],     // 已分配的 qubit ID 列表
        estimatedDuration: Int,     // 预计执行时间（基于电路深度）
        actualDuration: Int,        // 实际执行时间
        createdAt: Int,             // 创建时间戳
        submittedAt: Int            // 提交到队列的时间戳
    );

    /// 任务队列管理器
    /// 维护待处理任务的有序集合
    newtype TaskQueueManager = (
        queue: Task[],              // 任务队列（按提交顺序）
        pendingCount: Int,          // 等待中的任务数
        runningCount: Int,          // 运行中的任务数
        completedCount: Int,        // 已完成的任务数
        failedCount: Int,           // 失败的任务数
        nextTaskId: Int,            // 下一个可用的任务 ID
        globalTimestamp: Int        // 全局时间戳
    );

    /// 初始化一个空的任务队列管理器
    operation InitializeTaskQueue() : TaskQueueManager {
        return TaskQueueManager(
            [],     // 空队列
            0,      // 无等待任务
            0,      // 无运行任务
            0,      // 无完成任务
            0,      // 无失败任务
            1,      // 任务 ID 从 1 开始
            0       // 时间戳从 0 开始
        );
    }

    /// 创建一个新的量子任务
    /// 
    /// # Parameters
    /// - `id`: 任务唯一标识符
    /// - `name`: 任务名称
    /// - `circuit`: 要执行的量子电路块
    /// - `priority`: 任务优先级
    /// - `timestamp`: 创建时间戳
    /// 
    /// # Returns
    /// 新创建的 Task 对象
    operation CreateTask(
        id: Int,
        name: String,
        circuit: CircuitBlock,
        priority: TaskPriority,
        timestamp: Int
    ) : Task {
        let cost = GetTotalResourceCost(circuit);
        return Task(
            id,
            name,
            circuit,
            priority,
            TaskState.Pending,
            [],
            cost::depthEstimate,
            0,
            timestamp,
            timestamp
        );
    }

    /// 将任务添加到队列（入队操作）
    /// 
    /// # Parameters
    /// - `queueManager`: 当前队列管理器
    /// - `task`: 要添加的任务
    /// 
    /// # Returns
    /// 更新后的队列管理器
    operation Enqueue(queueManager: TaskQueueManager, task: Task) : TaskQueueManager {
        let newQueue = queueManager::queue + [task];
        return TaskQueueManager(
            newQueue,
            queueManager::pendingCount + 1,
            queueManager::runningCount,
            queueManager::completedCount,
            queueManager::failedCount,
            queueManager::nextTaskId + 1,
            queueManager::globalTimestamp
        );
    }

    /// 从队列头部移除并返回任务（出队操作）
    /// 返回优先级最高的 Pending 状态任务
    /// 
    /// # Parameters
    /// - `queueManager`: 当前队列管理器
    /// 
    /// # Returns
    /// (可选的任务，更新后的队列管理器)
    /// 如果队列为空，返回 (None, queueManager)
    operation Dequeue(queueManager: TaskQueueManager) : (Task?, TaskQueueManager) {
        if Length(queueManager::queue) == 0 {
            return (null, queueManager);
        }

        // 查找优先级最高的 Pending 任务
        mutable bestIndex = -1;
        mutable bestPriority = -1;

        for i in 0..Length(queueManager::queue) - 1 {
            let task = queueManager::queue[i];
            if task::state == TaskState.Pending {
                let priorityValue = GetPriorityValue(task::priority);
                if priorityValue > bestPriority {
                    set bestPriority = priorityValue;
                    set bestIndex = i;
                }
            }
        }

        // 如果没有找到 Pending 任务
        if bestIndex == -1 {
            return (null, queueManager);
        }

        // 取出任务
        let dequeuedTask = queueManager::queue[bestIndex];
        
        // 从队列中移除该任务
        let newQueue = [
            queueManager::queue[i]
            | i in 0..Length(queueManager::queue) - 1
            if i != bestIndex
        ];

        let updatedManager = TaskQueueManager(
            newQueue,
            queueManager::pendingCount - 1,
            queueManager::runningCount,
            queueManager::completedCount,
            queueManager::failedCount,
            queueManager::nextTaskId,
            queueManager::globalTimestamp + 1
        );

        return (dequeuedTask, updatedManager);
    }

    /// 查看队列头部任务（不移除）
    /// 返回优先级最高的 Pending 状态任务
    /// 
    /// # Parameters
    /// - `queueManager`: 当前队列管理器
    /// 
    /// # Returns
    /// 优先级最高的任务，如果队列为空则返回 null
    operation Peek(queueManager: TaskQueueManager) : Task? {
        if Length(queueManager::queue) == 0 {
            return null;
        }

        mutable bestIndex = -1;
        mutable bestPriority = -1;

        for i in 0..Length(queueManager::queue) - 1 {
            let task = queueManager::queue[i];
            if task::state == TaskState.Pending {
                let priorityValue = GetPriorityValue(task::priority);
                if priorityValue > bestPriority {
                    set bestPriority = priorityValue;
                    set bestIndex = i;
                }
            }
        }

        if bestIndex == -1 {
            return null;
        }

        return queueManager::queue[bestIndex];
    }

    /// 根据索引获取任务
    /// 
    /// # Parameters
    /// - `queueManager`: 当前队列管理器
    /// - `index`: 任务在队列中的索引
    /// 
    /// # Returns
    /// 指定索引处的任务
    operation GetTaskByIndex(queueManager: TaskQueueManager, index: Int) : Task {
        return queueManager::queue[index];
    }

    /// 根据 ID 查找任务
    /// 
    /// # Parameters
    /// - `queueManager`: 当前队列管理器
    /// - `taskId`: 要查找的任务 ID
    /// 
    /// # Returns
    /// (找到标志，任务索引)
    /// 如果找到，返回 (true, 索引)；否则返回 (false, -1)
    operation FindTaskById(queueManager: TaskQueueManager, taskId: Int) : (Bool, Int) {
        for i in 0..Length(queueManager::queue) - 1 {
            if queueManager::queue[i]::id == taskId {
                return (true, i);
            }
        }
        return (false, -1);
    }

    /// 更新任务状态
    /// 
    /// # Parameters
    /// - `queueManager`: 当前队列管理器
    /// - `index`: 任务在队列中的索引
    /// - `newState`: 新的任务状态
    /// 
    /// # Returns
    /// 更新后的队列管理器
    operation UpdateTaskState(
        queueManager: TaskQueueManager,
        index: Int,
        newState: TaskState
    ) : TaskQueueManager {
        let oldTask = queueManager::queue[index];
        let oldState = oldTask::state;

        let updatedTask = Task(
            oldTask::id,
            oldTask::name,
            oldTask::circuit,
            oldTask::priority,
            newState,
            oldTask::allocatedQubits,
            oldTask::estimatedDuration,
            oldTask::actualDuration,
            oldTask::createdAt,
            oldTask::submittedAt
        );

        let newQueue = [
            if i == index then updatedTask else queueManager::queue[i]
            | i in 0..Length(queueManager::queue) - 1
        ];

        // 更新计数器
        let newPending = if oldState == TaskState.Pending then queueManager::pendingCount - 1
                         elif newState == TaskState.Pending then queueManager::pendingCount + 1
                         else queueManager::pendingCount;
        
        let newRunning = if oldState == TaskState.Running then queueManager::runningCount - 1
                         elif newState == TaskState.Running then queueManager::runningCount + 1
                         else queueManager::runningCount;
        
        let newCompleted = if newState == TaskState.Completed then queueManager::completedCount + 1
                           else queueManager::completedCount;
        
        let newFailed = if newState == TaskState.Failed then queueManager::failedCount + 1
                        else queueManager::failedCount;

        return TaskQueueManager(
            newQueue,
            newPending,
            newRunning,
            newCompleted,
            newFailed,
            queueManager::nextTaskId,
            queueManager::globalTimestamp + 1
        );
    }

    /// 更新任务的 qubit 分配
    /// 
    /// # Parameters
    /// - `queueManager`: 当前队列管理器
    /// - `index`: 任务在队列中的索引
    /// - `qubits`: 新分配的 qubit ID 列表
    /// 
    /// # Returns
    /// 更新后的队列管理器
    operation UpdateTaskQubits(
        queueManager: TaskQueueManager,
        index: Int,
        qubits: Int[]
    ) : TaskQueueManager {
        let oldTask = queueManager::queue[index];

        let updatedTask = Task(
            oldTask::id,
            oldTask::name,
            oldTask::circuit,
            oldTask::priority,
            oldTask::state,
            qubits,
            oldTask::estimatedDuration,
            oldTask::actualDuration,
            oldTask::createdAt,
            oldTask::submittedAt
        );

        let newQueue = [
            if i == index then updatedTask else queueManager::queue[i]
            | i in 0..Length(queueManager::queue) - 1
        ];

        return TaskQueueManager(
            newQueue,
            queueManager::pendingCount,
            queueManager::runningCount,
            queueManager::completedCount,
            queueManager::failedCount,
            queueManager::nextTaskId,
            queueManager::globalTimestamp
        );
    }

    /// 从队列中移除已完成的任务
    /// 
    /// # Parameters
    /// - `queueManager`: 当前队列管理器
    /// 
    /// # Returns
    /// (已移除的任务列表，更新后的队列管理器)
    operation RemoveCompletedTasks(queueManager: TaskQueueManager) : (Task[], TaskQueueManager) {
        let completedTasks = [
            task
            | task in queueManager::queue
            if task::state == TaskState.Completed
        ];

        let newQueue = [
            task
            | task in queueManager::queue
            if task::state != TaskState.Completed
        ];

        let updatedManager = TaskQueueManager(
            newQueue,
            queueManager::pendingCount,
            queueManager::runningCount,
            queueManager::completedCount,
            queueManager::failedCount,
            queueManager::nextTaskId,
            queueManager::globalTimestamp
        );

        return (completedTasks, updatedManager);
    }

    /// 获取优先级对应的数值
    /// 用于比较和排序
    /// 
    /// # Parameters
    /// - `priority`: 任务优先级枚举值
    /// 
    /// # Returns
    /// 优先级的数值表示（越高越优先）
    function GetPriorityValue(priority: TaskPriority) : Int {
        return priority == TaskPriority.Critical ? 3 |
               priority == TaskPriority.High ? 2 |
               priority == TaskPriority.Normal ? 1 |
               0;
    }

    /// 获取队列统计信息
    /// 
    /// # Parameters
    /// - `queueManager`: 当前队列管理器
    /// 
    /// # Returns
    /// (总任务数，等待数，运行数，完成数，失败数)
    operation GetQueueStats(queueManager: TaskQueueManager) : (Int, Int, Int, Int, Int) {
        return (
            Length(queueManager::queue),
            queueManager::pendingCount,
            queueManager::runningCount,
            queueManager::completedCount,
            queueManager::failedCount
        );
    }

    /// 打印队列状态
    /// 
    /// # Parameters
    /// - `queueManager`: 当前队列管理器
    operation PrintQueueStatus(queueManager: TaskQueueManager) : Unit {
        Message("=== Task Queue Status ===");
        let (total, pending, running, completed, failed) = GetQueueStats(queueManager);
        Message($"Total Tasks: {total}");
        Message($"  Pending:   {pending}");
        Message($"  Running:   {running}");
        Message($"  Completed: {completed}");
        Message($"  Failed:    {failed}");
        Message($"Global Timestamp: {queueManager::globalTimestamp}");
        Message("");

        if total > 0 {
            Message("Queue Contents:");
            for i in 0..total - 1 {
                let task = queueManager::queue[i];
                let priorityStr = 
                    task::priority == TaskPriority.Critical ? "Critical" |
                    task::priority == TaskPriority.High ? "High" |
                    task::priority == TaskPriority.Normal ? "Normal" |
                    "Low";
                let stateStr =
                    task::state == TaskState.Pending ? "Pending" |
                    task::state == TaskState.Scheduled ? "Scheduled" |
                    task::state == TaskState.Running ? "Running" |
                    task::state == TaskState.Completed ? "Completed" |
                    "Failed";
                Message($"  [{i}] {task::name} | Priority: {priorityStr} | State: {stateStr} | Qubits: {Length(task::allocatedQubits)}");
            }
        }
    }
}
