using System;
using System.Collections.Generic;
using System.Linq;
using AIIntegration.Scheduler;
using AIIntegration.AI;

namespace AIIntegration.Runtime
{
    /// <summary>
    /// Phase 3: AI增强的调度器
    /// 集成AISchedulerAdapter，保持与Phase 2的向后兼容
    /// </summary>
    public class AIEnhancedScheduler
    {
        private List<Task> _taskQueue;
        private List<Task> _completedTasks;
        private AISchedulerAdapter _aiAdapter;
        private int _numQubits;
        private float _currentTime;
        private List<int> _qubitAllocations;
        
        public AIEnhancedScheduler(int numQubits, OperationMode mode = OperationMode.Hybrid)
        {
            _numQubits = numQubits;
            _taskQueue = new List<Task>();
            _completedTasks = new List<Task>();
            _aiAdapter = new AISchedulerAdapter { Mode = mode };
            _currentTime = 0f;
            _qubitAllocations = Enumerable.Range(0, numQubits)
                .Select(_ => 0).ToList();
        }
        
        // ==================== 任务管理 ====================
        
        public void SubmitTask(int taskId, string name, CircuitBlock circuit, TaskPriority priority)
        {
            var task = new Task(
                Id: taskId,
                Name: name,
                Circuit: circuit,
                Priority: priority,
                SubmitTime: _currentTime
            );
            _taskQueue.Add(task);
        }
        
        /// <summary>
        /// Step 2核心修改：使用AI选择下一个任务
        /// </summary>
        public int SelectNextTask()
        {
            if (_taskQueue.Count == 0)
                return -1;
            
            // 过滤：移除完成的任务
            _taskQueue = _taskQueue.Where(t => t.Status != TaskStatus.Completed).ToList();
            
            if (_taskQueue.Count == 0)
                return -1;
            
            // 使用AI计算每个任务的评分
            var bestIdx = 0;
            var bestScore = float.MinValue;
            
            for (int i = 0; i < _taskQueue.Count; i++)
            {
                var score = _aiAdapter.ComputeTaskScore(_taskQueue[i], _currentTime);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestIdx = i;
                }
            }
            
            return bestIdx;
        }
        
        public Task GetTask(int taskIndex)
        {
            if (taskIndex < 0 || taskIndex >= _taskQueue.Count)
                return null!;
            return _taskQueue[taskIndex];
        }
        
        // ==================== 资源管理 ====================
        
        public bool CanAllocateQubits(int count)
        {
            var available = _qubitAllocations.Count(q => q == 0);
            return available >= count;
        }
        
        public List<int> AllocateQubits(int count)
        {
            var allocated = new List<int>();
            for (int i = 0; i < _numQubits && allocated.Count < count; i++)
            {
                if (_qubitAllocations[i] == 0)
                {
                    _qubitAllocations[i] = 1;
                    allocated.Add(i);
                }
            }
            return allocated;
        }
        
        public void ReleaseQubits(List<int> qubits)
        {
            foreach (var q in qubits)
            {
                if (q >= 0 && q < _numQubits)
                    _qubitAllocations[q] = 0;
            }
        }
        
        // ==================== 故障检测集成 ====================
        
        /// <summary>
        /// 检查任务的qubits是否安全
        /// 使用故障预测器
        /// </summary>
        public (bool isSafe, List<int> riskyQubits) ValidateTaskQubits(Task task)
        {
            var riskyQubits = _aiAdapter.GetRiskyQubits(threshold: 0.7f);
            var isSafe = riskyQubits.Count == 0;
            return (isSafe, riskyQubits);
        }
        
        // ==================== 任务执行 ====================
        
        public void ExecuteTask(Task task, float executionTime)
        {
            // Step 3核心：记录执行数据供AI学习
            _aiAdapter.RecordTaskExecution(task, executionTime);
            
            // 记录qubit使用
            var allocatedQubits = AllocateQubits(task.Circuit.QubitCount);
            foreach (var q in allocatedQubits)
            {
                _aiAdapter.RecordQubitUsage(q);
            }
            
            // 更新任务状态
            var completedTask = task with { Status = TaskStatus.Completed, ExecutionTime = executionTime };
            _completedTasks.Add(completedTask);
            
            ReleaseQubits(allocatedQubits);
            _currentTime += executionTime;
        }
        
        // ==================== 性能统计 ====================
        
        public void PrintStatistics()
        {
            Console.WriteLine("\n=== 调度器统计 ===");
            Console.WriteLine($"提交任务数: {_taskQueue.Count + _completedTasks.Count}");
            Console.WriteLine($"完成任务数: {_completedTasks.Count}");
            Console.WriteLine($"待处理任务数: {_taskQueue.Count}");
            Console.WriteLine($"总执行时间: {_currentTime:F2}");
            
            if (_completedTasks.Count > 0)
            {
                var avgTime = _completedTasks.Average(t => t.ExecutionTime ?? 0);
                var totalQubits = _completedTasks.Sum(t => t.Circuit.QubitCount);
                Console.WriteLine($"平均任务时间: {avgTime:F2}");
                Console.WriteLine($"总qubit-时间: {totalQubits * _currentTime:F0}");
            }
            
            _aiAdapter.PrintDiagnostics();
        }
    }
}
