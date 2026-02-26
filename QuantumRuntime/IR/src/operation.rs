//! Operation 抽象模块 v0.2
//! 
//! 定义量子操作（门、测量、barrier、自定义操作）

use crate::qubit::LogicalQubitId;

// ============================================================================
// Single Qubit Gates
// ============================================================================

/// 单比特量子门
#[derive(Debug, Clone, Copy, PartialEq)]
pub enum SingleQubitGate {
    // Pauli 门
    X,
    Y,
    Z,
    // Hadamard 门
    H,
    // Phase 门
    S,
    T,
    Sdg,  // S†
    Tdg,  // T†
    // Rotation 门（带参数）
    Rx(f64),
    Ry(f64),
    Rz(f64),
    // Phase 门（带参数）
    P(f64),
    // U 门（通用单比特门，3 个参数）
    U(f64, f64, f64),
}

impl SingleQubitGate {
    pub fn name(&self) -> &'static str {
        match self {
            SingleQubitGate::X => "X",
            SingleQubitGate::Y => "Y",
            SingleQubitGate::Z => "Z",
            SingleQubitGate::H => "H",
            SingleQubitGate::S => "S",
            SingleQubitGate::T => "T",
            SingleQubitGate::Sdg => "Sdg",
            SingleQubitGate::Tdg => "Tdg",
            SingleQubitGate::Rx(_) => "Rx",
            SingleQubitGate::Ry(_) => "Ry",
            SingleQubitGate::Rz(_) => "Rz",
            SingleQubitGate::P(_) => "P",
            SingleQubitGate::U(_, _, _) => "U",
        }
    }
    
    pub fn is_parametric(&self) -> bool {
        matches!(self, 
            SingleQubitGate::Rx(_) | SingleQubitGate::Ry(_) | 
            SingleQubitGate::Rz(_) | SingleQubitGate::P(_) | 
            SingleQubitGate::U(_, _, _))
    }
    
    pub fn parameters(&self) -> Vec<f64> {
        match self {
            SingleQubitGate::Rx(theta) => vec![*theta],
            SingleQubitGate::Ry(theta) => vec![*theta],
            SingleQubitGate::Rz(theta) => vec![*theta],
            SingleQubitGate::P(phi) => vec![*phi],
            SingleQubitGate::U(theta, phi, lam) => vec![*theta, *phi, *lam],
            _ => vec![],
        }
    }
}

// ============================================================================
// Two Qubit Gates
// ============================================================================

/// 双比特量子门
#[derive(Debug, Clone, Copy, PartialEq)]
pub enum TwoQubitGate {
    // 控制非门
    CNOT,
    // 控制 Z 门
    CZ,
    // SWAP 门
    SWAP,
    // 控制相位门
    CP(f64),
    // iSWAP 门
    ISWAP,
    // sqrt(SWAP) 门
    SqrtSWAP,
    // Mølmer-Sørensen 门（离子阱）
    MS(f64),
}

impl TwoQubitGate {
    pub fn name(&self) -> &'static str {
        match self {
            TwoQubitGate::CNOT => "CNOT",
            TwoQubitGate::CZ => "CZ",
            TwoQubitGate::SWAP => "SWAP",
            TwoQubitGate::CP(_) => "CP",
            TwoQubitGate::ISWAP => "iSWAP",
            TwoQubitGate::SqrtSWAP => "√SWAP",
            TwoQubitGate::MS(_) => "MS",
        }
    }
    
    pub fn is_parametric(&self) -> bool {
        matches!(self, TwoQubitGate::CP(_) | TwoQubitGate::MS(_))
    }
    
    pub fn parameters(&self) -> Vec<f64> {
        match self {
            TwoQubitGate::CP(phi) => vec![*phi],
            TwoQubitGate::MS(theta) => vec![*theta],
            _ => vec![],
        }
    }
}

// ============================================================================
// Measurement
// ============================================================================

/// 测量结果
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum MeasurementResult {
    Zero,
    One,
}

impl From<u8> for MeasurementResult {
    fn from(value: u8) -> Self {
        match value {
            0 => MeasurementResult::Zero,
            1 => MeasurementResult::One,
            _ => panic!("Invalid measurement result: {}", value),
        }
    }
}

impl From<MeasurementResult> for u8 {
    fn from(result: MeasurementResult) -> u8 {
        match result {
            MeasurementResult::Zero => 0,
            MeasurementResult::One => 1,
        }
    }
}

// ============================================================================
// Custom Operation
// ============================================================================

/// 自定义操作
/// 
/// 用于扩展不支持的标准门或实验性操作
#[derive(Debug, Clone, PartialEq)]
pub struct CustomOp {
    /// 操作名称
    pub name: String,
    /// 目标 qubit
    pub qubits: Vec<LogicalQubitId>,
    /// 参数列表
    pub params: Vec<f64>,
    /// 元数据（可选）
    pub metadata: std::collections::HashMap<String, String>,
}

