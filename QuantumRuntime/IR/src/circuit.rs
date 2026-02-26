//! Circuit DAG 模块 v0.2
//! 
//! 核心改进：从线性 Vec<Operation> 演进为 DAG 结构
//! 支持并行操作检测和拓扑优化

use crate::qubit::LogicalQubitId;
use crate::operation::Operation;
use crate::{Result, IrError};

// ============================================================================
// Operation Node
// ============================================================================

/// DAG 中的操作节点
/// 
/// 每个节点包含操作本身及其依赖关系
#[derive(Debug, Clone)]
pub struct OperationNode {
    /// 节点唯一 ID（在 DAG 中的索引）
    pub id: usize,
    /// 量子操作
    pub op: Operation,
    /// 前置节点 ID 列表（依赖的节点）
    pub depends_on: Vec<usize>,
    /// 可并行执行的节点 ID 列表（由 runtime 计算）
    pub parallel_with: Vec<usize>,
    /// 操作涉及的 qubit
    pub qubits: Vec<LogicalQubitId>,
}

impl OperationNode {
    pub fn new(id: usize, op: Operation) -> Self {
        let qubits = op.qubits();
        Self {
            id,
            op,
            depends_on: Vec::new(),
            parallel_with: Vec::new(),
            qubits,
        }
    }
    
    pub fn with_dependency(mut self, node_id: usize) -> Self {
        self.depends_on.push(node_id);
        self
    }
    
    pub fn with_dependencies(mut self, node_ids: Vec<usize>) -> Self {
        self.depends_on = node_ids;
        self
    }
    
    /// 检查是否依赖于另一个节点
    pub fn depends_on_node(&self, other_id: usize) -> bool {
        self.depends_on.contains(&other_id)
    }
    
    /// 检查是否可以与另一个节点并行执行
    pub fn can_parallel_with(&self, other_id: usize) -> bool {
        self.parallel_with.contains(&other_id)
    }
    
    /// 获取操作的深度（qubit 数量）
    pub fn gate_size(&self) -> usize {
        self.qubits.len()
    }
}

// ============================================================================
// Circuit DAG
// ============================================================================

/// 量子电路的 DAG 表示
/// 
/// 使用邻接表存储，支持高效的依赖查询和拓扑排序
#[derive(Debug, Clone)]
pub struct CircuitDag {
    /// 所有操作节点
    nodes: Vec<OperationNode>,
    /// 边列表：(from, to) 表示 from 必须在 to 之前执行
    edges: Vec<(usize, usize)>,
    /// 电路输入 qubit
    inputs: Vec<LogicalQubitId>,
    /// 电路输出 qubit（测量后的经典结果）
    outputs: Vec<LogicalQubitId>,
    /// 电路元数据
    metadata: CircuitMetadata,
    /// 缓存的深度值
    cached_depth: Option<usize>,
}

/// 电路元数据
#[derive(Debug, Clone, Default)]
pub struct CircuitMetadata {
    pub name: Option<String>,
    pub description: Option<String>,
    pub tags: Vec<String>,
    pub created_at: Option<u64>,
}

impl CircuitDag {
    /// 创建空 DAG
    pub fn new() -> Self {
        Self {
            nodes: Vec::new(),
            edges: Vec::new(),
            inputs: Vec::new(),
            outputs: Vec::new(),
            metadata: CircuitMetadata::default(),
            cached_depth: None,
        }
    }
    
    /// 创建带名称的 DAG
    pub fn with_name(name: impl Into<String>) -> Self {
        let mut dag = Self::new();
        dag.metadata.name = Some(name.into());
        dag
    }
    
    /// 设置输入 qubit
    pub fn with_inputs(mut self, inputs: Vec<LogicalQubitId>) -> Self {
        self.inputs = inputs;
        self
    }
    
