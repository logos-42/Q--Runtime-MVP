//! Qubit 抽象模块 v0.2
//! 
//! 关键改进：逻辑 qubit 与物理 qubit 类型分离

use std::sync::atomic::{AtomicU64, Ordering};

// ============================================================================
// Type Definitions - Newtype Pattern for Type Safety
// ============================================================================

/// 逻辑 Qubit ID
/// 
/// 用户在 IR 层面使用的 qubit 标识
/// 与物理 qubit 完全解耦，支持 error correction 扩展
#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash, PartialOrd, Ord)]
pub struct LogicalQubitId(u64);

impl LogicalQubitId {
    pub fn new(id: u64) -> Self {
        Self(id)
    }
    
    pub fn value(&self) -> u64 {
        self.0
    }
}

impl From<u64> for LogicalQubitId {
    fn from(id: u64) -> Self {
        Self(id)
    }
}

impl std::fmt::Display for LogicalQubitId {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        write!(f, "LQ#{}", self.0)
    }
}

/// 物理 Qubit ID
/// 
/// 后端硬件实际使用的 qubit 标识
/// 由 Backend Adapter 负责逻辑→物理映射
#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash, PartialOrd, Ord)]
pub struct PhysicalQubitId(u64);

impl PhysicalQubitId {
    pub fn new(id: u64) -> Self {
        Self(id)
    }
    
    pub fn value(&self) -> u64 {
        self.0
    }
}

impl From<u64> for PhysicalQubitId {
    fn from(id: u64) -> Self {
        Self(id)
    }
}

impl std::fmt::Display for PhysicalQubitId {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        write!(f, "PQ#{}", self.0)
    }
}

// ============================================================================
// Qubit State
// ============================================================================

/// Qubit 状态（生命周期）
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum QubitState {
    /// 已分配，可使用
    Allocated,
    /// 已释放，不可再使用
    Freed,
    /// 已测量（经典态）
    Measured,
    /// 错误状态（用于 error correction）
    Error,
}

impl QubitState {
    pub fn is_available(&self) -> bool {
        matches!(self, QubitState::Allocated)
    }
    
    pub fn is_terminal(&self) -> bool {
        matches!(self, QubitState::Freed | QubitState::Error)
    }
}

// ============================================================================
// Logical Qubit
// ============================================================================

/// 逻辑 Qubit
/// 
/// 包含逻辑 qubit 的完整信息，支持未来 error correction 扩展
#[derive(Debug, Clone)]
pub struct LogicalQubit {
    /// 逻辑 ID
    pub id: LogicalQubitId,
    /// 当前状态
    pub state: QubitState,
    /// 关联的物理 qubit（可选，由 runtime 管理）
    pub physical_mapping: Option<PhysicalQubitId>,
    /// 是否是 ancilla qubit（用于 error correction）
    pub is_ancilla: bool,
}

impl LogicalQubit {
    pub fn new(id: LogicalQubitId) -> Self {
        Self {
            id,
            state: QubitState::Allocated,
            physical_mapping: None,
            is_ancilla: false,
        }
    }
    
    pub fn with_ancilla(id: LogicalQubitId) -> Self {
        Self {
            id,
            state: QubitState::Allocated,
            physical_mapping: None,
            is_ancilla: true,
        }
    }
    
    pub fn free(&mut self) {
        self.state = QubitState::Freed;
        self.physical_mapping = None;
    }
    
    pub fn map_to_physical(&mut self, physical_id: PhysicalQubitId) {
        self.physical_mapping = Some(physical_id);
    }
    
    pub fn unmap(&mut self) {
        self.physical_mapping = None;
    }
}

// ============================================================================
// Qubit Manager
// ============================================================================

/// 逻辑 Qubit 管理器
/// 
/// 负责逻辑 qubit 的分配、回收和映射管理
#[derive(Debug)]
pub struct LogicalQubitManager {
    /// 下一个可用的逻辑 qubit ID
    next_id: AtomicU64,
    /// 已分配的 qubit
    allocated: std::collections::HashMap<LogicalQubitId, LogicalQubit>,
    /// 空闲的 qubit ID（用于回收重用）
    free_list: Vec<LogicalQubitId>,
}

impl LogicalQubitManager {
    pub fn new() -> Self {
        Self {
            next_id: AtomicU64::new(0),
            allocated: std::collections::HashMap::new(),
            free_list: Vec::new(),
        }
    }
    
    /// 分配一个新的逻辑 qubit
    pub fn allocate(&mut self) -> LogicalQubitId {
        // 优先使用回收的 ID
        if let Some(id) = self.free_list.pop() {
            if let Some(qubit) = self.allocated.get_mut(&id) {
                qubit.state = QubitState::Allocated;
                qubit.physical_mapping = None;
            }
            return id;
        }
        
        // 分配新 ID
        let id = LogicalQubitId::new(self.next_id.fetch_add(1, Ordering::SeqCst));
        let qubit = LogicalQubit::new(id);
        self.allocated.insert(id, qubit);
        id
    }
    
    /// 分配一个 ancilla qubit（用于 error correction）
    pub fn allocate_ancilla(&mut self) -> LogicalQubitId {
        let id = LogicalQubitId::new(self.next_id.fetch_add(1, Ordering::SeqCst));
        let qubit = LogicalQubit::with_ancilla(id);
        self.allocated.insert(id, qubit);
        id
    }
    
    /// 释放逻辑 qubit
    pub fn free(&mut self, id: LogicalQubitId) -> bool {
        if let Some(qubit) = self.allocated.get_mut(&id) {
            qubit.free();
            self.free_list.push(id);
            return true;
        }
        false
    }
    
