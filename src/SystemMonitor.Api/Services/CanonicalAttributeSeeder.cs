using SystemMonitor.Shared.Models;

namespace SystemMonitor.Api.Services;

public static class CanonicalAttributeSeeder
{
    public static IReadOnlyList<CanonicalAttribute> GetSeedAttributes() =>
    [
        //CPU
        Attr("cpu_percent", "CPU Utilisation %", "System", "%",
            "Percentage of CPU time in use",
            "CPU %", "cpu usage", "processor_usage", "processorUsage", "ProcessorTime",
            "cpu_utilization", "cpu utilization", "CPU Utilization (%)",
            "Processor(_Total)\\% Processor Time", "system.cpu.percent",
            "node_cpu_seconds_total", "cpu.percent", "CPUUsage", "cpu_load"),

        Attr("cpu_user_percent", "CPU User %", "System", "%",
            "CPU time spent in user space",
            "cpu user", "user cpu", "cpu_user", "UserTime", "user_time"),

        Attr("cpu_system_percent", "CPU System %", "System", "%",
            "CPU time spent in kernel/system space",
            "cpu system", "system cpu", "cpu_system", "SystemTime", "kernel_time"),

        Attr("cpu_idle_percent", "CPU Idle %", "System", "%",
            "CPU idle time percentage",
            "cpu idle", "idle cpu", "cpu_idle", "IdleTime"),

        Attr("cpu_iowait_percent", "CPU I/O Wait %", "System", "%",
            "CPU time waiting for I/O operations",
            "cpu iowait", "iowait", "io_wait", "IOWait", "cpu_io_wait"),

        Attr("cpu_count", "CPU Core Count", "System", "cores",
            "Number of logical CPU cores",
            "processor count", "num_cpus", "numCPUs", "cpu cores", "logical processors"),

        Attr("load_avg_1m", "Load Average 1 min", "System", "",
            "1-minute load average",
            "load average", "loadavg1", "load1", "system_load_1", "load.1min"),

        Attr("load_avg_5m", "Load Average 5 min", "System", "",
            "5-minute load average",
            "load average 5", "loadavg5", "load5", "system_load_5"),

        Attr("load_avg_15m", "Load Average 15 min", "System", "",
            "15-minute load average",
            "load average 15", "loadavg15", "load15", "system_load_15"),

        //Memory
        Attr("memory_percent", "Memory Utilisation %", "Memory", "%",
            "Percentage of physical RAM in use",
            "Memory %", "mem usage", "memory_usage", "memoryUsage", "MemoryUsage",
            "ram_percent", "RAM %", "mem%", "memory utilization",
            "Available MBytes", "memory.percent", "node_memory_MemAvailable_bytes"),

        Attr("memory_total_mb", "Total RAM (MB)", "Memory", "MB",
            "Total physical RAM installed",
            "total memory", "mem total", "TotalPhysicalMemory", "total_ram",
            "memory_total", "MemTotal", "RAM Total"),

        Attr("memory_used_mb", "Used RAM (MB)", "Memory", "MB",
            "Physical RAM currently in use",
            "used memory", "mem used", "UsedMemory", "used_ram",
            "memory_used", "MemUsed"),

        Attr("memory_free_mb", "Free RAM (MB)", "Memory", "MB",
            "Physical RAM currently free",
            "free memory", "mem free", "FreeMemory", "free_ram",
            "MemFree", "MemAvailable"),

        Attr("memory_cached_mb", "Cached RAM (MB)", "Memory", "MB",
            "RAM used for disk cache",
            "cached memory", "mem cached", "Cached", "disk cache"),

        Attr("memory_swap_percent", "Swap Utilisation %", "Memory", "%",
            "Percentage of swap space in use",
            "swap usage", "swap percent", "swap_usage", "SwapUsage",
            "page file usage", "PageFile %", "paging file"),

        //Disk
        Attr("disk_percent", "Disk Utilisation %", "Disk", "%",
            "Percentage of disk space used on primary volume",
            "disk usage", "disk space %", "disk_usage", "DiskUsage",
            "disk utilization", "storage percent", "volume usage",
            "LogicalDisk(_Total)\\% Free Space"),

        Attr("disk_total_gb", "Total Disk Space (GB)", "Disk", "GB",
            "Total disk capacity",
            "disk total", "total disk", "DiskTotal", "storage total",
            "volume size", "disk_size"),

        Attr("disk_free_gb", "Free Disk Space (GB)", "Disk", "GB",
            "Free disk space",
            "disk free", "free disk", "DiskFree", "available disk",
            "free space", "FreeSpace"),

        Attr("disk_read_mbps", "Disk Read (Mbps)", "Disk", "Mbps",
            "Disk read throughput",
            "disk read", "read throughput", "DiskReadBytes", "disk_read_rate",
            "io_read", "IOReadBytes", "ReadBytesPerSec",
            "PhysicalDisk(_Total)\\Disk Read Bytes/sec"),

        Attr("disk_write_mbps", "Disk Write (Mbps)", "Disk", "Mbps",
            "Disk write throughput",
            "disk write", "write throughput", "DiskWriteBytes", "disk_write_rate",
            "io_write", "IOWriteBytes", "WriteBytesPerSec",
            "PhysicalDisk(_Total)\\Disk Write Bytes/sec"),

        Attr("disk_read_iops", "Disk Read IOPS", "Disk", "IOPS",
            "Disk read operations per second",
            "read iops", "disk reads/s", "ReadOpsPerSec", "read_ops"),

        Attr("disk_write_iops", "Disk Write IOPS", "Disk", "IOPS",
            "Disk write operations per second",
            "write iops", "disk writes/s", "WriteOpsPerSec", "write_ops"),

        Attr("disk_queue_depth", "Disk Queue Depth", "Disk", "",
            "Average I/O requests waiting in disk queue",
            "disk queue", "io queue", "DiskQueueLength", "queue depth",
            "PhysicalDisk(_Total)\\Avg. Disk Queue Length"),

        Attr("disk_latency_ms", "Disk Latency (ms)", "Disk", "ms",
            "Average disk I/O latency",
            "disk latency", "io latency", "DiskLatency", "disk_response_time"),

        //Network
        Attr("network_in_mbps", "Network In (Mbps)", "Network", "Mbps",
            "Inbound network throughput",
            "bytes received", "network received", "net_in", "netIn",
            "NetworkBytesReceivedPerSec", "rx_bytes", "inbound_bandwidth",
            "Network Interface(_Total)\\Bytes Received/sec",
            "ifInOctets", "if_in_octets", "network.bytes_rcvd"),

        Attr("network_out_mbps", "Network Out (Mbps)", "Network", "Mbps",
            "Outbound network throughput",
            "bytes sent", "network sent", "net_out", "netOut",
            "NetworkBytesSentPerSec", "tx_bytes", "outbound_bandwidth",
            "Network Interface(_Total)\\Bytes Sent/sec",
            "ifOutOctets", "if_out_octets", "network.bytes_sent"),

        Attr("network_packets_in", "Packets In (pps)", "Network", "pps",
            "Inbound network packets per second",
            "packets received", "packets in", "rx_packets", "inbound_packets",
            "ifInUcastPkts", "network.packets_rcvd"),

        Attr("network_packets_out", "Packets Out (pps)", "Network", "pps",
            "Outbound network packets per second",
            "packets sent", "packets out", "tx_packets", "outbound_packets",
            "ifOutUcastPkts", "network.packets_sent"),

        Attr("network_errors_in", "Network Errors In", "Network", "errors/s",
            "Inbound network error rate",
            "rx errors", "inbound errors", "ifInErrors", "network_rx_errors"),

        Attr("network_errors_out", "Network Errors Out", "Network", "errors/s",
            "Outbound network error rate",
            "tx errors", "outbound errors", "ifOutErrors", "network_tx_errors"),

        Attr("network_drops_in", "Dropped Packets In", "Network", "drops/s",
            "Inbound dropped packets",
            "rx drops", "dropped in", "ifInDiscards", "network_rx_drops"),

        Attr("network_connections", "Active TCP Connections", "Network", "",
            "Number of established TCP connections",
            "tcp connections", "active connections", "TCPConnections",
            "net.tcp.established", "tcp_established"),

        //Application metrics
        Attr("requests_per_second", "Requests Per Second", "Application", "req/s",
            "HTTP/application request rate",
            "rps", "req/s", "requests/sec", "RequestsPerSecond",
            "http_requests_total", "throughput", "TPS", "transactions_per_second",
            "request rate", "RequestRate", "web_requests"),

        Attr("error_rate", "Error Rate", "Application", "errors/s",
            "Application error rate (0–1 or count/s depending on source)",
            "errors/s", "error_count", "ErrorRate", "error rate",
            "http_errors", "exception_rate", "ExceptionRate",
            "failed_requests", "FailedRequests"),

        Attr("p50_latency_ms", "P50 Latency (ms)", "Application", "ms",
            "50th percentile (median) request latency",
            "p50", "median latency", "latency p50", "latency_p50"),

        Attr("p95_latency_ms", "P95 Latency (ms)", "Application", "ms",
            "95th percentile request latency",
            "p95", "95th percentile latency", "latency p95", "latency_p95",
            "response_time_p95"),

        Attr("p99_latency_ms", "P99 Latency (ms)", "Application", "ms",
            "99th percentile request latency",
            "p99", "99th percentile latency", "latency_p99", "latency p99",
            "response_time_p99", "P99", "slow_requests", "ResponseTimeP99"),

        Attr("avg_response_ms", "Average Response Time (ms)", "Application", "ms",
            "Mean HTTP/application response time",
            "response time", "avg latency", "average response", "mean latency",
            "ResponseTime", "avgResponseTime", "avg_latency",
            "response_time_avg", "AverageResponseTime"),

        Attr("active_sessions", "Active Sessions", "Application", "",
            "Number of active user/application sessions",
            "sessions", "active users", "concurrent users", "ActiveSessions",
            "concurrent_sessions", "user_sessions"),

        Attr("queue_depth", "Queue Depth", "Application", "",
            "Number of items waiting in application queue",
            "queue size", "queue length", "QueueDepth", "QueueLength",
            "pending_tasks", "backlog", "message_backlog"),

        //Database
        Attr("db_connections_active", "DB Active Connections", "Database", "",
            "Active database connections",
            "active connections", "db_connections", "DatabaseConnections",
            "ActiveConnections", "open_connections", "db_active_sessions"),

        Attr("db_connections_max", "DB Max Connections", "Database", "",
            "Maximum allowed database connections",
            "max connections", "db_max_connections", "MaxConnections"),

        Attr("db_queries_per_second", "DB Queries Per Second", "Database", "qps",
            "Database query execution rate",
            "queries/s", "qps", "QueriesPerSecond", "db_qps",
            "queries per second", "sql_queries"),

        Attr("db_query_avg_ms", "DB Average Query Time (ms)", "Database", "ms",
            "Average time to execute a database query",
            "query time", "avg query time", "QueryTime", "db_latency",
            "sql_latency", "QueryResponseTime"),

        Attr("db_cache_hit_ratio", "DB Cache Hit Ratio", "Database", "%",
            "Percentage of queries served from cache",
            "cache hit ratio", "buffer hit", "CacheHitRatio",
            "buffer_cache_hit_ratio", "db_cache_hits"),

        Attr("db_deadlocks", "DB Deadlocks", "Database", "count",
            "Number of database deadlocks",
            "deadlocks", "DeadlocksPerSec", "sql_deadlocks", "lock_timeouts"),

        //Process
        Attr("process_count", "Process Count", "System", "",
            "Number of running processes",
            "processes", "num_processes", "ProcessCount", "running_processes"),

        Attr("thread_count", "Thread Count", "System", "",
            "Number of active threads",
            "threads", "num_threads", "ThreadCount", "process_threads",
            "active_threads"),

        Attr("handle_count", "Handle Count", "System", "",
            "Number of open OS handles",
            "handles", "open_handles", "HandleCount", "file_descriptors",
            "fd_count", "open_files"),

        Attr("gc_gen0_collections", "GC Gen0 Collections", "Runtime", "",
            ".NET GC generation 0 collection count",
            "gc gen0", "gen0 collections", "GCGen0", "gc.gen0"),

        Attr("gc_gen1_collections", "GC Gen1 Collections", "Runtime", "",
            ".NET GC generation 1 collection count",
            "gc gen1", "gen1 collections", "GCGen1", "gc.gen1"),

        Attr("gc_gen2_collections", "GC Gen2 Collections", "Runtime", "",
            ".NET GC generation 2 collection count (full GC)",
            "gc gen2", "gen2 collections", "GCGen2", "gc.gen2", "full gc"),

        Attr("gc_heap_mb", "GC Heap Size (MB)", "Runtime", "MB",
            ".NET GC total heap size",
            "heap size", "gc heap", "GCHeapSize", "managed_heap"),

        //Temperature and Hardware
        Attr("cpu_temperature_c", "CPU Temperature (°C)", "Hardware", "°C",
            "CPU die temperature",
            "cpu temp", "processor temp", "cpu_temp", "CPUTemp",
            "CoreTemp", "cpu_thermal", "temperature"),

        Attr("system_uptime_seconds", "System Uptime (s)", "System", "s",
            "System uptime in seconds since last boot",
            "uptime", "system uptime", "SystemUptime", "BootTime",
            "uptime_seconds", "seconds_since_boot"),

        Attr("boot_time", "Last Boot Time", "System", "datetime",
            "Timestamp of last system boot",
            "last boot", "boot time", "BootTime", "last_reboot",
            "system_start_time"),
    ];

    private static CanonicalAttribute Attr(
        string name, string displayName, string category, string unit,
        string description, params string[] aliases)
        => new()
        {
            Id = name,
            Name = name,
            DisplayName = displayName,
            Category = category,
            Unit = unit,
            Description = description,
            KnownAliases = aliases.ToList()
        };
}
