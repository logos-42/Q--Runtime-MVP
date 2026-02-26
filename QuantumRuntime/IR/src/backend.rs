//! Backend Adapter 模块 v0.2
//! 
//! 定义异步后端适配器接口

use crate::circuit::CircuitDag;
use crate::job::{Job, JobId, JobResult, JobStatus};
use crate::qubit::QubitMapping;
use crate::{Result, IrError};

// ============================================================================
// Backend Capabilities
// ============================================================================

/// 后端能力描述
#[derive(Debug, Clone)]
pub struct BackendCapabilities {
    /// 后端名称
    pub name: String,
    /// 后端版本
    pub version: String,
    /// 后端类型
    pub backend_type: BackendType,
    /// 支持的 qubit 数量
    pub num_qubits: usize,
    /// 支持的单比特门
    pub supported_1q_gates: Vec<&'static str>,
    /// 支持的双比特门
    pub supported_2q_gates: Vec<&'static str>,
    /// 支持的三比特门
    pub supported_3q_gates: Vec<&'static str>,
    /// 是否支持测量
    pub supports_measurement: bool,
    /// 是否支持重置
    pub supports_reset: bool,
    /// 是否支持 barrier
    pub supports_barrier: bool,
    /// 是否支持自定义操作
    pub supports_custom: bool,
    /// 最大 shots 数
    pub max_shots: u32,
    /// 原生门集
    pub native_gates: Vec<&'static str>,
    /// 耦合图（对于受限拓扑设备）
    pub coupling_map: Option<CouplingMap>,
    /// 错误模型（可选）
    pub error_model: Option<ErrorModel>,
}

/// 后端类型
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum BackendType {
    /// 理想模拟器
    IdealSimulator,
    /// 噪声模拟器
    NoiseSimulator,
    /// 超导量子计算机
    Superconducting,
    /// 离子阱量子计算机
    IonTrap,
    /// 光量子计算机
    Photonic,
    /// 中性原子
    NeutralAtom,
    /// 其他
    Custom,
}

impl std::fmt::Display for BackendType {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        match self {
            BackendType::IdealSimulator => write!(f, "IdealSimulator"),
            BackendType::NoiseSimulator => write!(f, "NoiseSimulator"),
            BackendType::Superconducting => write!(f, "Superconducting"),
            BackendType::IonTrap => write!(f, "IonTrap"),
            BackendType::Photonic => write!(f, "Photonic"),
            BackendType::NeutralAtom => write!(f, "NeutralAtom"),
            BackendType::Custom => write!(f, "Custom"),
        }
    }
}

/// 耦合图（设备拓扑）
#[derive(Debug, Clone)]
pub struct CouplingMap {
    /// 允许的 qubit 对 (control, target)
    pub edges: Vec<(usize, usize)>,
}

impl CouplingMap {
    pub fn new(edges: Vec<(usize, usize)>) -> Self {
        Self { edges }
    }
    
    /// 全连接
    pub fn fully_connected(n: usize) -> Self {
        let mut edges = Vec::new();
        for i in 0..n {
            for j in 0..n {
                if i != j {
                    edges.push((i, j));
                }
            }
        }
        Self { edges }
    }
    
    /// 线性链
    pub fn linear_chain(n: usize) -> Self {
        let mut edges = Vec::new();
        for i in 0..n - 1 {
            edges.push((i, i + 1));
            edges.push((i + 1, i));
        }
        Self { edges }
    }
    
    /// 检查是否允许连接
    pub fn allows_connection(&self, q1: usize, q2: usize) -> bool {
        self.edges.contains(&(q1, q2)) || self.edges.contains(&(q2, q1))
    }
}

/// 错误模型
#[derive(Debug, Clone)]
pub struct ErrorModel {
    /// 单比特门错误率
    pub single_qubit_error_rate: f64,
    /// 双比特门错误率
    pub two_qubit_error_rate: f64,
    /// 测量错误率
    pub measurement_error_rate: f64,
    /// T1 退相干时间（纳秒）
    pub t1_time_ns: f64,
    /// T2 退相干时间（纳秒）
    pub t2_time_ns: f64,
}

