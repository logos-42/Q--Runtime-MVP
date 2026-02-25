/// 模块：任务队列
namespace QuantumRuntime.TaskQueue {

    open QuantumRuntime.CircuitIR;

    /// 任务优先级
    enum TaskPriority {
        Low = 0,
        Normal = 1,
        High = 2,
        Critical = 3
    }

    /// 任务状态
    enum TaskState {
        Pending = 0,
        Scheduled = 1,
        Running = 2,
        Completed = 3,
        Failed = 4
    }

    /// 任务
    newtype Task = (
        id: Int,
        name: String,
        circuit: CircuitBlock,
        priority: TaskPriority,
        state: TaskState,
        allocatedQubits: Int[]
    );

    /// 任务队列管理器
    newtype TaskQueueManager = (queue: Task[], nextTaskId: Int);

    /// 初始化任务队列
    operation InitializeTaskQueue() : TaskQueueManager {
        return TaskQueueManager([], 1);
    }

    /// 创建任务
    operation CreateTask(id : Int, name : String, circuit : CircuitBlock, priority : TaskPriority) : Task {
        return Task(id, name, circuit, priority, TaskState.Pending, []);
    }

    /// 入队
    operation Enqueue(queueManager : TaskQueueManager, task : Task) : TaskQueueManager {
        let newQueue = queueManager::queue + [task];
        return TaskQueueManager(newQueue, queueManager::nextTaskId + 1);
    }

    /// 获取优先级值
    function GetPriorityValue(priority : TaskPriority) : Int {
        if priority == TaskPriority.Critical { return 4; }
        elif priority == TaskPriority.High { return 3; }
        elif priority == TaskPriority.Normal { return 2; }
        else { return 1; }
    }

    /// 获取队列统计
    operation GetStats(queueManager : TaskQueueManager) : (Int, Int) {
        return (Length(queueManager::queue), queueManager::nextTaskId);
    }

    /// 打印队列状态
    operation PrintQueueStatus(queueManager : TaskQueueManager) : Unit {
        let (count, nextId) = GetStats(queueManager);
        Message($"Queue: {count} tasks, next ID: {nextId}");
        for i in 0..count - 1 {
            let task = queueManager::queue[i];
            Message($"  [{i}] {task::name} - Priority: {task::priority}");
        }
    }
}
