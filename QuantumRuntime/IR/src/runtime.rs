//! Runtime 执行引擎模块 v0.2
//! 
//! QuantumRuntime 负责协调调度器、后端和执行流程

use std::sync::Arc;
use crate::circuit::CircuitDag;
use crate::job::{Job, JobId, JobResult, JobScheduler, JobStatus, Priority, JobMetadata};
use crate::backend::{BackendAdapter, BackendCapabilities};

// ============================================================================
// Runtime Configuration
// ============================================================================

/// Runtime 配置
#[derive(Debug, Clone)]
pub struct RuntimeConfig {
    /// 最大并发作业数
    pub max_concurrent_jobs: usize,
    /// 默认后端
    pub default_backend: String,
    /// 作业超时时间（秒）
    pub job_timeout_secs: u64,
    /// 启用详细日志
    pub verbose: bool,
}

impl Default for RuntimeConfig {
    fn default() -> Self {
        Self {
            max_concurrent_jobs: 4,
            default_backend: "default".to_string(),
            job_timeout_secs: 300,
            verbose: false,
        }
    }
}

impl RuntimeConfig {
    pub fn new() -> Self {
        Self::default()
    }
    
    pub fn with_max_jobs(mut self, max: usize) -> Self {
        self.max_concurrent_jobs = max;
        self
    }
    
    pub fn with_default_backend(mut self, backend: impl Into<String>) -> Self {
        self.default_backend = backend.into();
        self
    }
    
    pub fn with_timeout(mut self, secs: u64) -> Self {
        self.job_timeout_secs = secs;
        self
    }
    
    pub fn with_verbose(mut self, verbose: bool) -> Self {
        self.verbose = verbose;
        self
    }
}

// ============================================================================
// Backend Registry
// ============================================================================

/// 后端注册表
#[derive(Default)]
pub struct BackendRegistry {
    backends: std::collections::HashMap<String, Arc<dyn BackendAdapter>>,
}

impl BackendRegistry {
    pub fn new() -> Self {
        Self::default()
    }
    
    pub fn register(&mut self, id: impl Into<String>, backend: Arc<dyn BackendAdapter>) {
        self.backends.insert(id.into(), backend);
    }
    
    pub fn get(&self, id: &str) -> Option<Arc<dyn BackendAdapter>> {
        self.backends.get(id).cloned()
    }
    
    pub fn list(&self) -> Vec<&str> {
        self.backends.keys().map(|s| s.as_str()).collect()
    }
    
    pub fn contains(&self, id: &str) -> bool {
        self.backends.contains_key(id)
    }
}

// ============================================================================
// Runtime Statistics
// ============================================================================

/// Runtime 统计信息
#[derive(Debug, Default)]
pub struct RuntimeStats {
    pub total_jobs_submitted: u64,
    pub total_jobs_completed: u64,
    pub total_jobs_failed: u64,
    pub total_execution_time_ms: u64,
    pub current_running_jobs: u64,
}

impl RuntimeStats {
    pub fn average_execution_time(&self) -> Option<f64> {
        if self.total_jobs_completed == 0 {
            return None;
        }
        Some(self.total_execution_time_ms as f64 / self.total_jobs_completed as f64)
    }
}

// ============================================================================
// Quantum Runtime
// ============================================================================

/// 量子运行时引擎
/// 
/// 核心职责：
/// 1. 管理后端注册表
/// 2. 管理作业调度器
/// 3. 协调作业执行流程
/// 4. 提供同步/异步执行接口
pub struct QuantumRuntime {
    config: RuntimeConfig,
    registry: BackendRegistry,
    scheduler: JobScheduler,
    stats: RuntimeStats,
    running: bool,
}

impl QuantumRuntime {
    /// 创建新的 Runtime
    pub fn new(config: RuntimeConfig) -> Self {
        let max_jobs = config.max_concurrent_jobs;
        Self {
            config,
            registry: BackendRegistry::new(),
            scheduler: JobScheduler::new(max_jobs),
            stats: RuntimeStats::default(),
            running: false,
        }
    }
    
    /// 注册后端
    pub fn register_backend(&mut self, id: impl Into<String>, backend: Arc<dyn BackendAdapter>) {
        self.registry.register(id, backend);
    }
    
    /// 获取后端
    pub fn get_backend(&self, id: &str) -> Option<Arc<dyn BackendAdapter>> {
        self.registry.get(id)
    }
    
    /// 获取所有后端列表
    pub fn list_backends(&self) -> Vec<&str> {
        self.registry.list()
    }
    
    /// 获取后端能力
    pub fn get_backend_capabilities(&self, id: &str) -> Option<BackendCapabilities> {
        self.registry.get(id).map(|b| b.capabilities())
    }
    
    /// 提交作业
    pub fn submit_job(&mut self, job: Job) -> JobId {
        let job_id = job.id;
        self.scheduler.submit(job);
        self.stats.total_jobs_submitted += 1;
        job_id
    }
    
    /// 创建并提交新作业
    pub fn create_job(
        &mut self,
        circuit: CircuitDag,
        shots: u32,
        priority: Priority,
        metadata: JobMetadata,
    ) -> JobId {
        let backend = if circuit.metadata().name.is_some() {
            circuit.metadata().name.clone().unwrap_or(self.config.default_backend.clone())
        } else {
            self.config.default_backend.clone()
        };
        
        let job = Job::new(circuit, shots, backend)
            .with_priority(priority)
            .with_metadata(metadata);
        
        self.submit_job(job)
    }
    
