//! Quantum Runtime IR v0.2
//! 
//! 量子计算中间表示层，支持 DAG 电路结构和异步后端。
//! 
//! ## 模块结构
//! ```text
//! lib.rs       - 库入口和错误类型
//! qubit.rs     - Qubit 抽象（逻辑/物理分离）
//! operation.rs - 操作抽象（门、测量、自定义）
//! circuit.rs   - DAG 电路结构
//! job.rs       - Job 和调度器
//! runtime.rs   - QuantumRuntime 执行引擎
//! backend.rs   - 异步 BackendAdapter trait
//! ```

#![allow(dead_code)]
#![allow(unused_variables)]

// ============================================================================
// Module Declarations
// ============================================================================

pub mod qubit;
pub mod operation;
pub mod circuit;
pub mod job;
pub mod runtime;
pub mod backend;
pub mod prelude;

// ============================================================================
// Re-exports
// ============================================================================

pub use qubit::{LogicalQubitId, PhysicalQubitId, QubitState};
pub use operation::{Operation, SingleQubitGate, TwoQubitGate, CustomOp};
pub use circuit::{CircuitDag, OperationNode};
pub use job::{Job, JobId, Priority, JobStatus, JobResult, JobScheduler};
pub use runtime::QuantumRuntime;
pub use backend::{BackendAdapter, BackendCapabilities, BackendCircuit};

// ============================================================================
// Error Types
// ============================================================================

/// IR 操作可能返回的错误
#[derive(Debug, Clone, PartialEq)]
pub enum IrError {
    /// Qubit 不存在
    QubitNotFound(String),
    /// Qubit 已分配
    QubitAlreadyAllocated(String),
    /// 无效的操作
    InvalidOperation(String),
    /// Backend 不支持该操作
    UnsupportedOperation(String),
    /// Backend 不可用
    BackendUnavailable(String),
    /// Job 执行失败
    JobExecutionFailed(String),
    /// DAG 循环依赖
    CyclicDependency(String),
    /// 调度冲突
    SchedulingConflict(String),
    /// 异步操作超时
    Timeout(String),
}

impl std::fmt::Display for IrError {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        match self {
            IrError::QubitNotFound(msg) => write!(f, "Qubit not found: {}", msg),
            IrError::QubitAlreadyAllocated(msg) => write!(f, "Qubit already allocated: {}", msg),
            IrError::InvalidOperation(msg) => write!(f, "Invalid operation: {}", msg),
            IrError::UnsupportedOperation(msg) => write!(f, "Unsupported operation: {}", msg),
            IrError::BackendUnavailable(msg) => write!(f, "Backend unavailable: {}", msg),
            IrError::JobExecutionFailed(msg) => write!(f, "Job execution failed: {}", msg),
            IrError::CyclicDependency(msg) => write!(f, "Cyclic dependency detected: {}", msg),
            IrError::SchedulingConflict(msg) => write!(f, "Scheduling conflict: {}", msg),
            IrError::Timeout(msg) => write!(f, "Operation timeout: {}", msg),
        }
    }
}

impl std::error::Error for IrError {}

pub type Result<T> = std::result::Result<T, IrError>;

// ============================================================================
// Async Trait Support (without external crate)
// ============================================================================

/// 简化的 Future trait（用于 async trait 模拟）
pub trait FutureLike<T> {
    fn poll(&mut self) -> Option<T>;
}

/// 异步任务句柄
#[derive(Debug, Clone)]
pub struct AsyncTaskHandle<T> {
    id: u64,
    completed: bool,
    result: Option<T>,
}

impl<T> AsyncTaskHandle<T> {
    pub fn new(id: u64) -> Self {
        Self {
            id,
            completed: false,
            result: None,
        }
    }
    
    pub fn is_completed(&self) -> bool {
        self.completed
    }
    
    pub fn set_result(&mut self, result: T) {
        self.result = Some(result);
        self.completed = true;
    }
    
    pub fn get_result(&self) -> Option<&T> {
        self.result.as_ref()
    }
}
