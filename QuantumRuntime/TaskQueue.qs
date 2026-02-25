/// 模块：任务队列
namespace QuantumRuntime.TaskQueue {

    open Microsoft.Quantum.Intrinsic;
    open QuantumRuntime.CircuitIR;

    // 任务优先级常量
    function TaskPriority_Low() : Int { return 0; }
    function TaskPriority_Normal() : Int { return 1; }
    function TaskPriority_High() : Int { return 2; }
    function TaskPriority_Critical() : Int { return 3; }

    // 任务状态常量
    function TaskState_Pending() : Int { return 0; }
    function TaskState_Scheduled() : Int { return 1; }
    function TaskState_Running() : Int { return 2; }
    function TaskState_Completed() : Int { return 3; }
    function TaskState_Failed() : Int { return 4; }

    /// 任务
    newtype Task = (
        id: Int,
        name: String,
        circuit: CircuitBlock,
        priority: Int,
        state: Int,
        allocatedQubits: Int[]
    );

    /// 任务队列管理器
    newtype TaskQueueManager = (queue: Task[], nextTaskId: Int);

    /// 初始化任务队列
    operation InitializeTaskQueue() : TaskQueueManager {
        return TaskQueueManager([], 1);
    }

    /// 创建任务
    operation CreateTask(id : Int, name : String, circuit : CircuitBlock, priority : Int) : Task {
        return Task(id, name, circuit, priority, TaskState_Pending(), []);
    }

    /// 入队
    operation Enqueue(queueManager : TaskQueueManager, task : Task) : TaskQueueManager {
        let newQueue = queueManager::queue + [task];
        return TaskQueueManager(newQueue, queueManager::nextTaskId + 1);
    }

    /// 获取优先级值
    function GetPriorityValue(priority : Int) : Int {
        return priority;
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
