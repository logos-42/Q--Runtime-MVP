//! Job 和调度器模块 v0.2
//! 
//! 包含 Job 抽象、优先级队列和调度器

use std::sync::atomic::{AtomicU64, Ordering};
use crate::qubit::LogicalQubitId;
use crate::circuit::CircuitDag;

// ============================================================================
// Type Definitions
// ============================================================================

/// Job 唯一标识符
pub type JobId = u64;

/// 调度器句柄
pub type SchedulerHandle = u64;

// ============================================================================
// Priority
// ============================================================================

/// 作业优先级
#[derive(Debug, Clone, Copy, PartialEq, Eq, PartialOrd, Ord, Default)]
#[repr(u8)]
pub enum Priority {
    Low = 0,
    #[default]
    Normal = 1,
    High = 2,
    Urgent = 3,
}

impl Priority {
    pub fn from_u8(value: u8) -> Self {
        match value {
            0 => Priority::Low,
            1 => Priority::Normal,
            2 => Priority::High,
            3.. => Priority::Urgent,
        }
    }
    
    pub fn to_u8(self) -> u8 {
        self as u8
    }
}

// ============================================================================
// Job Status
// ============================================================================

/// 作业状态
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum JobStatus {
    /// 已创建，等待提交
    Pending,
    /// 已提交到调度器
    Queued,
    /// 已分配资源，等待执行
    Ready,
    /// 正在执行
    Running,
    /// 执行完成
    Completed,
    /// 执行失败
    Failed,
    /// 已取消
    Cancelled,
    /// 等待依赖
    Waiting,
}

impl JobStatus {
    pub fn is_terminal(&self) -> bool {
        matches!(self, JobStatus::Completed | JobStatus::Failed | JobStatus::Cancelled)
    }
    
    pub fn is_runnable(&self) -> bool {
        matches!(self, JobStatus::Ready | JobStatus::Running)
    }
    
    pub fn is_queued(&self) -> bool {
        matches!(self, JobStatus::Queued | JobStatus::Ready | JobStatus::Waiting)
    }
}

// ============================================================================
// Job Metadata
// ============================================================================

/// 作业元数据
#[derive(Debug, Clone, Default)]
pub struct JobMetadata {
    pub user_id: Option<String>,
    pub project: Option<String>,
    pub experiment_name: Option<String>,
    pub custom: std::collections::HashMap<String, String>,
}

impl JobMetadata {
    pub fn new() -> Self {
        Self::default()
    }
    
    pub fn with_user(mut self, user_id: impl Into<String>) -> Self {
        self.user_id = Some(user_id.into());
        self
    }
    
    pub fn with_project(mut self, project: impl Into<String>) -> Self {
        self.project = Some(project.into());
        self
    }
    
    pub fn with_experiment(mut self, name: impl Into<String>) -> Self {
        self.experiment_name = Some(name.into());
        self
    }
    
    pub fn with_custom(mut self, key: impl Into<String>, value: impl Into<String>) -> Self {
        self.custom.insert(key.into(), value.into());
        self
    }
}

// ============================================================================
// Job Result
// ============================================================================

/// 作业执行结果
#[derive(Debug, Clone)]
pub struct JobResult {
    pub job_id: JobId,
    pub status: JobStatus,
    /// 测量结果：qubit -> 结果列表 (0/1)
    pub counts: std::collections::HashMap<LogicalQubitId, Vec<u8>>,
    /// 统计：qubit -> P(1)
    pub statistics: std::collections::HashMap<LogicalQubitId, f64>,
    /// 执行时间（毫秒）
    pub execution_time_ms: Option<u64>,
    /// 错误信息
    pub error: Option<String>,
    /// 后端返回的额外数据
    pub backend_data: Option<String>,
}

impl JobResult {
    pub fn success(job_id: JobId) -> Self {
        Self {
            job_id,
            status: JobStatus::Completed,
            counts: std::collections::HashMap::new(),
            statistics: std::collections::HashMap::new(),
            execution_time_ms: None,
            error: None,
            backend_data: None,
        }
    }
    
    pub fn failure(job_id: JobId, error: impl Into<String>) -> Self {
        Self {
            job_id,
            status: JobStatus::Failed,
            counts: std::collections::HashMap::new(),
            statistics: std::collections::HashMap::new(),
            execution_time_ms: None,
            error: Some(error.into()),
            backend_data: None,
        }
    }
    
    pub fn add_counts(&mut self, qubit: LogicalQubitId, counts: Vec<u8>) {
        let ones = counts.iter().filter(|&&x| x == 1).count() as f64;
        let total = counts.len() as f64;
        let prob = if total > 0.0 { ones / total } else { 0.0 };
        
        self.counts.insert(qubit, counts);
        self.statistics.insert(qubit, prob);
    }
    