impl ErrorModel {
    pub fn new(
        single_qubit_error: f64,
        two_qubit_error: f64,
        measurement_error: f64,
        t1_ns: f64,
        t2_ns: f64,
    ) -> Self {
        Self {
            single_qubit_error_rate: single_qubit_error,
            two_qubit_error_rate: two_qubit_error,
            measurement_error_rate: measurement_error,
            t1_time_ns: t1_ns,
            t2_time_ns: t2_ns,
        }
    }
    
    /// 理想无错误
    pub fn ideal() -> Self {
        Self {
            single_qubit_error_rate: 0.0,
            two_qubit_error_rate: 0.0,
            measurement_error_rate: 0.0,
            t1_time_ns: f64::INFINITY,
            t2_time_ns: f64::INFINITY,
        }
    }
}

impl BackendCapabilities {
    /// 理想模拟器能力
    pub fn ideal_simulator() -> Self {
        Self {
            name: "Ideal Simulator".to_string(),
            version: "1.0.0".to_string(),
            backend_type: BackendType::IdealSimulator,
            num_qubits: 32,
            supported_1q_gates: vec!["X", "Y", "Z", "H", "S", "T", "Rx", "Ry", "Rz", "U"],
            supported_2q_gates: vec!["CNOT", "CZ", "SWAP", "CP", "iSWAP"],
            supported_3q_gates: vec!["Toffoli", "Fredkin"],
            supports_measurement: true,
            supports_reset: true,
            supports_barrier: true,
            supports_custom: true,
            max_shots: 1_000_000,
            native_gates: vec!["X", "Y", "Z", "H", "S", "T", "CNOT", "Rx", "Ry", "Rz"],
            coupling_map: Some(CouplingMap::fully_connected(32)),
            error_model: Some(ErrorModel::ideal()),
        }
    }
    
    /// NISQ 设备能力（示例）
    pub fn nisq_device() -> Self {
        Self {
            name: "NISQ Device".to_string(),
            version: "1.0.0".to_string(),
            backend_type: BackendType::Superconducting,
            num_qubits: 100,
            supported_1q_gates: vec!["X", "Y", "Z", "H", "S", "T", "Rx", "Ry", "Rz"],
            supported_2q_gates: vec!["CNOT", "CZ", "SWAP"],
            supported_3q_gates: vec!["Toffoli"],
            supports_measurement: true,
            supports_reset: true,
            supports_barrier: true,
            supports_custom: false,
            max_shots: 10_000,
            native_gates: vec!["X", "Y", "Z", "H", "S", "T", "CNOT", "Rz"],
            coupling_map: Some(CouplingMap::linear_chain(100)),
            error_model: Some(ErrorModel::new(0.001, 0.01, 0.02, 100_000.0, 50_000.0)),
        }
    }
    
    /// 检查是否支持某个操作
    pub fn supports_gate(&self, gate_name: &str) -> bool {
        self.supported_1q_gates.contains(&gate_name)
            || self.supported_2q_gates.contains(&gate_name)
            || self.supported_3q_gates.contains(&gate_name)
    }
}

// ============================================================================
// Backend Circuit
// ============================================================================

/// 后端电路表示
#[derive(Debug, Clone)]
pub struct BackendCircuit {
    /// 后端标识
    pub backend_name: String,
    /// 电路数据（后端特定格式）
    pub data: Vec<u8>,
    /// 元数据
    pub metadata: std::collections::HashMap<String, String>,
    /// Qubit 映射
    pub qubit_mapping: QubitMapping,
}

impl BackendCircuit {
    pub fn new(backend_name: impl Into<String>) -> Self {
        Self {
            backend_name: backend_name.into(),
            data: Vec::new(),
            metadata: std::collections::HashMap::new(),
            qubit_mapping: QubitMapping::new(),
        }
    }
    