impl CustomOp {
    pub fn new(name: impl Into<String>) -> Self {
        Self {
            name: name.into(),
            qubits: Vec::new(),
            params: Vec::new(),
            metadata: std::collections::HashMap::new(),
        }
    }
    
    pub fn with_qubits(mut self, qubits: Vec<LogicalQubitId>) -> Self {
        self.qubits = qubits;
        self
    }
    
    pub fn with_params(mut self, params: Vec<f64>) -> Self {
        self.params = params;
        self
    }
    
    pub fn with_metadata(mut self, key: impl Into<String>, value: impl Into<String>) -> Self {
        self.metadata.insert(key.into(), value.into());
        self
    }
}

// ============================================================================
// Operation Enum
// ============================================================================

/// 量子操作
/// 
/// 所有可执行操作的统一抽象
#[derive(Debug, Clone, PartialEq)]
pub enum Operation {
    /// 单比特门
    Gate1 {
        gate: SingleQubitGate,
        target: LogicalQubitId,
    },
    /// 双比特门
    Gate2 {
        gate: TwoQubitGate,
        control: LogicalQubitId,
        target: LogicalQubitId,
    },
    /// 三比特门（如 Toffoli）
    Gate3 {
        gate: ThreeQubitGate,
        controls: [LogicalQubitId; 2],
        target: LogicalQubitId,
    },
    /// 测量操作
    Measure {
        qubit: LogicalQubitId,
        /// 可选：存储结果的经典寄存器索引
        classical_reg: Option<usize>,
    },
    /// 重置操作
    Reset {
        qubit: LogicalQubitId,
    },
    /// Barrier（同步屏障）
    Barrier {
        qubits: Vec<LogicalQubitId>,
    },
    /// 自定义操作
    Custom(CustomOp),
}

/// 三比特门
#[derive(Debug, Clone, Copy, PartialEq)]
pub enum ThreeQubitGate {
    /// Toffoli 门（CCNOT）
    Toffoli,
    /// Fredkin 门（CSWAP）
    Fredkin,
    /// CCZ 门
    CCZ,
}

impl ThreeQubitGate {
    pub fn name(&self) -> &'static str {
        match self {
            ThreeQubitGate::Toffoli => "Toffoli",
            ThreeQubitGate::Fredkin => "Fredkin",
            ThreeQubitGate::CCZ => "CCZ",
        }
    }
}

impl Operation {
    /// 创建单比特门操作
    pub fn gate1(gate: SingleQubitGate, target: LogicalQubitId) -> Self {
        Operation::Gate1 { gate, target }
    }
    
    /// 创建双比特门操作
    pub fn gate2(gate: TwoQubitGate, control: LogicalQubitId, target: LogicalQubitId) -> Self {
        Operation::Gate2 { gate, control, target }
    }
    
    /// 创建三比特门操作
    pub fn gate3(gate: ThreeQubitGate, control1: LogicalQubitId, control2: LogicalQubitId, target: LogicalQubitId) -> Self {
        Operation::Gate3 {
            gate,
            controls: [control1, control2],
            target,
        }
    }
    
    /// 创建测量操作
    pub fn measure(qubit: LogicalQubitId) -> Self {
        Operation::Measure { qubit, classical_reg: None }
    }
    
    /// 创建带经典寄存器的测量操作
    pub fn measure_to(qubit: LogicalQubitId, classical_reg: usize) -> Self {
        Operation::Measure { qubit, classical_reg: Some(classical_reg) }
    }
    
    /// 创建重置操作
    pub fn reset(qubit: LogicalQubitId) -> Self {
        Operation::Reset { qubit }
    }
    
    /// 创建 barrier
    pub fn barrier(qubits: Vec<LogicalQubitId>) -> Self {
        Operation::Barrier { qubits }
    }
    
    /// 获取操作涉及的 qubit 列表
    pub fn qubits(&self) -> Vec<LogicalQubitId> {
        match self {
            Operation::Gate1 { target, .. } => vec![*target],
            Operation::Gate2 { control, target, .. } => vec![*control, *target],
            Operation::Gate3 { controls, target, .. } => {
                vec![controls[0], controls[1], *target]
            }
            Operation::Measure { qubit, .. } => vec![*qubit],
            Operation::Reset { qubit } => vec![*qubit],
            Operation::Barrier { qubits } => qubits.clone(),
            Operation::Custom(op) => op.qubits.clone(),
        }
    }
    
    /// 获取操作名称
    pub fn name(&self) -> &str {
        match self {
            Operation::Gate1 { gate, .. } => gate.name(),
            Operation::Gate2 { gate, .. } => gate.name(),
            Operation::Gate3 { gate, .. } => gate.name(),
            Operation::Measure { .. } => "M",
            Operation::Reset { .. } => "Reset",
            Operation::Barrier { .. } => "Barrier",
            Operation::Custom(op) => &op.name,
        }
    }
    