    pub fn get_counts(&self, qubit: LogicalQubitId) -> Option<&Vec<u8>> {
        self.counts.get(&qubit)
    }
    
    pub fn get_probability(&self, qubit: LogicalQubitId) -> Option<f64> {
        self.statistics.get(&qubit).copied()
    }
}

// ============================================================================
// Job
// ============================================================================

/// 量子计算作业
#[derive(Debug, Clone)]
pub struct Job {
    pub id: JobId,
    pub circuit: CircuitDag,
    pub shots: u32,
    pub priority: Priority,
    pub target_backend: String,
    pub status: JobStatus,
    pub metadata: JobMetadata,
    pub created_at: u64,
    pub submitted_at: Option<u64>,
    pub started_at: Option<u64>,
    pub completed_at: Option<u64>,
    /// 依赖的其他 Job ID
    pub depends_on: Vec<JobId>,
    /// 占用的 qubit 资源
    pub allocated_qubits: Vec<LogicalQubitId>,
}

impl Job {
    pub fn new(circuit: CircuitDag, shots: u32, target_backend: impl Into<String>) -> Self {
        let allocated_qubits = circuit.all_qubits();
        Self {
            id: Self::generate_id(),
            circuit,
            shots,
            priority: Priority::default(),
            target_backend: target_backend.into(),
            status: JobStatus::Pending,
            metadata: JobMetadata::default(),
            created_at: current_timestamp(),
            submitted_at: None,
            started_at: None,
            completed_at: None,
            depends_on: Vec::new(),
            allocated_qubits,
        }
    }
    
    pub fn with_priority(mut self, priority: Priority) -> Self {
        self.priority = priority;
        self
    }
    
    pub fn with_metadata(mut self, metadata: JobMetadata) -> Self {
        self.metadata = metadata;
        self
    }
    
    pub fn with_dependency(mut self, job_id: JobId) -> Self {
        self.depends_on.push(job_id);
        self
    }
    
    pub fn set_status(&mut self, status: JobStatus) {
        let now = current_timestamp();
        self.status = status;
        
        match status {
            JobStatus::Queued => self.submitted_at = Some(now),
            JobStatus::Running => self.started_at = Some(now),
            JobStatus::Completed | JobStatus::Failed | JobStatus::Cancelled => {
                self.completed_at = Some(now);
            }
            _ => {}
        }
    }
    
    pub fn cancel(&mut self) -> bool {
        if self.status.is_terminal() {
            return false;
        }
        self.set_status(JobStatus::Cancelled);
        true
    }
    
    pub fn execution_duration(&self) -> Option<u64> {
        match (self.started_at, self.completed_at) {
            (Some(start), Some(end)) => Some(end - start),
            _ => None,
        }
    }
    
    fn generate_id() -> JobId {
        static COUNTER: AtomicU64 = AtomicU64::new(1);
        COUNTER.fetch_add(1, Ordering::SeqCst)
    }
}

// ============================================================================
// Job Queue (Priority Queue)
// ============================================================================

/// 作业优先级队列
#[derive(Debug, Default)]
pub struct JobQueue {
    jobs: std::collections::VecDeque<Job>,
}

impl JobQueue {
    pub fn new() -> Self {
        Self::default()
    }
    
    /// 按优先级添加作业
    pub fn push(&mut self, job: Job) {
        let pos = self.jobs
            .iter()
            .position(|j| j.priority < job.priority)
            .unwrap_or(self.jobs.len());
        self.jobs.insert(pos, job);
    }
    
    /// 获取下一个作业
    pub fn pop(&mut self) -> Option<Job> {
        self.jobs.pop_front()
    }
    
    /// 查看下一个作业
    pub fn peek(&self) -> Option<&Job> {
        self.jobs.front()
    }
    
    pub fn len(&self) -> usize {
        self.jobs.len()
    }
    
    pub fn is_empty(&self) -> bool {
        self.jobs.is_empty()
    }
    
    pub fn find(&self, id: JobId) -> Option<&Job> {
        self.jobs.iter().find(|j| j.id == id)
    }
    
    pub fn find_mut(&mut self, id: JobId) -> Option<&mut Job> {
        self.jobs.iter_mut().find(|j| j.id == id)
    }
    
    pub fn remove(&mut self, id: JobId) -> Option<Job> {
        let pos = self.jobs.iter().position(|j| j.id == id)?;
        Some(self.jobs.remove(pos).unwrap())
    }
    
    pub fn iter(&self) -> impl Iterator<Item = &Job> {
        self.jobs.iter()
    }
    
    pub fn iter_mut(&mut self) -> impl Iterator<Item = &mut Job> {
        self.jobs.iter_mut()
    }
}