    /// 添加操作节点
    pub fn add_node(&mut self, op: Operation) -> usize {
        let id = self.nodes.len();
        let node = OperationNode::new(id, op);
        self.nodes.push(node);
        self.cached_depth = None;  // 清除缓存
        id
    }
    
    /// 添加操作节点并指定依赖
    pub fn add_node_with_deps(&mut self, op: Operation, depends_on: Vec<usize>) -> Result<usize> {
        // 验证依赖节点存在
        for &dep_id in &depends_on {
            if dep_id >= self.nodes.len() {
                return Err(IrError::QubitNotFound(format!(
                    "Dependency node {} does not exist", dep_id
                )));
            }
        }
        
        let id = self.nodes.len();
        let node = OperationNode::new(id, op).with_dependencies(depends_on.clone());
        self.nodes.push(node);
        
        // 添加边
        for &dep_id in &depends_on {
            self.edges.push((dep_id, id));
        }
        
        self.cached_depth = None;
        Ok(id)
    }
    
    /// 添加边（依赖关系）
    pub fn add_edge(&mut self, from: usize, to: usize) -> Result<()> {
        if from >= self.nodes.len() || to >= self.nodes.len() {
            return Err(IrError::QubitNotFound(format!(
                "Node index out of bounds: from={}, to={}", from, to
            )));
        }
        
        // 检查是否会产生环
        if self.would_create_cycle(from, to) {
            return Err(IrError::CyclicDependency(format!(
                "Adding edge {} -> {} would create a cycle", from, to
            )));
        }
        
        self.edges.push((from, to));
        self.nodes[to].depends_on.push(from);
        self.cached_depth = None;
        Ok(())
    }
    
    /// 检查添加边是否会创建环（DFS）
    fn would_create_cycle(&self, from: usize, to: usize) -> bool {
        // 如果从 to 能到达 from，则会形成环
        let mut visited = vec![false; self.nodes.len()];
        self.has_path(to, from, &mut visited)
    }
    
    /// 检查是否存在从 start 到 end 的路径
    fn has_path(&self, start: usize, end: usize, visited: &mut [bool]) -> bool {
        if start == end {
            return true;
        }
        
        if visited[start] {
            return false;
        }
        
        visited[start] = true;
        
        // 查找所有从 start 出发的边
        for &(from, to) in &self.edges {
            if from == start {
                if self.has_path(to, end, visited) {
                    return true;
                }
            }
        }
        
        false
    }
    
    /// 获取节点引用
    pub fn get_node(&self, id: usize) -> Option<&OperationNode> {
        self.nodes.get(id)
    }
    
    /// 获取节点可变引用
    pub fn get_node_mut(&mut self, id: usize) -> Option<&mut OperationNode> {
        self.cached_depth = None;
        self.nodes.get_mut(id)
    }
    
    /// 获取所有节点
    pub fn nodes(&self) -> &[OperationNode] {
        &self.nodes
    }
    
    /// 获取所有边
    pub fn edges(&self) -> &[(usize, usize)] {
        &self.edges
    }
    
    /// 获取节点数量
    pub fn num_nodes(&self) -> usize {
        self.nodes.len()
    }
    
    /// 获取 qubit 数量
    pub fn num_qubits(&self) -> usize {
        let mut qubits = std::collections::HashSet::new();
        for node in &self.nodes {
            for &q in &node.qubits {
                qubits.insert(q);
            }
        }
        qubits.len()
    }
    
    /// 获取操作数量
    pub fn num_operations(&self) -> usize {
        self.nodes.len()
    }
    
    /// 计算电路深度（关键路径长度）
    pub fn depth(&mut self) -> usize {
        if let Some(d) = self.cached_depth {
            return d;
        }
        
        if self.nodes.is_empty() {
            self.cached_depth = Some(0);
            return 0;
        }
        
        // 使用动态规划计算最长路径
        let order = self.topological_sort();
        let mut depths = vec![0usize; self.nodes.len()];
        
        for &node_id in &order {
            let node = &self.nodes[node_id];
            let max_pred_depth = node.depends_on
                .iter()
                .map(|&pred| depths[pred])
                .max()
                .unwrap_or(0);
            depths[node_id] = max_pred_depth + 1;
        }
        
        let max_depth = *depths.iter().max().unwrap_or(&0);
        self.cached_depth = Some(max_depth);
        max_depth
    }
    