    /// 获取参数列表
    pub fn parameters(&self) -> Vec<f64> {
        match self {
            Operation::Gate1 { gate, .. } => gate.parameters(),
            Operation::Gate2 { gate, .. } => gate.parameters(),
            Operation::Gate3 { .. } => vec![],
            Operation::Measure { .. } => vec![],
            Operation::Reset { .. } => vec![],
            Operation::Barrier { .. } => vec![],
            Operation::Custom(op) => op.params.clone(),
        }
    }
    
    /// 检查是否是测量操作
    pub fn is_measurement(&self) -> bool {
        matches!(self, Operation::Measure { .. })
    }
    
    /// 检查是否是 barrier
    pub fn is_barrier(&self) -> bool {
        matches!(self, Operation::Barrier { .. })
    }
    
    /// 检查是否是重置操作
    pub fn is_reset(&self) -> bool {
        matches!(self, Operation::Reset { .. })
    }
    
    /// 检查是否是自定义操作
    pub fn is_custom(&self) -> bool {
        matches!(self, Operation::Custom(_))
    }
}

// ============================================================================
// Convenience Functions
// ============================================================================

pub fn x(qubit: LogicalQubitId) -> Operation {
    Operation::gate1(SingleQubitGate::X, qubit)
}

pub fn y(qubit: LogicalQubitId) -> Operation {
    Operation::gate1(SingleQubitGate::Y, qubit)
}

pub fn z(qubit: LogicalQubitId) -> Operation {
    Operation::gate1(SingleQubitGate::Z, qubit)
}

pub fn h(qubit: LogicalQubitId) -> Operation {
    Operation::gate1(SingleQubitGate::H, qubit)
}

pub fn s(qubit: LogicalQubitId) -> Operation {
    Operation::gate1(SingleQubitGate::S, qubit)
}

pub fn t(qubit: LogicalQubitId) -> Operation {
    Operation::gate1(SingleQubitGate::T, qubit)
}

pub fn rx(qubit: LogicalQubitId, theta: f64) -> Operation {
    Operation::gate1(SingleQubitGate::Rx(theta), qubit)
}

pub fn ry(qubit: LogicalQubitId, theta: f64) -> Operation {
    Operation::gate1(SingleQubitGate::Ry(theta), qubit)
}

pub fn rz(qubit: LogicalQubitId, theta: f64) -> Operation {
    Operation::gate1(SingleQubitGate::Rz(theta), qubit)
}

pub fn cnot(control: LogicalQubitId, target: LogicalQubitId) -> Operation {
    Operation::gate2(TwoQubitGate::CNOT, control, target)
}

pub fn cz(control: LogicalQubitId, target: LogicalQubitId) -> Operation {
    Operation::gate2(TwoQubitGate::CZ, control, target)
}

pub fn swap(q1: LogicalQubitId, q2: LogicalQubitId) -> Operation {
    Operation::gate2(TwoQubitGate::SWAP, q1, q2)
}

pub fn toffoli(c1: LogicalQubitId, c2: LogicalQubitId, t: LogicalQubitId) -> Operation {
    Operation::gate3(ThreeQubitGate::Toffoli, c1, c2, t)
}

pub fn measure(qubit: LogicalQubitId) -> Operation {
    Operation::measure(qubit)
}

// ============================================================================
// Tests
// ============================================================================

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_single_qubit_gates() {
        let op = x(LogicalQubitId::new(0));
        assert_eq!(op.name(), "X");
        assert_eq!(op.qubits().len(), 1);
        
        let op = rz(LogicalQubitId::new(1), std::f64::consts::PI);
        assert!(op.parameters().len() == 1);
    }

    #[test]
    fn test_two_qubit_gates() {
        let op = cnot(LogicalQubitId::new(0), LogicalQubitId::new(1));
        assert_eq!(op.name(), "CNOT");
        assert_eq!(op.qubits().len(), 2);
    }

    #[test]
    fn test_three_qubit_gates() {
        let op = toffoli(
            LogicalQubitId::new(0),
            LogicalQubitId::new(1),
            LogicalQubitId::new(2)
        );
        assert_eq!(op.name(), "Toffoli");
        assert_eq!(op.qubits().len(), 3);
    }

    #[test]
    fn test_custom_operation() {
        let custom = CustomOp::new("MyGate")
            .with_qubits(vec![LogicalQubitId::new(0), LogicalQubitId::new(1)])
            .with_params(vec![1.57, 3.14])
            .with_metadata("version", "1.0");
        
        let op = Operation::Custom(custom);
        assert!(op.is_custom());
        assert_eq!(op.qubits().len(), 2);
        assert_eq!(op.parameters().len(), 2);
    }

    #[test]
    fn test_operation_qubits() {
        let q0 = LogicalQubitId::new(0);
        let q1 = LogicalQubitId::new(1);
        
        let op = cnot(q0, q1);
        let qubits = op.qubits();
        assert_eq!(qubits.len(), 2);
        assert!(qubits.contains(&q0));
        assert!(qubits.contains(&q1));
    }
}