// ============================================================================
// Job Scheduler
// ============================================================================

/// 作业调度器
/// 
/// 负责管理作业队列、资源分配和调度决策
#[derive(Debug)]
pub struct JobScheduler {
    /// 等待队列
    queue: JobQueue,
    /// 运行中的作业
    running: std::collections::HashMap<JobId, Job>,
    /// 已完成的作业
    completed: std::collections::HashMap<JobId, JobResult>,
    /// 可用的 qubit 资源
    available_qubits: std::collections::HashSet<LogicalQubitId>,
    /// 最大并发作业数
    max_concurrent_jobs: usize,
    /// 调度统计
    stats: SchedulerStats,
}

#[derive(Debug, Default)]
pub struct SchedulerStats {
    pub total_submitted: u64,
    pub total_completed: u64,
    pub total_failed: u64,
    pub total_cancelled: u64,
    pub current_queue_depth: u64,
}

impl JobScheduler {
    pub fn new(max_concurrent_jobs: usize) -> Self {
        Self {
            queue: JobQueue::new(),
            running: std::collections::HashMap::new(),
            completed: std::collections::HashMap::new(),
            available_qubits: std::collections::HashSet::new(),
            max_concurrent_jobs,
            stats: SchedulerStats::default(),
        }
    }
    
    /// 初始化可用 qubit
    pub fn with_qubits(mut self, qubits: Vec<LogicalQubitId>) -> Self {
        self.available_qubits = qubits.into_iter().collect();
        self
    }
    
    /// 提交作业
    pub fn submit(&mut self, mut job: Job) -> JobId {
        let job_id = job.id;
        job.set_status(JobStatus::Queued);
        self.stats.total_submitted += 1;
        self.queue.push(job);
        self.stats.current_queue_depth = self.queue.len() as u64;
        job_id
    }
    
    /// 调度下一个可执行的作业
    pub fn schedule_next(&mut self) -> Option<Job> {
        if self.running.len() >= self.max_concurrent_jobs {
            return None;
        }
        
        // 查找第一个资源可用的作业
        let mut candidates: Vec<usize> = Vec::new();
        for (i, job) in self.queue.iter().enumerate() {
            if self.can_schedule(job) {
                candidates.push(i);
            }
        }
        
        if candidates.is_empty() {
            return None;
        }
        
        // 选择优先级最高的
        let best_idx = candidates
            .into_iter()
            .max_by_key(|&i| self.queue.iter().nth(i).unwrap().priority)
            .unwrap();
        
        let mut job = self.queue.jobs.remove(best_idx).unwrap();
        job.set_status(JobStatus::Ready);
        
        // 分配资源
        for &q in &job.allocated_qubits {
            self.available_qubits.remove(&q);
        }
        
        self.stats.current_queue_depth = self.queue.len() as u64;
        Some(job)
    }
    
    /// 检查作业是否可以调度
    fn can_schedule(&self, job: &Job) -> bool {
        // 检查依赖
        for &dep_id in &job.depends_on {
            if !self.completed.contains_key(&dep_id) {
                return false;
            }
        }
        
        // 检查资源
        job.allocated_qubits.iter().all(|q| self.available_qubits.contains(q))
    }
    
    /// 开始执行作业
    pub fn start_execution(&mut self, job_id: JobId) -> Option<&mut Job> {
        if let Some(job) = self.queue.find_mut(job_id) {
            job.set_status(JobStatus::Running);
            return None;  // 作业还在队列中
        }
        
        if let Some(job) = self.running.get_mut(&job_id) {
            job.set_status(JobStatus::Running);
            return Some(job);
        }
        
        None
    }
    
    /// 标记作业完成
    pub fn complete(&mut self, job_id: JobId, result: JobResult) {
        if let Some(job) = self.running.remove(&job_id) {
            // 释放资源
            for &q in &job.allocated_qubits {
                self.available_qubits.insert(q);
            }
            
            if result.status == JobStatus::Completed {
                self.stats.total_completed += 1;
            } else {
                self.stats.total_failed += 1;
            }
            
            self.completed.insert(job_id, result);
        }
    }
    
    /// 取消作业
    pub fn cancel(&mut self, job_id: JobId) -> bool {
        // 从队列中取消
        if let Some(job) = self.queue.remove(job_id) {
            let mut job = job;
            job.cancel();
            self.stats.total_cancelled += 1;
            return true;
        }
        
        // 从运行中取消
        if let Some(mut job) = self.running.remove(&job_id) {
            job.cancel();
            self.stats.total_cancelled += 1;
            
            // 释放资源
            for &q in &job.allocated_qubits {
                self.available_qubits.insert(q);
            }
            return true;
        }
        
        false
    }
    