    /// 拓扑排序
    pub fn topological_sort(&self) -> Vec<usize> {
        let mut result = Vec::with_capacity(self.nodes.len());
        let mut visited = vec![false; self.nodes.len()];
        let mut in_degree = vec![0usize; self.nodes.len()];
        
        // 计算入度
        for &(from, to) in &self.edges {
            in_degree[to] += 1;
        }
        
        // Kahn 算法
        let mut queue: Vec<usize> = in_degree
            .iter()
            .enumerate()
            .filter(|(_, &deg)| deg == 0)
            .map(|(i, _)| i)
            .collect();
        
        while let Some(node) = queue.pop() {
            if visited[node] {
                continue;
            }
            visited[node] = true;
            result.push(node);
            
            // 减少后继节点的入度
            for &(from, to) in &self.edges {
                if from == node {
                    in_degree[to] -= 1;
                    if in_degree[to] == 0 {
                        queue.push(to);
                    }
                }
            }
        }
        
        result
    }
    
    /// 计算可并行执行的节点组
    pub fn compute_parallel_groups(&mut self) -> Vec<Vec<usize>> {
        let order = self.topological_sort();
        let mut groups: Vec<Vec<usize>> = Vec::new();
        let mut node_level = vec![0usize; self.nodes.len()];
        
        for &node_id in &order {
            let node = &self.nodes[node_id];
            let level = node.depends_on
                .iter()
                .map(|&pred| node_level[pred])
                .max()
                .unwrap_or(0);
            
            node_level[node_id] = level;
            
            while groups.len() <= level {
                groups.push(Vec::new());
            }
            groups[level].push(node_id);
        }
        
        // 更新节点的 parallel_with 信息
        for (level, group) in groups.iter().enumerate() {
            for &node_id in group {
                if let Some(node) = self.nodes.get_mut(node_id) {
                    node.parallel_with = group
                        .iter()
                        .copied()
                        .filter(|&id| id != node_id)
                        .collect();
                }
            }
        }
        
        self.cached_depth = Some(groups.len());
        groups
    }
    
    /// 检查两个节点是否可以并行执行
    pub fn can_parallel(&self, node1: usize, node2: usize) -> bool {
        if node1 >= self.nodes.len() || node2 >= self.nodes.len() {
            return false;
        }
        
        // 检查是否有依赖关系
        if self.nodes[node1].depends_on_node(node2) 
            || self.nodes[node2].depends_on_node(node1) {
            return false;
        }
        
        // 检查是否共享 qubit
        let qubits1: std::collections::HashSet<_> = 
            self.nodes[node1].qubits.iter().collect();
        let qubits2: std::collections::HashSet<_> = 
            self.nodes[node2].qubits.iter().collect();
        
        qubits1.is_disjoint(&qubits2)
    }
    
    /// 获取电路使用的 qubit 列表
    pub fn all_qubits(&self) -> Vec<LogicalQubitId> {
        let mut qubits = std::collections::HashSet::new();
        for node in &self.nodes {
            for &q in &node.qubits {
                qubits.insert(q);
            }
        }
        qubits.into_iter().collect()
    }
    
    /// 获取测量操作节点
    pub fn measurement_nodes(&self) -> Vec<&OperationNode> {
        self.nodes
            .iter()
            .filter(|n| n.op.is_measurement())
            .collect()
    }
    
    /// 清除所有节点
    pub fn clear(&mut self) {
        self.nodes.clear();
        self.edges.clear();
        self.cached_depth = None;
    }
    