    /// 获取 qubit 引用
    pub fn get(&self, id: LogicalQubitId) -> Option<&LogicalQubit> {
        self.allocated.get(&id)
    }
    
    /// 获取 qubit 可变引用
    pub fn get_mut(&mut self, id: LogicalQubitId) -> Option<&mut LogicalQubit> {
        self.allocated.get_mut(&id)
    }
    
    /// 检查 qubit 是否存在
    pub fn contains(&self, id: LogicalQubitId) -> bool {
        self.allocated.contains_key(&id)
    }
    
    /// 获取所有已分配的 qubit
    pub fn all_qubits(&self) -> Vec<LogicalQubitId> {
        self.allocated.keys().copied().collect()
    }
    
    /// 获取活跃 qubit 数量
    pub fn active_count(&self) -> usize {
        self.allocated
            .values()
            .filter(|q| q.state.is_available())
            .count()
    }
    
    /// 重置管理器
    pub fn reset(&mut self) {
        self.next_id.store(0, Ordering::SeqCst);
        self.allocated.clear();
        self.free_list.clear();
    }
}

impl Default for LogicalQubitManager {
    fn default() -> Self {
        Self::new()
    }
}

// ============================================================================
// Qubit Mapping (Logical -> Physical)
// ============================================================================

/// Qubit 映射表
/// 
/// 负责逻辑 qubit 到物理 qubit 的映射管理
#[derive(Debug, Clone, Default)]
pub struct QubitMapping {
    logical_to_physical: std::collections::HashMap<LogicalQubitId, PhysicalQubitId>,
    physical_to_logical: std::collections::HashMap<PhysicalQubitId, LogicalQubitId>,
}

impl QubitMapping {
    pub fn new() -> Self {
        Self::default()
    }
    
    /// 建立映射
    pub fn map(&mut self, logical: LogicalQubitId, physical: PhysicalQubitId) -> Option<PhysicalQubitId> {
        let old = self.logical_to_physical.insert(logical, physical);
        if let Some(old_physical) = old {
            self.physical_to_logical.remove(&old_physical);
        }
        self.physical_to_logical.insert(physical, logical);
        old
    }
    
    /// 移除映射
    pub fn unmap(&mut self, logical: LogicalQubitId) -> Option<PhysicalQubitId> {
        if let Some(physical) = self.logical_to_physical.remove(&logical) {
            self.physical_to_logical.remove(&physical);
            return Some(physical);
        }
        None
    }
    
    /// 获取物理 qubit ID
    pub fn get_physical(&self, logical: LogicalQubitId) -> Option<PhysicalQubitId> {
        self.logical_to_physical.get(&logical).copied()
    }
    
    /// 获取逻辑 qubit ID
    pub fn get_logical(&self, physical: PhysicalQubitId) -> Option<LogicalQubitId> {
        self.physical_to_logical.get(&physical).copied()
    }
    
    /// 检查是否已映射
    pub fn is_mapped(&self, logical: LogicalQubitId) -> bool {
        self.logical_to_physical.contains_key(&logical)
    }
    
    /// 获取所有映射
    pub fn all_mappings(&self) -> Vec<(LogicalQubitId, PhysicalQubitId)> {
        self.logical_to_physical
            .iter()
            .map(|(&l, &p)| (l, p))
            .collect()
    }
    
    /// 清除所有映射
    pub fn clear(&mut self) {
        self.logical_to_physical.clear();
        self.physical_to_logical.clear();
    }
}

// ============================================================================
// Tests
// ============================================================================

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_logical_qubit_creation() {
        let qubit = LogicalQubit::new(LogicalQubitId::new(0));
        assert_eq!(qubit.id.value(), 0);
        assert!(qubit.state.is_available());
        assert!(!qubit.is_ancilla);
    }

    #[test]
    fn test_ancilla_qubit() {
        let qubit = LogicalQubit::with_ancilla(LogicalQubitId::new(0));
        assert!(qubit.is_ancilla);
    }

    #[test]
    fn test_qubit_manager_allocation() {
        let mut manager = LogicalQubitManager::new();
        
        let q1 = manager.allocate();
        let q2 = manager.allocate();
        
        assert_eq!(q1.value(), 0);
        assert_eq!(q2.value(), 1);
        assert!(manager.contains(q1));
        assert!(manager.contains(q2));
    }

    #[test]
    fn test_qubit_manager_free_and_reuse() {
        let mut manager = LogicalQubitManager::new();
        
        let q1 = manager.allocate();
        assert!(manager.free(q1));
        
        let q2 = manager.allocate();
        // 应该重用 q1 的 ID
        assert_eq!(q2.value(), q1.value());
    }

    #[test]
    fn test_qubit_mapping() {
        let mut mapping = QubitMapping::new();
        
        let logical = LogicalQubitId::new(0);
        let physical = PhysicalQubitId::new(5);
        
        mapping.map(logical, physical);
        
        assert_eq!(mapping.get_physical(logical), Some(physical));
        assert_eq!(mapping.get_logical(physical), Some(logical));
        assert!(mapping.is_mapped(logical));
    }

    #[test]
    fn test_type_safety() {
        // 编译时类型检查：LogicalQubitId != PhysicalQubitId
        let logical = LogicalQubitId::new(0);
        let physical = PhysicalQubitId::new(0);
        
        // 以下代码无法编译，确保类型安全：
        // let _: LogicalQubitId = physical;  // 编译错误
        // let _: PhysicalQubitId = logical;  // 编译错误
        
        // 必须显式转换
        assert_eq!(logical.value(), physical.value());
    }
}