    /// 获取作业状态
    pub fn get_status(&self, job_id: JobId) -> Option<JobStatus> {
        if let Some(job) = self.queue.find(job_id) {
            return Some(job.status);
        }
        if let Some(job) = self.running.get(&job_id) {
            return Some(job.status);
        }
        if let Some(result) = self.completed.get(&job_id) {
            return Some(result.status);
        }
        None
    }
    
    /// 获取作业结果
    pub fn get_result(&self, job_id: JobId) -> Option<&JobResult> {
        self.completed.get(&job_id)
    }
    
    /// 获取运行中的作业
    pub fn running_jobs(&self) -> Vec<&Job> {
        self.running.values().collect()
    }
    
    /// 获取队列长度
    pub fn queue_length(&self) -> usize {
        self.queue.len()
    }
    
    /// 获取统计信息
    pub fn stats(&self) -> &SchedulerStats {
        &self.stats
    }
}

impl Default for JobScheduler {
    fn default() -> Self {
        Self::new(4)
    }
}

// ============================================================================
// Helper Functions
// ============================================================================

fn current_timestamp() -> u64 {
    use std::time::{SystemTime, UNIX_EPOCH};
    SystemTime::now()
        .duration_since(UNIX_EPOCH)
        .unwrap_or_default()
        .as_secs()
}

// ============================================================================
// Tests
// ============================================================================

#[cfg(test)]
mod tests {
    use super::*;
    use crate::circuit::bell_state_dag;

    #[test]
    fn test_job_creation() {
        let circuit = bell_state_dag();
        let job = Job::new(circuit, 100, "simulator");
        
        assert!(job.id > 0);
        assert_eq!(job.shots, 100);
        assert_eq!(job.priority, Priority::Normal);
        assert_eq!(job.status, JobStatus::Pending);
    }

    #[test]
    fn test_job_priority() {
        let circuit = bell_state_dag();
        let job = Job::new(circuit, 100, "simulator")
            .with_priority(Priority::High);
        
        assert_eq!(job.priority, Priority::High);
    }

    #[test]
    fn test_job_queue_ordering() {
        let mut queue = JobQueue::new();
        
        let job1 = Job::new(bell_state_dag(), 100, "sim");
        let job2 = Job::new(bell_state_dag(), 100, "sim")
            .with_priority(Priority::High);
        let job3 = Job::new(bell_state_dag(), 100, "sim")
            .with_priority(Priority::Low);
        
        queue.push(job1);
        queue.push(job2);
        queue.push(job3);
        
        // 高优先级应该先出队
        let next = queue.pop().unwrap();
        assert_eq!(next.priority, Priority::High);
    }

    #[test]
    fn test_scheduler_submit() {
        let mut scheduler = JobScheduler::new(4);
        let circuit = bell_state_dag();
        
        let job_id = scheduler.submit(Job::new(circuit, 100, "simulator"));
        
        assert!(job_id > 0);
        assert_eq!(scheduler.queue_length(), 1);
        assert_eq!(scheduler.stats.total_submitted, 1);
    }

    #[test]
    fn test_scheduler_schedule() {
        let mut scheduler = JobScheduler::new(4)
            .with_qubits(vec![
                LogicalQubitId::new(0),
                LogicalQubitId::new(1),
            ]);
        
        let circuit = bell_state_dag();
        let job_id = scheduler.submit(Job::new(circuit, 100, "simulator"));
        
        let scheduled = scheduler.schedule_next();
        assert!(scheduled.is_some());
        assert_eq!(scheduled.unwrap().id, job_id);
    }

    #[test]
    fn test_job_result() {
        let mut result = JobResult::success(1);
        result.add_counts(LogicalQubitId::new(0), vec![0, 1, 0, 1, 1]);
        
        assert_eq!(result.status, JobStatus::Completed);
        assert_eq!(result.get_counts(LogicalQubitId::new(0)).unwrap().len(), 5);
        assert!((result.get_probability(LogicalQubitId::new(0)).unwrap() - 0.6).abs() < 0.01);
    }

    #[test]
    fn test_scheduler_resource_management() {
        let mut scheduler = JobScheduler::new(2)
            .with_qubits(vec![LogicalQubitId::new(0), LogicalQubitId::new(1)]);

        // 提交一个占用 qubit 0 和 1 的作业（bell 态需要 2 个 qubit）
        let circuit = bell_state_dag();
        let job = Job::new(circuit, 100, "simulator");
        let job_id = scheduler.submit(job);

        // 调度（返回 Job 但不自动加入 running）
        let scheduled = scheduler.schedule_next();
        assert!(scheduled.is_some());
        
        // 验证作业已从队列移除
        assert_eq!(scheduler.queue_length(), 0);
    }
}