    /// 获取元数据
    pub fn metadata(&self) -> &CircuitMetadata {
        &self.metadata
    }
    
    /// 设置元数据
    pub fn set_metadata(&mut self, metadata: CircuitMetadata) {
        self.metadata = metadata;
    }
}

impl Default for CircuitDag {
    fn default() -> Self {
        Self::new()
    }
}

// ============================================================================
// Circuit Builder (DAG version)
// ============================================================================

/// DAG 电路构建器
/// 
/// 提供流畅的 API 用于构建 DAG 电路
#[derive(Debug)]
pub struct CircuitDagBuilder {
    dag: CircuitDag,
    last_ops: Vec<usize>,  // 每个 qubit 最后添加的操作
}

impl CircuitDagBuilder {
    pub fn new() -> Self {
        Self {
            dag: CircuitDag::new(),
            last_ops: Vec::new(),
        }
    }
    
    pub fn with_name(name: impl Into<String>) -> Self {
        Self {
            dag: CircuitDag::with_name(name),
            last_ops: Vec::new(),
        }
    }
    
    /// 添加 qubit 到输入
    pub fn add_input(&mut self, qubit: LogicalQubitId) -> &mut Self {
        self.dag.inputs.push(qubit);
        self
    }
    
    /// 添加操作（自动处理依赖）
    pub fn add_op(&mut self, op: Operation) -> usize {
        let qubits = op.qubits();
        
        // 找出所有相关 qubit 的最后操作
        let mut deps = Vec::new();
        for q in &qubits {
            let qidx = q.value() as usize;
            if qidx < self.last_ops.len() {
                if let Some(&last_op) = self.last_ops.get(qidx) {
                    deps.push(last_op);
                }
            }
        }
        
        // 去重
        deps.sort();
        deps.dedup();
        
        let node_id = self.dag.add_node(op);
        
        // 添加依赖边
        for &dep in &deps {
            let _ = self.dag.add_edge(dep, node_id);
        }
        
        // 更新每个 qubit 的最后操作
        for q in &qubits {
            let qidx = q.value() as usize;
            while self.last_ops.len() <= qidx {
                self.last_ops.push(usize::MAX);
            }
            self.last_ops[qidx] = node_id;
        }
        
        node_id
    }
    
    /// 构建 DAG
    pub fn build(self) -> CircuitDag {
        self.dag
    }
}

impl Default for CircuitDagBuilder {
    fn default() -> Self {
        Self::new()
    }
}

// ============================================================================
// Predefined Circuit Patterns
// ============================================================================

/// 创建 Bell 态电路（DAG 版本）
pub fn bell_state_dag() -> CircuitDag {
    let mut builder = CircuitDagBuilder::with_name("Bell State");
    
    let q0 = LogicalQubitId::new(0);
    let q1 = LogicalQubitId::new(1);
    
    builder.add_input(q0);
    builder.add_input(q1);
    
    use crate::operation::{h, cnot, measure};
    
    builder.add_op(h(q0));
    builder.add_op(cnot(q0, q1));
    builder.add_op(measure(q0));
    builder.add_op(measure(q1));
    
    builder.build()
}

/// 创建 GHZ 态电路（DAG 版本）
pub fn ghz_dag(n: usize) -> CircuitDag {
    if n < 2 {
        panic!("GHZ circuit requires at least 2 qubits");
    }
    
    let mut builder = CircuitDagBuilder::with_name(format!("GHZ-{}", n));
    
    use crate::operation::{h, cnot, measure};
    
    // 添加输入
    for i in 0..n {
        builder.add_input(LogicalQubitId::new(i as u64));
    }
    
    // H 门在第一个 qubit
    builder.add_op(h(LogicalQubitId::new(0)));
    
    // 级联 CNOT
    for i in 0..n - 1 {
        builder.add_op(cnot(
            LogicalQubitId::new(i as u64),
            LogicalQubitId::new((i + 1) as u64)
        ));
    }
    
    // 测量所有
    for i in 0..n {
        builder.add_op(measure(LogicalQubitId::new(i as u64)));
    }
    
    builder.build()
}