    pub fn with_data(backend_name: impl Into<String>, data: Vec<u8>) -> Self {
        Self {
            backend_name: backend_name.into(),
            data,
            metadata: std::collections::HashMap::new(),
            qubit_mapping: QubitMapping::new(),
        }
    }
    
    pub fn with_mapping(
        backend_name: impl Into<String>,
        mapping: QubitMapping,
    ) -> Self {
        Self {
            backend_name: backend_name.into(),
            data: Vec::new(),
            metadata: std::collections::HashMap::new(),
            qubit_mapping: mapping,
        }
    }
}

// ============================================================================
// Backend Adapter Trait
// ============================================================================

/// 后端适配器 Trait
/// 
/// 所有量子后端必须实现此接口
/// 
/// 异步方法说明：
/// - submit_job: 提交作业，返回 JobId
/// - get_job_status: 查询作业状态
/// - get_job_result: 获取作业结果（阻塞直到完成）
/// - cancel_job: 取消作业
pub trait BackendAdapter: Send + Sync {
    /// 获取后端标识
    fn id(&self) -> &str;
    
    /// 获取后端能力
    fn capabilities(&self) -> BackendCapabilities;
    
    /// 将 IR 电路转换为后端格式
    fn translate_circuit(&self, circuit: &CircuitDag) -> Result<BackendCircuit>;
    
    /// 验证电路是否可在此后端执行
    fn validate_circuit(&self, circuit: &CircuitDag) -> Result<()> {
        let caps = self.capabilities();
        
        // 检查 qubit 数量
        if circuit.num_qubits() > caps.num_qubits {
            return Err(IrError::UnsupportedOperation(format!(
                "Circuit has {} qubits, backend supports {}",
                circuit.num_qubits(),
                caps.num_qubits
            )));
        }
        
        // 检查耦合图（如果有）
        if let Some(coupling) = &caps.coupling_map {
            // TODO: 验证电路中的双比特门是否符合耦合图
        }
        
        Ok(())
    }
    
    // ========================================================================
    // 异步执行接口
    // ========================================================================
    
    /// 提交作业（异步）
    /// 
    /// 返回 JobId 用于后续查询
    fn submit_job(&self, job: &Job) -> Result<JobId>;
    
    /// 获取作业状态（异步）
    fn get_job_status(&self, job_id: JobId) -> Result<JobStatus>;
    
    /// 获取作业结果（异步，可能阻塞）
    fn get_job_result(&self, job_id: JobId) -> Result<JobResult>;
    
    /// 取消作业（异步）
    fn cancel_job(&self, job_id: JobId) -> Result<()>;
    
    // ========================================================================
    // 同步执行接口（可选实现）
    // ========================================================================
    
    /// 同步执行作业（可选，用于模拟器）
    fn execute(&self, job: &Job) -> Result<JobResult> {
        // 默认实现：提交 + 轮询
        let job_id = self.submit_job(job)?;
        
        // 简单轮询（实际实现应该更高效）
        loop {
            let status = self.get_job_status(job_id)?;
            match status {
                JobStatus::Completed => return self.get_job_result(job_id),
                JobStatus::Failed | JobStatus::Cancelled => {
                    return self.get_job_result(job_id);
                }
                _ => {
                    std::thread::sleep(std::time::Duration::from_millis(100));
                }
            }
        }
    }
    
    /// 后端是否可用
    fn is_available(&self) -> bool {
        true
    }
}

// ============================================================================
// Mock Backend (for testing)
// ============================================================================

/// 模拟后端适配器（用于测试）
pub struct MockBackendAdapter {
    capabilities: BackendCapabilities,
    jobs: std::sync::Mutex<std::collections::HashMap<JobId, MockJobState>>,
    next_job_id: std::sync::atomic::AtomicU64,
}

struct MockJobState {
    status: JobStatus,
    result: Option<JobResult>,
}

impl MockBackendAdapter {
    pub fn new() -> Self {
        Self {
            capabilities: BackendCapabilities::ideal_simulator(),
            jobs: std::sync::Mutex::new(std::collections::HashMap::new()),
            next_job_id: std::sync::atomic::AtomicU64::new(1000),
        }
    }
    
