//! Quantum IR v0.2 DAG Demo
//! 
//! 演示如何使用 quantum-ir v0.2 的 DAG 电路结构和异步后端。

use quantum_ir::prelude::*;

fn main() {
    println!("=== Quantum IR v0.2 DAG Demo ===\n");

    // 示例 1：创建 DAG 电路
    demo_dag_circuit();

    // 示例 2：拓扑排序和深度计算
    demo_topological_sort();

    // 示例 3：并行检测
    demo_parallel_detection();

    // 示例 4：使用 Runtime 执行
    demo_runtime_execution();

    println!("\n=== Demo Complete ===");
}

/// 示例 1：创建 DAG 电路
fn demo_dag_circuit() {
    println!("1. DAG Circuit Creation");
    println!("   ---------------------");

    let mut dag = CircuitDag::with_name("Bell State DAG");
    
    let q0 = LogicalQubitId::new(0);
    let q1 = LogicalQubitId::new(1);
    
    // 添加节点
    let n1 = dag.add_node(h(q0));
    let n2 = dag.add_node(h(q1));
    let n3 = dag.add_node(cnot(q0, q1));
    
    // 添加依赖边
    dag.add_edge(n1, n3).unwrap();
    dag.add_edge(n2, n3).unwrap();
    
    println!("   Nodes: {}", dag.num_nodes());
    println!("   Qubits: {}", dag.num_qubits());
    println!("   Depth: {}", dag.depth());
    println!();
}

/// 示例 2：拓扑排序
fn demo_topological_sort() {
    println!("2. Topological Sort");
    println!("   -----------------");

    let mut dag = CircuitDag::with_name("Linear Circuit");
    
    let q0 = LogicalQubitId::new(0);
    
    let n1 = dag.add_node(h(q0));
    let n2 = dag.add_node(x(q0));
    let n3 = dag.add_node(z(q0));
    
    dag.add_edge(n1, n2).unwrap();
    dag.add_edge(n2, n3).unwrap();
    
    let order = dag.topological_sort();
    println!("   Topological order: {:?}", order);
    println!("   Depth: {}", dag.depth());
    println!();
}

/// 示例 3：并行检测
fn demo_parallel_detection() {
    println!("3. Parallel Detection");
    println!("   -------------------");

    let mut dag = CircuitDag::with_name("Parallel Circuit");
    
    let q0 = LogicalQubitId::new(0);
    let q1 = LogicalQubitId::new(1);
    let q2 = LogicalQubitId::new(2);
    
    // q0 和 q1 上的操作可以并行
    let n1 = dag.add_node(h(q0));
    let n2 = dag.add_node(x(q1));
    
    // 这个操作依赖于前两个
    let n3 = dag.add_node(cnot(q0, q1));
    dag.add_edge(n1, n3).unwrap();
    dag.add_edge(n2, n3).unwrap();
    
    // 这个操作在 q2 上，可以并行
    let n4 = dag.add_node(h(q2));
    
    println!("   Nodes: {}", dag.num_nodes());
    println!("   Can parallel (0, 1): {}", dag.can_parallel(n1, n2));
    println!("   Can parallel (0, 3): {}", dag.can_parallel(n1, n4));
    println!("   Can parallel (1, 3): {}", dag.can_parallel(n2, n4));
    
    // 计算并行组
    let groups = dag.compute_parallel_groups();
    println!("   Parallel groups: {}", groups.len());
    for (i, group) in groups.iter().enumerate() {
        println!("     Level {}: {:?}", i, group);
    }
    println!();
}

/// 示例 4：使用 Runtime 执行
fn demo_runtime_execution() {
    println!("4. Runtime Execution");
    println!("   ------------------");

    // 创建 Runtime
    let config = RuntimeConfig::new()
        .with_max_jobs(4)
        .with_default_backend("simulator");
    
    let mut runtime = QuantumRuntime::new(config);
    
    // 注册后端
    let backend = std::sync::Arc::new(IdealSimulatorBackend::new());
    runtime.register_backend("simulator", backend);
    
    // 创建电路
    let mut dag = CircuitDag::with_name("Test Circuit");
    let q0 = LogicalQubitId::new(0);
    let q1 = LogicalQubitId::new(1);
    
    dag.add_node(h(q0));
    dag.add_node(cnot(q0, q1));
    dag.add_node(measure(q0));
    dag.add_node(measure(q1));
    
    // 创建作业
    let job_id = runtime.create_job(
        dag,
        100,  // shots
        Priority::Normal,
        JobMetadata::new().with_user("demo_user"),
    );
    
    println!("   Job ID: {}", job_id);
    println!("   Status: {:?}", runtime.get_job_status(job_id));
    
    // 执行
    let results = runtime.execute_all();
    println!("   Results: {} jobs completed", results.len());
    
    if let Some(result) = results.first() {
        println!("   First result status: {:?}", result.status);
        println!("   Execution time: {:?} ms", result.execution_time_ms);
    }
    
    // 统计
    let stats = runtime.stats();
    println!("   Total submitted: {}", stats.total_jobs_submitted);
    println!("   Total completed: {}", stats.total_jobs_completed);
    println!();
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_all_demos() {
        demo_dag_circuit();
        demo_topological_sort();
        demo_parallel_detection();
        demo_runtime_execution();
    }
}