// ============================================================================
// Tests
// ============================================================================

#[cfg(test)]
mod tests {
    use super::*;
    use crate::operation::{h, cnot, x, z};

    #[test]
    fn test_dag_creation() {
        let mut dag = CircuitDag::with_name("Test");
        let q0 = LogicalQubitId::new(0);
        
        dag.add_node(h(q0));
        dag.add_node(x(q0));
        
        assert_eq!(dag.num_nodes(), 2);
        assert_eq!(dag.num_qubits(), 1);
    }

    #[test]
    fn test_dag_dependency() {
        let mut dag = CircuitDag::new();
        let q0 = LogicalQubitId::new(0);
        
        let n1 = dag.add_node(h(q0));
        let n2 = dag.add_node(x(q0));
        dag.add_edge(n1, n2).unwrap();
        
        assert!(dag.get_node(n2).unwrap().depends_on_node(n1));
    }

    #[test]
    fn test_topological_sort() {
        let mut dag = CircuitDag::new();
        let q0 = LogicalQubitId::new(0);
        
        let n1 = dag.add_node(h(q0));
        let n2 = dag.add_node(x(q0));
        let n3 = dag.add_node(z(q0));
        
        dag.add_edge(n1, n2).unwrap();
        dag.add_edge(n2, n3).unwrap();
        
        let order = dag.topological_sort();
        assert_eq!(order, vec![n1, n2, n3]);
    }

    #[test]
    fn test_depth_calculation() {
        let mut dag = CircuitDag::new();
        let q0 = LogicalQubitId::new(0);
        let q1 = LogicalQubitId::new(1);
        
        // 并行操作
        let n1 = dag.add_node(h(q0));
        let n2 = dag.add_node(h(q1));
        
        // 依赖 n1 和 n2
        let n3 = dag.add_node(cnot(q0, q1));
        dag.add_edge(n1, n3).unwrap();
        dag.add_edge(n2, n3).unwrap();
        
        assert_eq!(dag.depth(), 2);
    }

    #[test]
    fn test_parallel_detection() {
        let mut dag = CircuitDag::new();
        let q0 = LogicalQubitId::new(0);
        let q1 = LogicalQubitId::new(1);
        
        dag.add_node(h(q0));
        dag.add_node(h(q1));
        
        assert!(dag.can_parallel(0, 1));
    }

    #[test]
    fn test_bell_dag() {
        let dag = bell_state_dag();
        assert_eq!(dag.num_qubits(), 2);
        assert_eq!(dag.num_operations(), 4);
    }

    #[test]
    fn test_cyclic_dependency_detection() {
        let mut dag = CircuitDag::new();
        let q0 = LogicalQubitId::new(0);
        
        let n1 = dag.add_node(h(q0));
        let n2 = dag.add_node(x(q0));
        
        dag.add_edge(n1, n2).unwrap();
        
        // 尝试创建环
        let result = dag.add_edge(n2, n1);
        assert!(result.is_err());
    }

    #[test]
    fn test_parallel_groups() {
        let mut dag = CircuitDag::new();
        let q0 = LogicalQubitId::new(0);

        // 线性电路：h -> x -> z
        let n1 = dag.add_node(h(q0));
        let n2 = dag.add_node(x(q0));
        let n3 = dag.add_node(z(q0));
        
        dag.add_edge(n1, n2).unwrap();
        dag.add_edge(n2, n3).unwrap();

        let groups = dag.compute_parallel_groups();
        // 验证至少返回了一组
        assert!(!groups.is_empty());
        // 验证所有节点都在组中
        let total_nodes: usize = groups.iter().map(|g| g.len()).sum();
        assert_eq!(total_nodes, 3);
    }
}
