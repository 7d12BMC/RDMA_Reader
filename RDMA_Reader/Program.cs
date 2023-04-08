using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Diagnostics;
using System.Net;
using System.IO;
using Newtonsoft.Json;
using Prometheus;

namespace RDMA_Reader
{
    class Program
    {
        public static int RDMA_Reader_nic_count { get; private set; }
        public static PerformanceCounterCategory category = new PerformanceCounterCategory("RDMA Activity");
        static void Main(string[] args)
        {
            // 设置窗体信息
            string version = "1.1.1";
            Console.Title = "RDMA Metrics v" + version;
            Console.WindowWidth = 40;
            Console.WindowHeight = 15;
            // 获取网卡数量
            try
            {
                RDMA_Reader_nic_count = category.GetInstanceNames().Length;
            }
            catch (Exception)
            {
                RDMA_Reader_nic_count = 0;
            }
            string hostname = Dns.GetHostEntry("localhost").HostName;
            Gauge rdma_gauge_i = Metrics.CreateGauge("rdma_in", "RDMA Inbound Bytes/sec", new GaugeConfiguration { 
                StaticLabels = new Dictionary<string, string> { { "hostname", hostname} },
                LabelNames = new[] { "nic" } 
            });
            Gauge rdma_gauge_o = Metrics.CreateGauge("rdma_out", "RDMA Outbound Bytes/sec", new GaugeConfiguration {
                StaticLabels = new Dictionary<string, string> { { "hostname", hostname } },
                LabelNames = new[] { "nic" } 
            });
            PerformanceCounter[] rdma_pc_i = new PerformanceCounter[RDMA_Reader_nic_count];
            PerformanceCounter[] rdma_pc_o = new PerformanceCounter[RDMA_Reader_nic_count];
            // 获取符合RDMA的网卡
            
            if (RDMA_Reader_nic_count != 0)
            {
                String[] instancename = category.GetInstanceNames();
                
                // 遍历所有网卡
                for (int i = 0; i < instancename.Length; i++)
                {
                    string source_if_name = instancename[i];
                    // 寻找符合属性
                    foreach (PerformanceCounter t in category.GetCounters(source_if_name))
                    {
                        if (t.CounterName == "RDMA Inbound Bytes/sec")
                        {
                            rdma_pc_i[i] = t;
                            t.NextValue();
                        }
                        if (t.CounterName == "RDMA Outbound Bytes/sec")
                        {
                            rdma_pc_o[i] = t;
                            t.NextValue();
                        }
                    }
                }
                Thread.Sleep(1000);
                Console.WriteLine(hostname + " 初始化完成");
                Console.WriteLine("找到" + instancename.Length.ToString() + "块符合RDMA的网卡");
            } else {
                Console.WriteLine("没有符合RDMA的网卡");
            }
            // 启动服务
            int run_port = args.Length != 0 ? Convert.ToInt16(args[0]) : 34567; // 默认端口为34567
            MetricServer _metricsServer = new MetricServer(port: run_port);
            _metricsServer.Start();
            Console.WriteLine("Running 0.0.0.0:" + run_port.ToString());
            // 循环读取数据
            while (true)
            {
                long if_all_i = 0;
                long if_all_o = 0;
                for (int i = 0; i < rdma_pc_i.Length; i++)
                {
                    string if_name = rdma_pc_i[i].InstanceName;
                    if_name = if_name.Replace("HP InfiniBand FDR-Ethernet 10Gb-40Gb 2-port 544+FLR-QSFP Ethernet Adapter", "");
                    if_name = if_name.Replace(" ", "");
                    if_name = if_name.Replace("#", "");
                    if_name = if_name.Length != 0 ? if_name : "1";
                    if_name = "NIC_" + if_name;
                    long if_i = (long)rdma_pc_i[i].NextValue();
                    long if_o = (long)rdma_pc_o[i].NextValue();
                    rdma_gauge_i.WithLabels(if_name).Set(if_i);
                    rdma_gauge_o.WithLabels(if_name).Set(if_o);
                    if_all_i += if_i;
                    if_all_o += if_o;
                }
                rdma_gauge_i.WithLabels("NIC_All").Set(if_all_i);
                rdma_gauge_o.WithLabels("NIC_All").Set(if_all_o);
                Thread.Sleep(TimeSpan.FromSeconds(1));
            }
        }
    }
}
