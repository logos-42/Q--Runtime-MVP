namespace QuantumRuntime.TaskQueue {
    
    open QuantumRuntime.CircuitIR;
    
    newtype TaskPriority = Int;
    newtype TaskState = Int;
    
    newtype Task = (Int, String, CircuitBlock, TaskPriority, TaskState, Int[], Int, Int, Int, Int);
    
    newtype TaskQueueManager = (Task[], Int, Int, Int, Int, Int, Int);

    operation Initialize() : TaskQueueManager {
        return ([], 0, 0, 0, 0, 1, 0);
    }

    operation CreateTask(id: Int, name: String, circuit: CircuitBlock, priority: TaskPriority, ts: Int) : Task {
        let cost = GetCost(circuit);
        return (id, name, circuit, priority, 0, [], cost[2], 0, ts, ts);
    }

    operation Enqueue(mgr: TaskQueueManager, task: Task) : TaskQueueManager {
        let (queue, p, r, c, f, nextId, ts) = mgr;
        return (queue + [task], p+1, r, c, f, nextId+1, ts);
    }

    operation GetStats(mgr: TaskQueueManager) : (Int, Int, Int, Int, Int) {
        let (queue, p, r, c, f, nextId, ts) = mgr;
        return (Length(queue), p, r, c, f);
    }
}