    pub fn with_capabilities(capabilities: BackendCapabilities) -> Self {
        Self {
            capabilities,
            jobs: std::sync::Mutex::new(std::collections::HashMap::new()),
            next_job_id: std::sync::atomic::AtomicU64::new(1000),
        }
    }
}

impl Default for MockBackendAdapter {
    fn default() -> Self {
        Self::new()
    }
}

impl BackendAdapter for MockBackendAdapter {
    fn id(&self) -> &str {
        "mock_backend"
    }
    
    fn capabilities(&self) -> BackendCapabilities {
        self.capabilities.clone()
    }
    
    fn translate_circuit(&self, circuit: &CircuitDag) -> Result<BackendCircuit> {
        self.validate_circuit(circuit)?;
        Ok(BackendCircuit::with_data(
            self.id(),
            format!("{:?}", circuit).into_bytes(),
        ))
    }
    
    fn submit_job(&self, job: &Job) -> Result<JobId> {
        let job_id = self.next_job_id.fetch_add(1, std::sync::atomic::Ordering::SeqCst);
        
        // 模拟立即完成
        let mut result = JobResult::success(job_id);
        result.execution_time_ms = Some(1);
        
        // 生成随机测量结果
        for &qubit in &job.circuit.all_qubits() {
            let counts: Vec<u8> = (0..job.shots)
                .map(|i| ((job_id + i as u64 + qubit.value()) % 2) as u8)
                .collect();
            result.add_counts(qubit, counts);
        }
        
        let mut jobs = self.jobs.lock().unwrap();
        jobs.insert(
            job_id,
            MockJobState {
                status: JobStatus::Completed,
                result: Some(result),
            },
        );
        
        Ok(job_id)
    }
    
    fn get_job_status(&self, job_id: JobId) -> Result<JobStatus> {
        let jobs = self.jobs.lock().unwrap();
        jobs.get(&job_id)
            .map(|s| s.status)
            .ok_or_else(|| IrError::QubitNotFound(format!("Job {} not found", job_id)))
    }
    
    fn get_job_result(&self, job_id: JobId) -> Result<JobResult> {
        let jobs = self.jobs.lock().unwrap();
        jobs.get(&job_id)
            .and_then(|s| s.result.clone())
            .ok_or_else(|| IrError::QubitNotFound(format!("Job {} not found", job_id)))
    }
    
    fn cancel_job(&self, job_id: JobId) -> Result<()> {
        let mut jobs = self.jobs.lock().unwrap();
        if let Some(state) = jobs.get_mut(&job_id) {
            if !state.status.is_terminal() {
                state.status = JobStatus::Cancelled;
                return Ok(());
            }
        }
        Err(IrError::JobExecutionFailed("Job cannot be cancelled".to_string()))
    }
}

// ============================================================================
// Ideal Simulator Backend
// ============================================================================

/// 理想模拟器后端
pub struct IdealSimulatorBackend {
    capabilities: BackendCapabilities,
    jobs: std::sync::Mutex<std::collections::HashMap<JobId, MockJobState>>,
    next_job_id: std::sync::atomic::AtomicU64,
}

impl IdealSimulatorBackend {
    pub fn new() -> Self {
        Self {
            capabilities: BackendCapabilities::ideal_simulator(),
            jobs: std::sync::Mutex::new(std::collections::HashMap::new()),
            next_job_id: std::sync::atomic::AtomicU64::new(2000),
        }
    }
}

impl Default for IdealSimulatorBackend {
    fn default() -> Self {
        Self::new()
    }
}

impl BackendAdapter for IdealSimulatorBackend {
    fn id(&self) -> &str {
        "ideal_simulator"
    }
    
    fn capabilities(&self) -> BackendCapabilities {
        self.capabilities.clone()
    }
    
    fn translate_circuit(&self, circuit: &CircuitDag) -> Result<BackendCircuit> {
        self.validate_circuit(circuit)?;
        Ok(BackendCircuit::new(self.id()))
    }
    