    /// 调度并执行下一个作业（同步）
    pub fn schedule_and_execute(&mut self) -> Option<JobResult> {
        // 调度下一个可执行的作业
        let job = self.scheduler.schedule_next()?;
        let job_id = job.id;
        
        // 获取后端
        let backend = self.registry.get(&job.target_backend)?;
        
        // 执行
        match backend.execute(&job) {
            Ok(result) => {
                self.scheduler.complete(job_id, result.clone());
                self.stats.total_jobs_completed += 1;
                if let Some(exec_time) = result.execution_time_ms {
                    self.stats.total_execution_time_ms += exec_time;
                }
                Some(result)
            }
            Err(e) => {
                let result = JobResult::failure(job_id, e.to_string());
                self.scheduler.complete(job_id, result.clone());
                self.stats.total_jobs_failed += 1;
                Some(result)
            }
        }
    }
    
    /// 执行所有排队的作业
    pub fn execute_all(&mut self) -> Vec<JobResult> {
        let mut results = Vec::new();
        
        while let Some(result) = self.schedule_and_execute() {
            results.push(result);
        }
        
        results
    }
    
    /// 获取作业状态
    pub fn get_job_status(&self, job_id: JobId) -> Option<JobStatus> {
        self.scheduler.get_status(job_id)
    }
    
    /// 获取作业结果
    pub fn get_job_result(&self, job_id: JobId) -> Option<JobResult> {
        self.scheduler.get_result(job_id).cloned()
    }
    
    /// 取消作业
    pub fn cancel_job(&mut self, job_id: JobId) -> bool {
        self.scheduler.cancel(job_id)
    }
    
    /// 获取统计信息
    pub fn stats(&self) -> &RuntimeStats {
        &self.stats
    }
    
    /// 获取调度器引用
    pub fn scheduler(&self) -> &JobScheduler {
        &self.scheduler
    }
    
    /// 获取可变引用
    pub fn scheduler_mut(&mut self) -> &mut JobScheduler {
        &mut self.scheduler
    }
    
    /// 检查是否在运行
    pub fn is_running(&self) -> bool {
        self.running
    }
    
    /// 启动 Runtime
    pub fn start(&mut self) {
        self.running = true;
    }
    
    /// 停止 Runtime
    pub fn stop(&mut self) {
        self.running = false;
    }
    
    /// 重置统计
    pub fn reset_stats(&mut self) {
        self.stats = RuntimeStats::default();
    }
}

impl Default for QuantumRuntime {
    fn default() -> Self {
        Self::new(RuntimeConfig::default())
    }
}

// ============================================================================
// Async Runtime (Skeleton)
// ============================================================================

/// 异步运行时句柄
pub struct AsyncRuntimeHandle {
    runtime_id: u64,
}

impl AsyncRuntimeHandle {
    pub fn new(runtime_id: u64) -> Self {
        Self { runtime_id }
    }
    
    pub fn runtime_id(&self) -> u64 {
        self.runtime_id
    }
}

/// 异步执行上下文
pub struct AsyncExecutionContext {
    job_id: JobId,
    backend_id: String,
}

impl AsyncExecutionContext {
    pub fn new(job_id: JobId, backend_id: impl Into<String>) -> Self {
        Self {
            job_id,
            backend_id: backend_id.into(),
        }
    }
    
    pub fn job_id(&self) -> JobId {
        self.job_id
    }
    
    pub fn backend_id(&self) -> &str {
        &self.backend_id
    }
}

// ============================================================================
// Tests
// ============================================================================

#[cfg(test)]
mod tests {
    use super::*;
    use crate::circuit::bell_state_dag;
    use crate::backend::MockBackendAdapter;

    #[test]
    fn test_runtime_creation() {
        let config = RuntimeConfig::new()
            .with_max_jobs(8)
            .with_default_backend("simulator");
        
        let runtime = QuantumRuntime::new(config);
        
        assert_eq!(runtime.config.max_concurrent_jobs, 8);
        assert!(!runtime.is_running());
    }

    #[test]
    fn test_runtime_backend_registration() {
        let mut runtime = QuantumRuntime::default();
        let backend = Arc::new(MockBackendAdapter::new());
        
        runtime.register_backend("test_backend", backend);
        
        assert!(runtime.get_backend("test_backend").is_some());
        assert!(runtime.list_backends().contains(&"test_backend"));
    }

    #[test]
    fn test_runtime_job_submission() {
        let mut runtime = QuantumRuntime::default();
        let circuit = bell_state_dag();
        
        let job_id = runtime.create_job(
            circuit,
            100,
            Priority::Normal,
            JobMetadata::new(),
        );
        
        assert!(job_id > 0);
        assert_eq!(runtime.stats.total_jobs_submitted, 1);
    }

    #[test]
    fn test_runtime_stats() {
        let mut runtime = QuantumRuntime::default();
        
        // 模拟统计
        runtime.stats.total_jobs_submitted = 10;
        runtime.stats.total_jobs_completed = 8;
        runtime.stats.total_execution_time_ms = 800;
        
        assert_eq!(runtime.stats.average_execution_time(), Some(100.0));
    }

    #[test]
    fn test_runtime_start_stop() {
        let mut runtime = QuantumRuntime::default();
        
        assert!(!runtime.is_running());
        
        runtime.start();
        assert!(runtime.is_running());
        
        runtime.stop();
        assert!(!runtime.is_running());
    }
}
