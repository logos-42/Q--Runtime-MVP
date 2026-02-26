//! Prelude 模块 v0.2
//! 
//! 提供常用的导入，简化使用代码。

pub use crate::qubit::{LogicalQubitId, PhysicalQubitId, QubitState, LogicalQubit, LogicalQubitManager, QubitMapping};
pub use crate::operation::{
    Operation, SingleQubitGate, TwoQubitGate, ThreeQubitGate, CustomOp, MeasurementResult,
    x, y, z, h, s, t, rx, ry, rz, cnot, cz, swap, measure, toffoli,
};
pub use crate::circuit::{CircuitDag, CircuitDagBuilder, OperationNode, CircuitMetadata, bell_state_dag, ghz_dag};
pub use crate::job::{Job, JobId, Priority, JobStatus, JobResult, JobMetadata, JobScheduler, SchedulerStats};
pub use crate::backend::{
    BackendAdapter, BackendCapabilities, BackendType, BackendCircuit, CouplingMap, ErrorModel,
    MockBackendAdapter, IdealSimulatorBackend,
};
pub use crate::runtime::{QuantumRuntime, RuntimeConfig, RuntimeStats, BackendRegistry};
pub use crate::{Result, IrError};