    fn submit_job(&self, job: &Job) -> Result<JobId> {
        let job_id = self.next_job_id.fetch_add(1, std::sync::atomic::Ordering::SeqCst);
        
        let mut result = JobResult::success(job_id);
        result.execution_time_ms = Some(1);
        
        for &qubit in &job.circuit.all_qubits() {
            let counts: Vec<u8> = (0..job.shots)
                .map(|i| ((job_id + i as u64 + qubit.value()) % 2) as u8)
                .collect();
            result.add_counts(qubit, counts);
        }
        
        let mut jobs = self.jobs.lock().unwrap();
        jobs.insert(
            job_id,
            MockJobState {
                status: JobStatus::Completed,
                result: Some(result),
            },
        );
        
        Ok(job_id)
    }
    
    fn get_job_status(&self, job_id: JobId) -> Result<JobStatus> {
        let jobs = self.jobs.lock().unwrap();
        jobs.get(&job_id)
            .map(|s| s.status)
            .ok_or_else(|| IrError::QubitNotFound(format!("Job {} not found", job_id)))
    }
    
    fn get_job_result(&self, job_id: JobId) -> Result<JobResult> {
        let jobs = self.jobs.lock().unwrap();
        jobs.get(&job_id)
            .and_then(|s| s.result.clone())
            .ok_or_else(|| IrError::QubitNotFound(format!("Job {} not found", job_id)))
    }
    
    fn cancel_job(&self, job_id: JobId) -> Result<()> {
        let mut jobs = self.jobs.lock().unwrap();
        if let Some(state) = jobs.get_mut(&job_id) {
            if !state.status.is_terminal() {
                state.status = JobStatus::Cancelled;
                return Ok(());
            }
        }
        Err(IrError::JobExecutionFailed("Job cannot be cancelled".to_string()))
    }
}

// ============================================================================
// Tests
// ============================================================================

#[cfg(test)]
mod tests {
    use super::*;
    use crate::circuit::bell_state_dag;

    #[test]
    fn test_backend_capabilities() {
        let caps = BackendCapabilities::ideal_simulator();
        assert_eq!(caps.backend_type, BackendType::IdealSimulator);
        assert!(caps.supports_measurement);
        assert!(caps.supported_1q_gates.contains(&"H"));
    }

    #[test]
    fn test_coupling_map() {
        let fully = CouplingMap::fully_connected(4);
        assert!(fully.allows_connection(0, 1));
        assert!(fully.allows_connection(3, 0));
        
        let linear = CouplingMap::linear_chain(4);
        assert!(linear.allows_connection(0, 1));
        assert!(!linear.allows_connection(0, 3));
    }

    #[test]
    fn test_mock_backend() {
        let backend = MockBackendAdapter::new();
        let circuit = bell_state_dag();
        let job = Job::new(circuit, 100, "mock_backend");
        
        let job_id = backend.submit_job(&job).unwrap();
        assert!(job_id > 0);
        
        let status = backend.get_job_status(job_id).unwrap();
        assert_eq!(status, JobStatus::Completed);
        
        let result = backend.get_job_result(job_id).unwrap();
        assert_eq!(result.status, JobStatus::Completed);
    }

    #[test]
    fn test_ideal_simulator() {
        let backend = IdealSimulatorBackend::new();
        assert_eq!(backend.id(), "ideal_simulator");
        
        let caps = backend.capabilities();
        assert_eq!(caps.num_qubits, 32);
    }

    #[test]
    fn test_error_model() {
        let ideal = ErrorModel::ideal();
        assert_eq!(ideal.single_qubit_error_rate, 0.0);
        assert!(ideal.t1_time_ns.is_infinite());
        
        let noisy = ErrorModel::new(0.001, 0.01, 0.02, 100_000.0, 50_000.0);
        assert_eq!(noisy.single_qubit_error_rate, 0.001);
        assert_eq!(noisy.two_qubit_error_rate, 0.01);
    }
}
